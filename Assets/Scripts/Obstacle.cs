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
            rb.mass = 100f; // Very heavy - almost immovable
            rb.drag = 8f; // High friction - stops quickly
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
        
        // Don't reposition - just grab where it is
        
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
            rb.mass = 1f; // Light when grabbed - easy to pull
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
            rb.mass = 100f; // Back to very heavy
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
