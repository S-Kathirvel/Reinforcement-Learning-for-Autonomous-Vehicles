using TMPro;
using UnityEngine;
using Unity.MLAgents;

public class SpeedUI : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private CarAgent_P2 carAgent;
    [SerializeField] private TMP_Text speedText;
    
    [Header("Settings")]
    [Range(0.1f, 1f)] public float updateInterval = 0.2f;
    
    private float timer;
    private float displayedSpeed;
    private float totalReward;
    private float laneReward;
    private float collisionPenalty;
    private float speedReward;
    private string zoneStatus;

    void Update()
    {
        if (carAgent == null || speedText == null) return;
        
        timer += Time.deltaTime;
        if (timer >= updateInterval)
        {
            UpdateDisplayValues();
            UpdateUIText();
            timer = 0;
        }
    }

    void UpdateDisplayValues()
    {
        // Get current values
        displayedSpeed = Mathf.Lerp(displayedSpeed, carAgent.currentSpeed, 0.5f);
        totalReward = Mathf.Lerp(totalReward, carAgent.GetCumulativeReward(), 0.5f);
        
        // Get reward components (you'll need to expose these in CarAgent_P2)
        laneReward = Mathf.Lerp(laneReward, carAgent.CurrentLaneReward, 0.5f);
        collisionPenalty = Mathf.Lerp(collisionPenalty, carAgent.CurrentCollisionPenalty, 0.5f);
        speedReward = Mathf.Lerp(speedReward, carAgent.CurrentSpeedReward, 0.5f);

        zoneStatus = carAgent.inSpeedZone 
            ? $"<color=#00FF00>Target: {carAgent.targetSpeed:F0}u/s</color>" 
            : "<color=#FFA500>No Zone</color>";
    }

    void UpdateUIText()
    {
        speedText.text = $"<b>SPEED:</b> {displayedSpeed:F1}u/s\n" +
                       $"<b>TOTAL REWARD:</b> {totalReward:F2}\n" +
                       $"<color=#00FF00>+ Speed Reward: {speedReward:F2}</color>\n" +
                       $"<color=#FFD700>+ Lane Reward: {laneReward:F2}</color>\n" +
                       $"<color=#FF0000>- Collisions: {collisionPenalty:F2}</color>\n" +
                       zoneStatus;
    }

    void OnValidate()
    {
        if (carAgent == null)
            carAgent = FindObjectOfType<CarAgent_P2>();
        
        if (speedText == null)
            speedText = GetComponentInChildren<TMP_Text>();
    }
}