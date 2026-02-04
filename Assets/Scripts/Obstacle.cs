using UnityEngine;

public class Obstacle : MonoBehaviour
{
    [Header("Lock State")]
    public bool isLocked = false;
    public AgentTeam? lockedByTeam = null;
    
    [Header("Visual Feedback")]
    public Color unlockedColor = Color.gray;
    public Color hiderLockedColor = Color.blue;
    public Color seekerLockedColor = Color.red;
    
    [Header("Grab Settings")]
    public float grabDistance = 1.5f; // Distance in front of agent when grabbed
    public float lampGrabDistance = 2f; // Distance for Lamp (ramp)
    
    private Rigidbody rb;
    private Renderer rend;
    private HideSeekAgent grabbedBy = null;
    private FixedJoint grabJoint = null;
    private Collider[] myColliders;
    private Collider[] agentColliders;
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // Try self first, then children (for LampHolder structure)
        rend = GetComponent<Renderer>();
        if (rend == null)
        {
            rend = GetComponentInChildren<Renderer>();
        }
        myColliders = GetComponentsInChildren<Collider>();
    }
    
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    
    private void Start()
    {
        // Capture initial state for respawning
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        
        UpdateVisuals();
        
        // Prevent obstacles from ever falling over and make them heavy
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.mass = 70f; // Heavy but moveable
            rb.drag = 8f; // High friction - stops quickly
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // Prevent tunneling through walls
        }
    }
    
    private void FixedUpdate()
    {
        // Check for Out Of Bounds (falling off map)
        if (transform.position.y < -5f)
        {
            Respawn();
            return;
        }

        // If grabbed with joint, the joint handles physics automatically
        // We just need to ensure the obstacle stays at proper height
        if (grabbedBy != null && !isLocked && grabJoint != null)
        {
            // Keep upright (prevent rolling)
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void Respawn()
    {
        // Force release if grabbed
        if (grabbedBy != null)
        {
            grabbedBy.ForceReleaseGrab();
        }
        
        ResetObstacle();
        
        // Restore position
        transform.position = initialPosition;
        transform.rotation = initialRotation;
        
        // Stop physics
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
    
    /// <summary>
    /// Try to grab this obstacle using FixedJoint. Returns true if successful.
    /// </summary>
    public bool TryGrab(HideSeekAgent agent)
    {
        // Can't grab if already grabbed
        if (grabbedBy != null) return false;
        
        // Can't grab if locked by other team
        if (isLocked && lockedByTeam != agent.team) return false;
        
        grabbedBy = agent;
        
        // Get agent's colliders
        agentColliders = agent.GetComponentsInChildren<Collider>();
        
        // Ignore collision between agent and this obstacle while grabbed
        SetCollisionIgnore(true);
        
        // Auto-unlock if locked by same team (allows moving it)
        if (isLocked)
        {
            isLocked = false;
            lockedByTeam = null;
            if (rb != null) rb.isKinematic = false;
            UpdateVisuals();
        }
        
        // SNAP LOGIC: Align closest face to agent
        if (rb != null)
        {
            // 1. Determine which local axis of the box is facing the agent
            Vector3 directToAgent = transform.InverseTransformDirection(agent.transform.position - transform.position);
            
            Vector3 closestAxis = Vector3.forward;
            float maxDot = -Mathf.Infinity;
            
            // Check 4 cardinal directions (we assume box is upright on Y)
            Vector3[] axes = new Vector3[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
            
            foreach (var axis in axes)
            {
                float dot = Vector3.Dot(directToAgent, axis);
                if (dot > maxDot)
                {
                    maxDot = dot;
                    closestAxis = axis;
                }
            }
            
            // 2. Rotate box so that 'closestAxis' points AT the agent (Agent.back)
            // We want the face LOOKING AT the agent to be attached to the agent's FRONT.
            // So Box.Rotation * closestAxis should align with Agent.Back (-Agent.Forward)
            
            // Current world direction of the axis
            // We want to rotate such that: NewRotation * axis = -Agent.Forward
            
            Quaternion targetRotation = Quaternion.LookRotation(-agent.transform.forward, Vector3.up);
            
            // Adjust based on which local axis we are holding
            if (closestAxis == Vector3.forward) targetRotation *= Quaternion.Euler(0, 0, 0); // No adjustment needed if Back is facing Agent? Wait.
            // LookRotation forward is Z+. If we want Z+ to point at Agent (which is -AgentForward relative to World), we use LookRotation(-AgentForward).
            
            // But wait, if closestAxis is Right (X+), we want X+ to point at Agent.
            // So we need to rotate -90 around Y relative to targetRotation.
            if (closestAxis == Vector3.left) targetRotation *= Quaternion.Euler(0, 90, 0); 
            else if (closestAxis == Vector3.right) targetRotation *= Quaternion.Euler(0, -90, 0);
            else if (closestAxis == Vector3.back) targetRotation *= Quaternion.Euler(0, 180, 0);
            
            transform.rotation = targetRotation;
            
            // 3. Position Snap
            float snapDist = grabDistance;
            
            // RAYCAST CLAMP: Check for walls between agent and target position
            // Cast from chest height (up 0.5f) to avoid floor hits
            Vector3 rayOrigin = agent.transform.position + Vector3.up * 0.5f;
            Vector3 rayDirection = agent.transform.forward;
            RaycastHit hit;
            
            // Check for walls up to grabDistance
            // We use a mask for Default and Wall layers
            int layerMask = LayerMask.GetMask("Default", "Wall", "Obstacle"); // Also check against other obstacles
            
            if (Physics.Raycast(rayOrigin, rayDirection, out hit, grabDistance, layerMask))
            {
                // If we hit something (that isn't the object we are grabbing, ideally)
                // Since we are grabbing 'this', we should disable its collider during this check or ignore it?
                // But we haven't moved 'this' yet, so 'this' is at old pos.
                // If 'this' is in front of us, the ray might hit it.
                // We want to find WALLS BEHIND the box or close to agent.
                
                // Acutally, if we hit a wall at 1.0m, we should snap to 0.9m.
                if (hit.collider.gameObject != gameObject && !hit.collider.transform.IsChildOf(transform))
                {
                     snapDist = Mathf.Max(hit.distance - 0.2f, 0.5f); // Keep at least 0.5m away from agent
                }
            }

            Vector3 targetPos = agent.transform.position + agent.transform.forward * snapDist;
            targetPos = new Vector3(targetPos.x, initialPosition.y, targetPos.z); // Keep original Y (floor)
            
            // Reverted overly strict wall check
            
            transform.rotation = targetRotation;
            transform.position = targetPos; // Keep original Y (floor)
            
            // Reset velocities for clean snap
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        // Create FixedJoint to connect obstacle to agent
        grabJoint = gameObject.AddComponent<FixedJoint>();
        grabJoint.connectedBody = agent.GetComponent<Rigidbody>();
        grabJoint.breakForce = Mathf.Infinity; // Don't break
        grabJoint.breakTorque = Mathf.Infinity;
        
        // Configure joint for stability
        grabJoint.enableCollision = false;
        grabJoint.enablePreprocessing = false;
        
        // Reduce physics when grabbed for smooth pulling
        if (rb != null)
        {
            rb.mass = 5f; // Light when grabbed - easy to pull
            rb.drag = 2f;
            rb.angularDrag = 10f;
            // Freeze rotation AND Y position to prevent floating
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        }
        
        return true;
    }
    
    /// <summary>
    /// Release the obstacle from grabbing.
    /// </summary>
    public void Release()
    {
        // Re-enable collision
        if (agentColliders != null)
        {
            SetCollisionIgnore(false);
        }
        
        // Destroy the joint
        if (grabJoint != null)
        {
            Destroy(grabJoint);
            grabJoint = null;
        }
        
        grabbedBy = null;
        agentColliders = null;
        
        // Restore to heavy physics when released
        if (rb != null && !rb.isKinematic)
        {
            rb.mass = 70f; // Back to heavy
            rb.drag = 8f;
            rb.angularDrag = 0.5f;
            rb.constraints = RigidbodyConstraints.FreezeRotation; // Keep upright
            rb.velocity *= 0.3f; // Slow down when released
        }
    }
    
    /// <summary>
    /// Enable or disable collision between this obstacle and the grabbing agent.
    /// </summary>
    private void SetCollisionIgnore(bool ignore)
    {
        if (agentColliders == null || myColliders == null) return;
        
        foreach (var myCol in myColliders)
        {
            foreach (var agentCol in agentColliders)
            {
                if (myCol != null && agentCol != null)
                {
                    Physics.IgnoreCollision(myCol, agentCol, ignore);
                }
            }
        }
    }
    
    /// <summary>
    /// Try to lock this obstacle. Locked obstacles can't be moved.
    /// </summary>
    public bool TryLock(HideSeekAgent agent)
    {
        // Already locked
        if (isLocked) return false;
        
        // Hider cannot lock Lamp
        if (agent.team == AgentTeam.Hider && gameObject.name.Contains("Lamp")) return false;
        
        isLocked = true;
        lockedByTeam = agent.team;
        
        // Release grab first
        if (grabbedBy != null)
        {
            grabbedBy.ForceReleaseGrab();
            Release();
        }
        
        // Lock in place - make kinematic
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.None;
        }
        
        UpdateVisuals();
        return true;
    }
    
    /// <summary>
    /// Try to unlock this obstacle. Only the team that locked it can unlock.
    /// </summary>
    public bool TryUnlock(HideSeekAgent agent)
    {
        if (!isLocked) return false;
        if (lockedByTeam != agent.team) return false;
        
        isLocked = false;
        lockedByTeam = null;
        
        if (rb != null)
        {
            rb.isKinematic = false;
        }
        
        UpdateVisuals();
        return true;
    }
    
    /// <summary>
    /// Reset obstacle state for new episode.
    /// </summary>
    public void ResetObstacle()
    {
        // Clean up joint if exists
        if (grabJoint != null)
        {
            Destroy(grabJoint);
            grabJoint = null;
        }
        
        // Re-enable collisions
        if (agentColliders != null)
        {
            SetCollisionIgnore(false);
        }
        
        isLocked = false;
        lockedByTeam = null;
        grabbedBy = null;
        agentColliders = null;
        
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.drag = 0.5f;
            rb.angularDrag = 0.5f;
            rb.constraints = RigidbodyConstraints.None;
        }
        
        UpdateVisuals();
    }
    
    private void UpdateVisuals()
    {
        if (rend == null) return;
        
        if (!isLocked)
        {
            rend.material.color = unlockedColor;
        }
        else if (lockedByTeam == AgentTeam.Hider)
        {
            rend.material.color = hiderLockedColor;
        }
        else if (lockedByTeam == AgentTeam.Seeker)
        {
            rend.material.color = seekerLockedColor;
        }
    }
    
    public bool IsGrabbed => grabbedBy != null;
    public HideSeekAgent GrabbedBy => grabbedBy;
}
