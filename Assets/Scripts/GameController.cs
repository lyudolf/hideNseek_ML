using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;

public class GameController : MonoBehaviour
{
    [Header("Episode Settings")]
    public float prepPhaseDuration = 10f;
    public float seekPhaseDuration = 30f;
    
    [Header("Agents")]
    public List<HideSeekAgent> hiders = new List<HideSeekAgent>();
    public List<HideSeekAgent> seekers = new List<HideSeekAgent>();
    
    // Convenience properties for single agent access
    public HideSeekAgent hider => hiders.Count > 0 ? hiders[0] : null;
    public HideSeekAgent seeker => seekers.Count > 0 ? seekers[0] : null;
    
    [Header("Spawn Points")]
    public Transform[] hiderSpawnPoints;
    public Transform[] seekerSpawnPoints;
    
    [Header("Environment")]
    public List<Obstacle> obstacles = new List<Obstacle>();
    private Dictionary<Obstacle, Vector3> obstacleInitialPositions = new Dictionary<Obstacle, Vector3>();
    private Dictionary<Obstacle, Quaternion> obstacleInitialRotations = new Dictionary<Obstacle, Quaternion>();
    
    private float episodeTimer;
    private bool isPrepPhase = true;
    public bool IsPrepPhase => isPrepPhase;
    
    private int hidersRemaining;
    
    private void Start()
    {
        // Find all obstacles if not assigned
        if (obstacles.Count == 0)
        {
            obstacles.AddRange(FindObjectsOfType<Obstacle>());
        }
        
        // Save initial poses
        foreach (var obs in obstacles)
        {
            obstacleInitialPositions[obs] = obs.transform.position;
            obstacleInitialRotations[obs] = obs.transform.rotation;
        }
        
        ResetEpisode();
    }
    
    private void FixedUpdate()
    {
        episodeTimer -= Time.fixedDeltaTime;
        
        if (isPrepPhase && episodeTimer <= 0)
        {
            // Prep phase ends, seek phase begins
            isPrepPhase = false;
            episodeTimer = seekPhaseDuration;

        }
        else if (!isPrepPhase && episodeTimer <= 0)
        {
            // Time's up - Hiders win
            EndEpisode(hiderWins: true);
        }
    }
    
    public void OnHiderCaught(HideSeekAgent hider, HideSeekAgent seeker)
    {
        if (isPrepPhase) return; // Can't catch during prep
        
        // Reward/Penalty
        float timeBonus = episodeTimer / seekPhaseDuration;
        seeker.AddReward(1f + timeBonus); // Bonus for catching quickly
        hider.AddReward(-1f);
        
        hider.SetActive(false);
        hidersRemaining--;
        

        
        if (hidersRemaining <= 0)
        {
            // All hiders caught - Seekers win
            EndEpisode(hiderWins: false);
        }
    }
    
    private void EndEpisode(bool hiderWins)
    {
        if (hiderWins)
        {
            // Hiders survived
            foreach (var hider in hiders)
            {
                hider.AddReward(1f);
            }
            foreach (var seeker in seekers)
            {
                seeker.AddReward(-1f);
            }

        }
        else
        {

        }
        
        // End all agents
        foreach (var agent in hiders) agent.EndEpisode();
        foreach (var agent in seekers) agent.EndEpisode();
        
        ResetEpisode();
    }
    
    private void ResetEpisode()
    {
        isPrepPhase = true;
        episodeTimer = prepPhaseDuration;
        hidersRemaining = hiders.Count;
        
        // Reset obstacles
        foreach (var obs in obstacles)
        {
            obs.ResetObstacle(); // Reset state (grabbed, locked, etc)
            
            // Restore position/rotation
            if (obstacleInitialPositions.ContainsKey(obs))
            {
                obs.transform.position = obstacleInitialPositions[obs];
                obs.transform.rotation = obstacleInitialRotations[obs];
            }
        }
        
        // Reset agent positions
        for (int i = 0; i < hiders.Count && i < hiderSpawnPoints.Length; i++)
        {
            hiders[i].transform.position = hiderSpawnPoints[i].position;
            hiders[i].transform.rotation = hiderSpawnPoints[i].rotation;
            hiders[i].SetActive(true);
        }
        
        for (int i = 0; i < seekers.Count && i < seekerSpawnPoints.Length; i++)
        {
            seekers[i].transform.position = seekerSpawnPoints[i].position;
            seekers[i].transform.rotation = seekerSpawnPoints[i].rotation;
        }
        

    }
    
    private void OnGUI()
    {
        // Main style
        GUIStyle style = new GUIStyle();
        style.fontSize = 28;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.white;
        
        // Shadow style for outline effect
        GUIStyle shadowStyle = new GUIStyle(style);
        shadowStyle.normal.textColor = Color.black;
        
        string phase = isPrepPhase ? "PREP PHASE" : "SEEK PHASE";
        string timer = $"{episodeTimer:F1}s";
        string hiders = $"Hiders: {hidersRemaining}";
        
        // Draw shadow (offset by 2 pixels)
        GUI.Label(new Rect(12, 12, 300, 35), phase, shadowStyle);
        GUI.Label(new Rect(12, 47, 300, 35), timer, shadowStyle);
        GUI.Label(new Rect(12, 82, 300, 35), hiders, shadowStyle);
        
        // Draw main text
        GUI.Label(new Rect(10, 10, 300, 35), phase, style);
        GUI.Label(new Rect(10, 45, 300, 35), timer, style);
        GUI.Label(new Rect(10, 80, 300, 35), hiders, style);
    }
}
