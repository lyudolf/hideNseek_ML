using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public enum AgentTeam { Hider, Seeker }

public class HideSeekAgent : Agent
{
    [Header("Team")]
    public AgentTeam team = AgentTeam.Hider;
    
    [Header("Movement")]
    public float hiderSpeed = 20f;
    public float seekerSpeed = 23f; // Seeker is 15% faster to catch kiting Hiders
    private float moveSpeed; // Internal usage
    public float rotateSpeed = 200f;
    
    [Header("Grab Settings")]
    public float grabRange = 2f;
    
    [Header("References")]
    public GameController gameController;
    
    private Rigidbody rb;
    private bool isActive = true;
    private Obstacle grabbedObstacle = null;
    
    // Input buffering for heuristic mode
    private bool grabKeyPressed = false;
    private bool lockKeyPressed = false;
    
    // Lock survival tracking
    private bool hasLockedObstacle = false;
    private float lockTime = 0f;
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }
    
    private void Update()
    {
        // Check if fallen off map
        if (transform.localPosition.y < -5f)
        {
            AddReward(-1f); // Big penalty for suicide/falling
            EndEpisode();
            return;
        }

        // Buffer keyboard input (Update runs every frame, Heuristic may not)
        if (Input.GetKeyDown(KeyCode.F)) grabKeyPressed = true;
        if (Input.GetKeyDown(KeyCode.R)) lockKeyPressed = true;
    }
    
    public override void Initialize()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        
        // Set speed based on team
        moveSpeed = (team == AgentTeam.Seeker) ? seekerSpeed : hiderSpeed;
        
        // Freeze rotation only - allow Y movement for ramp climbing
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        
        // Heavy and grounded to prevent flying
        rb.mass = 5f;
        rb.drag = 2f;
    }
    
    public override void OnEpisodeBegin()
    {
        // Release any grabbed obstacle
        if (grabbedObstacle != null)
        {
            grabbedObstacle.Release();
            grabbedObstacle = null;
        }
        
        // Reset physics
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        isActive = true;
    }
    
    public override void CollectObservations(VectorSensor sensor)
    {
        // NO CHEATING: Seekers should be BLIND during prep phase!
        // If they have memory, they would memorize where Hiders went.
        if (team == AgentTeam.Seeker && gameController != null && gameController.IsPrepPhase)
        {
            // Add empty/zero observations to keep input shape consistant
            sensor.AddObservation(Vector3.zero); // Pos
            sensor.AddObservation(Vector3.zero); // Fwd
            sensor.AddObservation(Vector3.zero); // Vel
            sensor.AddObservation(1f);           // Prep Phase (They know it's prep)
            sensor.AddObservation(0f);           // IsGrabbing
            return;
        }

        // Local position (normalized)
        sensor.AddObservation(transform.localPosition / 10f);  // 3
        
        // Forward direction
        sensor.AddObservation(transform.forward);  // 3
        
        // Velocity
        if (rb != null)
            sensor.AddObservation(rb.velocity / moveSpeed);  // 3
        else
            sensor.AddObservation(Vector3.zero);
        
        // Is prep phase?
        sensor.AddObservation(gameController != null && gameController.IsPrepPhase ? 1f : 0f);  // 1
        
        // Am I holding something? (Tactile sense)
        sensor.AddObservation(IsGrabbing ? 1f : 0f); // 1
        
        // Opponent direction REMOVED for fairness
        // Agents must rely on Ray Perception Sensor to find each other!
        
        // Total: 3 + 3 + 3 + 1 + 1 = 11 (Changed from 10)
    }
    
    /// <summary>
    /// Find the nearest active opponent (supports 2v2 multi-agent).
    /// </summary>
    private HideSeekAgent FindNearestOpponent()
    {
        if (gameController == null) return null;
        
        var opponents = team == AgentTeam.Seeker ? gameController.hiders : gameController.seekers;
        
        HideSeekAgent nearest = null;
        float minDistance = float.MaxValue;
        
        foreach (var opp in opponents)
        {
            if (opp == null || !opp.IsActive) continue;
            
            float dist = Vector3.Distance(transform.localPosition, opp.transform.localPosition);
            if (dist < minDistance)
            {
                minDistance = dist;
                nearest = opp;
            }
        }
        
        return nearest;
    }
    
    public bool IsActive => isActive;
    
    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!isActive) return;
        
        // Seeker frozen during prep phase
        if (team == AgentTeam.Seeker && gameController != null && gameController.IsPrepPhase)
        {
            return;
        }
        
        // Continuous actions: Move X (world), Move Z (world)
        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];
        
        // Apply movement in WORLD space (instant, no acceleration)
        // W=-Z, S=+Z, A=+X, D=-X
        Vector3 move = new Vector3(moveX, 0, moveZ);
        Vector3 targetVelocity = move * moveSpeed;
        targetVelocity.y = rb.velocity.y; // Preserve vertical velocity (gravity)
        rb.velocity = targetVelocity;
        
        // Face movement direction (only when actively moving)
        if (move.sqrMagnitude > 0.5f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(move);
            transform.rotation = targetRotation; // Instant rotation
        }
        
        // Discrete actions: 0=Nothing, 1=Grab/Release, 2=Lock
        if (actions.DiscreteActions.Length > 0)
        {
            int grabAction = actions.DiscreteActions[0];
            if (grabAction == 1)
            {
                ToggleGrab();
            }
            else if (grabAction == 2)
            {
                TryLock();
            }
        }
        
        // Activity penalty removed to allow Hiders to camp quietly without jittering
        // AddReward(-0.0001f);
        
        // Distance-based reward shaping (using nearest opponent for 2v2)
        if (gameController != null && !gameController.IsPrepPhase)
        {
            HideSeekAgent opponent = FindNearestOpponent();
            
            if (opponent != null)
            {
                float distance = Vector3.Distance(transform.localPosition, opponent.transform.localPosition);
                float normalizedDistance = Mathf.Clamp01(distance / 20f);
                
                if (team == AgentTeam.Seeker)
                {
                    // Seeker: NO continuous distance reward!
                    // (Prevents wall-hugging when Hider is on the other side)
                    // Seeker must rely on Vision (RayPerception) and Curiosity.
                }
                else // Hider
                {
                    // Hider: NO continuous distance reward! 
                    // (Prevents leaving safe room just because Seeker is outside the wall)
                    
                    // Bonus ONLY for active fleeing when very close
                    // REMOVED for Pure RL (v5)
                    // Vector3 awayFromOpponent = (transform.localPosition - opponent.transform.localPosition).normalized;
                    // float fleeSpeed = Vector3.Dot(rb.velocity.normalized, awayFromOpponent);
                    // if (fleeSpeed > 0.5f && distance < 8f)
                    // {
                    //    AddReward(0.001f);
                    // }
                }
            }
        }
        
        // Lock survival reward - only if Hider locked a box AND survives
        // REMOVED for Pure RL (v5)
        // UpdateLockSurvivalReward();
    }
    
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        
        // World space movement: A=+X, D=-X, W=-Z, S=+Z
        float moveX = 0f;
        float moveZ = 0f;
        
        if (Input.GetKey(KeyCode.A)) moveX = 1f;   // +X
        if (Input.GetKey(KeyCode.D)) moveX = -1f;  // -X
        if (Input.GetKey(KeyCode.W)) moveZ = -1f;  // -Z
        if (Input.GetKey(KeyCode.S)) moveZ = 1f;   // +Z
        
        continuousActions[0] = moveX;
        continuousActions[1] = moveZ;
        continuousActions[2] = 0f; // No rotation needed for world-space movement
        
        // Discrete actions for grab/lock
        var discreteActions = actionsOut.DiscreteActions;
        if (discreteActions.Length > 0)
        {
            discreteActions[0] = 0;
            
            if (grabKeyPressed)
            {
                discreteActions[0] = 1;
                grabKeyPressed = false;
            }
            else if (lockKeyPressed)
            {
                discreteActions[0] = 2;
                lockKeyPressed = false;
            }
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        // Seeker catches Hider
        if (team == AgentTeam.Seeker && collision.gameObject.TryGetComponent<HideSeekAgent>(out var other))
        {
            if (other.team == AgentTeam.Hider)
            {
                gameController?.OnHiderCaught(other, this);
            }
        }
    }
    
    public void SetActive(bool active)
    {
        isActive = active;
    }
    
    // ========== Grab/Lock Functions ==========
    
    /// <summary>
    /// Find nearest obstacle in grab range.
    /// </summary>
    private Obstacle FindNearestObstacle()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, grabRange);
        Obstacle nearest = null;
        float nearestDist = float.MaxValue;
        
        foreach (var col in colliders)
        {
            if (col.TryGetComponent<Obstacle>(out var obstacle))
            {
                float dist = Vector3.Distance(transform.position, obstacle.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = obstacle;
                }
            }
        }
        return nearest;
    }
    
    /// <summary>
    /// Try to grab or release an obstacle.
    /// </summary>
    public void ToggleGrab()
    {

        
        if (grabbedObstacle != null)
        {
            // Release

            grabbedObstacle.Release();
            grabbedObstacle = null;
        }
        else
        {
            // Try to grab nearest
            Obstacle nearest = FindNearestObstacle();

            
            if (nearest != null && nearest.TryGrab(this))
            {
                grabbedObstacle = nearest;
                // No reward for just grabbing - reward comes from locking + surviving
            }
            else if (nearest == null)
            {

            }
        }
    }

    /// <summary>
    /// Forcefully release the grab.
    /// </summary>
    public void ForceReleaseGrab()
    {
        grabbedObstacle = null;
    }
    
    /// <summary>
    /// Try to lock the grabbed obstacle.
    /// </summary>
    public void TryLock()
    {
        Obstacle target = grabbedObstacle;
        if (target != null && target.TryLock(this))
        {
            // Mark that we locked something - reward comes from surviving after lock
            hasLockedObstacle = true;
            lockTime = Time.time;
            grabbedObstacle = null;
        }
    }
    
    /// <summary>
    /// Called each step to give survival bonus after locking.
    /// </summary>
    public void UpdateLockSurvivalReward()
    {
        if (hasLockedObstacle && team == AgentTeam.Hider)
        {
            float timeSinceLock = Time.time - lockTime;
            // Give small continuous reward for surviving after lock (max 5 seconds)
            if (timeSinceLock <= 5f)
            {
                AddReward(0.0005f); // Reduced from 0.002f to prevent "Lock Farming"
            }
        }
    }
    
    public bool IsGrabbing => grabbedObstacle != null;
}
