using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.Collections;
using Unity.MLAgents.Policies;
using System.Linq;

public class CarAgent_P2 : Agent
{

    [Header("Movement Parameters")]
    public float maxSpeed = 150f;
    public float accelerationRate = 60f;  // Increased from 10
    public float steeringRate = 80f;
    [SerializeField] private float naturalDecay = 0.98f;  // Changed from 0.95
    public float handbrakeDecay = 0.7f;
    public float maxSteeringAngle = 45f;

    [Header("Speed Zone Settings")]
    public float defaultSpeed = 15f;
    public float speedTolerance = 3f;
    [SerializeField] public bool inSpeedZone;
    public float targetSpeed;
    private float lastZoneSpeed;

    [HideInInspector] public float CurrentLaneReward { get; private set; }
    [HideInInspector] public float CurrentCollisionPenalty { get; private set; }
    [HideInInspector] public float CurrentSpeedReward { get; private set; }
    
    [Header("Speed Enforcement")]
    public float minNonZoneSpeed = 15f;
    public float speedBuildUpFactor = 0.8f; 
    // private float speedDeficitPenalty;

    [Header("LIDAR Settings")]
    public int rayCount = 18;
    public float maxRayDistance = 35f;
    public LayerMask detectionMask;
    private float[] rayRewards;

    [Header("Spawn Position")]
    public float spawnXMin = -25f;
    public float spawnXMax = 25f;
    public float spawnY = 2.6f;
    [SerializeField] public float spawnZ = -475f;

    [Header("Lane Detection")]
    [SerializeField] private bool[] laneHits;
    [SerializeField] private float[] laneDistances;

    [Header("Adaptive Lane Params")]
    public float steeringPenaltyCurve = 0.3f; // Previously hardcoded 0.5
    public float detectionWeightPower = 0.33f; // Exponent for detection weighting
    [SerializeField] private int detectedRayCount; // For debugging

    [Header("Lane Settings")]
    public int numberOfLanes = 4;
    public float laneWidth = 25f;
    public float laneDeviationTolerance = 0.7f;
    public float maxRewardDeviation = 1.5f;
    [SerializeField] private float currentLaneCenter;
    private float laneDeviation;

    [Header("Lane Detection Rays")]
    public int laneRayCount = 4;
    public float frontRaySpreadAngle = 75f;
    public float backRaySpreadAngle = 75f;
    public float laneRayDownwardAngle = -8.5f;
    public float laneRayMaxDistance =25f;
    public LayerMask laneDetectionMask;
    public NativeArray<RaycastHit> laneRaycastHits;
    private RaycastCommand[] laneRayCommands;

    [Header("Episode Settings")]
    public float maxEpisodeDuration = 600f;
    private float timeInEpisode = 0f;
    private int episodeNumber = 0;
    private float RewardLog = 0f;

    [Header("Dependencies")]
    public TrainingLogger logger;

    [SerializeField] public float currentSpeed;
    [SerializeField] private float currentSteering;
    private float currentAcceleration = 0f;
    private float currentRotation;
    public NativeArray<RaycastHit> raycastHits;
    private RaycastCommand[] rayCommands;
    private int currentStep;

    [Header("Debug Settings")]
    public bool showLaneRays = true;
    public Color hitColor = Color.green;
    public Color missColor = Color.red;


    public override void Initialize()
    {
        InitializeLIDARSystem();
        InitializeLanes();
        rayRewards = new float[rayCount];
        targetSpeed = defaultSpeed;
        GetComponent<BehaviorParameters>().BehaviorType = BehaviorType.Default;
        laneHits = new bool[laneRayCount];
        laneDistances = new float[laneRayCount];
    }

    void InitializeLIDARSystem()
    {
        rayCommands = new RaycastCommand[rayCount];
        raycastHits = new NativeArray<RaycastHit>(rayCount, Allocator.Persistent);

        laneRayCommands = new RaycastCommand[laneRayCount];
        laneRaycastHits = new NativeArray<RaycastHit>(laneRayCount, Allocator.Persistent);
    }

    void InitializeLanes()
    {
        float spawnWidth = spawnXMax - spawnXMin;
        numberOfLanes = Mathf.RoundToInt(spawnWidth / laneWidth);
        currentLaneCenter = Mathf.Round(transform.position.x / laneWidth) * laneWidth;
    }

    public override void OnEpisodeBegin()
    {
        transform.localPosition = new Vector3(Random.Range(spawnXMin, spawnXMax), spawnY, spawnZ);
        transform.localRotation = Quaternion.identity;

        currentSpeed = 0f;
        currentSteering = 0f;
        currentRotation = 0f;
        currentAcceleration = 0f;
        timeInEpisode = 0f;
        targetSpeed = defaultSpeed;
        inSpeedZone = false;
        episodeNumber++;
        RewardLog = 0f;
  // Reset step counter
    }

    public override void CollectObservations(VectorSensor sensor)
{
    // Existing observations
    sensor.AddObservation(currentSpeed / maxSpeed);
    sensor.AddObservation(currentRotation / maxSteeringAngle);
    sensor.AddObservation(inSpeedZone ? 1 : 0);
    sensor.AddObservation(targetSpeed / maxSpeed);

    // Obstacle detection observations
    for (int i = 0; i < rayCount; i++)
    {
        sensor.AddObservation(raycastHits[i].distance / maxRayDistance);
    }

    // Lane detection observations
    for (int i = 0; i < laneRayCount; i++)
    {
        bool hit = laneRaycastHits[i].collider != null && 
                 laneRaycastHits[i].collider.CompareTag("Lane");
        float distance = hit ? laneRaycastHits[i].distance : -1f;
        
        sensor.AddObservation(hit ? 1 : 0);
        sensor.AddObservation(distance / laneRayMaxDistance);
    }

    // Lane position observations
    sensor.AddObservation(laneDeviation);
    sensor.AddObservation(currentLaneCenter / spawnXMax);
    sensor.AddObservation((float)detectedRayCount/laneRayCount);
}

    void UpdateLanePosition()
    {
        float leftSum = 0f, rightSum = 0f;
    int validHits = 0;

    for (int i = 0; i < laneRayCount; i++)
    {
        if (!laneHits[i]) continue;
        
        Vector3 hitPoint = laneRaycastHits[i].point;
        float lateralDistance = hitPoint.x - transform.position.x;

        // Front rays (0-180°)
        if (i < laneRayCount/2)
        {
            if (lateralDistance < 0) leftSum += Mathf.Abs(lateralDistance);
            else rightSum += lateralDistance;
        }
        // Back rays (180-360°)
        else
        {
            if (lateralDistance < 0) rightSum += Mathf.Abs(lateralDistance);
            else leftSum += lateralDistance;
        }
        
        validHits++;
    }

    if (validHits > 0)
    {
        float avgLeft = leftSum / validHits;
        float avgRight = rightSum / validHits;
        currentLaneCenter = transform.position.x - avgLeft + (avgLeft + avgRight)/2;
    }
    else
    {
        currentLaneCenter = Mathf.Round(transform.position.x / laneWidth) * laneWidth;
    }

    laneDeviation = Mathf.Clamp(
        (transform.position.x - currentLaneCenter) / (laneWidth/2), 
        -1f, 1f
    );

    detectedRayCount = 0;
    for(int i=0; i<laneRayCount; i++){
        laneHits[i] = laneRaycastHits[i].collider != null && 
                     laneRaycastHits[i].collider.CompareTag("Lane");
        if(laneHits[i]) detectedRayCount++;
    }
}

    public override void OnActionReceived(ActionBuffers actions)
    {
        HandleMovement(actions.ContinuousActions);
        ApplySpeedDecay();  // DECAY FIRST
        CalculateRewards();
        CheckEpisodeTimeout();
        UpdatePosition();
    }

    void ApplySpeedDecay()
    {
        currentSpeed *= naturalDecay;
    }

    void HandleMovement(ActionSegment<float> actions)
    {   
        ApplyThrottle(actions[0]);
        ApplySteering(actions[1]);
        ApplyBrake(actions[2]);
    }

    void UpdatePosition()
    {
        transform.localPosition += transform.forward * currentSpeed * Time.fixedDeltaTime;
        currentRotation = Mathf.Clamp(currentRotation, -maxSteeringAngle, maxSteeringAngle);
        transform.localRotation = Quaternion.Euler(0, currentRotation, 0);
        UpdateLanePosition();
    }

    void ApplyThrottle(float input)
    {
        float frameAcceleration = 0f;
        float throttleResponse = Mathf.Pow(Mathf.Abs(input), 1.5f);
        if (input > 0)
        {
            float speedFactor = Mathf.Clamp01(1 - (currentSpeed/maxSpeed));
            float acceleration = throttleResponse * accelerationRate * speedFactor * Time.fixedDeltaTime;
            currentSpeed += acceleration;
        }
        else if (input < 0) // Reversing
        {
            frameAcceleration = input * (accelerationRate / 2) * Time.fixedDeltaTime;
        }
        else // Coasting - no artificial braking
        {
            return;
        }

        currentSpeed += frameAcceleration;
        currentSpeed = Mathf.Clamp(currentSpeed, -maxSpeed/2, maxSpeed);
        currentAcceleration = frameAcceleration / Time.fixedDeltaTime;
    }

    void ApplySteering(float input)
    {
        currentSteering = input * steeringRate * Time.fixedDeltaTime;
        currentRotation += currentSteering * Mathf.Clamp01(currentSpeed / maxSpeed);
    }

    void ApplyBrake(float input)
    {
        if (input > 0.5f)
        {
            currentSpeed *= handbrakeDecay;
            currentAcceleration = 0f;
        }
    }

    void CalculateRewards()
    {   
    CurrentSpeedReward = CalculateSpeedReward();
    CurrentLaneReward = CalculateLaneReward();
    CurrentCollisionPenalty = CalculateCollisionRisk();
    
    // Add any other reward components you have
    float stationaryPenalty = (currentSpeed < 1f) ? -0.1f : 0f;
    float speedDeficitPenalty = (!inSpeedZone && currentSpeed < minNonZoneSpeed) ? 
        -0.05f * (minNonZoneSpeed - currentSpeed) : 0f;

    // Sum all components
    float totalReward = CurrentSpeedReward 
                      + CurrentLaneReward 
                      + CurrentCollisionPenalty
                      + stationaryPenalty
                      + speedDeficitPenalty
                      + 0.0001F;

    AddReward(totalReward);
    RewardLog += totalReward; // Log the reward for this step
}

float CalculateSpeedReward()
{
    // Non-speed zone rewards
    if (!inSpeedZone)
    {
        float speedRatio = Mathf.Clamp01((currentSpeed - minNonZoneSpeed) / (maxSpeed - minNonZoneSpeed));
        float baseReward = 0.4f * Mathf.Pow(speedRatio, 1.5f);
        return currentSpeed < minNonZoneSpeed ? 
            -0.03f * (minNonZoneSpeed - currentSpeed) : baseReward;
    }

    // Speed zone parameters
    float maxSpeedInZone = targetSpeed * 1.4f;
    float minSpeedInZone = targetSpeed * 0.8f;

    // Overspeed penalty (softer)
    if (currentSpeed > maxSpeedInZone)
    {
        float overRatio = (currentSpeed - targetSpeed) / (maxSpeedInZone - targetSpeed);
        return Mathf.Clamp(-0.04f * overRatio, -0.05f, -0.01f);
    }

    // Underspeed penalty (quadratic)
    if (currentSpeed < minSpeedInZone)
    {
        float deficit = (minSpeedInZone - currentSpeed) / minSpeedInZone;
        return -0.04f * deficit * deficit;
    }

    // Target speed rewards
    float speedDifference = currentSpeed - targetSpeed;
    float perfectBand = targetSpeed * 0.05f; // ±5% window
    
    if (Mathf.Abs(speedDifference) <= perfectBand)
    {
        return 0.1f; // Flat bonus for perfect speed
    }
    
    // Graduated slope outside perfect band
    return Mathf.Clamp(speedDifference * 0.025f, -0.04f, 0.12f);
}

float CalculateLaneReward()
{
    // Count detected rays and calculate coverage
    int detectedRays = laneHits.Count(h => h);
    float rayCoverage = Mathf.Clamp01((float)detectedRays / laneRayCount);
    
    // Normalized deviation (0 = center, 1 = max deviation)
    float normDeviation = Mathf.Clamp01(Mathf.Abs(laneDeviation) / maxRewardDeviation);
    
    // Centering reward with softer falloff
    float centerReward = Mathf.Exp(-0.2f * normDeviation * normDeviation);
    
    // Speed factor with minimum threshold
    float speedFactor = currentSpeed < minNonZoneSpeed ? 0 : 
        Mathf.Clamp01(currentSpeed / targetSpeed);
    
    // Final calculation
    return 0.9f * rayCoverage * centerReward * speedFactor;
}

        float CalculateCollisionRisk()
    {
        float totalPenalty = 0f;

        for (int i = 0; i < rayCount; i++)
        {
            float distance = raycastHits[i].distance;

            if (distance > 0 && distance < 10f)
            {
                float penaltyFactor = (distance < 6f) ? Mathf.Pow(1.2f, 6f - distance) : 1f;
                float penalty = -0.015f * penaltyFactor; // Reduced base penalty

                // Apply reduced penalty outside speed zones
                if (!inSpeedZone) penalty *= 0.9f;

                totalPenalty += penalty;

                if (distance < 1f)
                {
                    EndEpisode();
                    return totalPenalty;
                }
            }
        }
        return totalPenalty;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("SpeedZone"))
        {
            var zone = other.GetComponent<SpeedZone>();
            if (zone != null)
            {
                inSpeedZone = true;
                lastZoneSpeed = zone.targetSpeed;
                targetSpeed = zone.targetSpeed;
                AddReward(0.2f);

            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("SpeedZone"))
        {
            inSpeedZone = false;
            float speedDifference = Mathf.Abs(currentSpeed - lastZoneSpeed); // Use zone's speed
            targetSpeed = defaultSpeed;
            
            // Smoother reward curve based on zone's requirements
            float exitReward = Mathf.Clamp(1 - (speedDifference / lastZoneSpeed), -0.1f, 0.3f);
            AddReward(exitReward);
        }
    }

    void CheckEpisodeTimeout()
    {
        if (timeInEpisode > maxEpisodeDuration)
        {
            AddReward(50f);
            RewardLog -= 50f;
            logger.LogEpisodeEnd(episodeNumber, timeInEpisode, RewardLog, "Timeout");
            EndEpisode();
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            AddReward(-100f);
            RewardLog -= 100f;
            logger.LogEpisodeEnd(episodeNumber, timeInEpisode, RewardLog, "WallCollision");
            Debug.Log("Episode Failed: Collision with Wall!");
            EndEpisode();
        }
        else if (collision.gameObject.CompareTag("vehicleNPC"))
        {
            AddReward(-80f);
            RewardLog -= 80f;
            logger.LogEpisodeEnd(episodeNumber, timeInEpisode, RewardLog,"NPCCollision");
            Debug.Log("Episode Failed: Collision with NPC Vehicle!");
            EndEpisode();
        }
        else if (collision.gameObject.CompareTag("Finish"))
        {
            AddReward(100f);
            RewardLog += 100f;
            logger.LogEpisodeEnd(episodeNumber, timeInEpisode, RewardLog, "Success");
            Debug.Log("Episode Success: Reached Finish Line!");
            EndEpisode();
        }
    }

    // void LogTrainingData()
    // {
    //     currentStep++;
    //     if (logger != null)
    //     {
    //         logger.LogData(CompletedEpisodes, currentStep, currentSpeed, 
    //                      GetCumulativeReward(), currentSteering, currentAcceleration);
    //     }
    // }

    void FixedUpdate()
    {
        if (raycastHits.IsCreated)
            PerformLIDARScan();

        timeInEpisode += Time.fixedDeltaTime;
        CheckEpisodeTimeout();
    }

    void PerformLIDARScan()
{
    // Existing LIDAR scan code
    Vector3 origin = transform.position;
    NativeArray<RaycastCommand> commands = new NativeArray<RaycastCommand>(rayCount, Allocator.TempJob);
    QueryParameters queryParams = new QueryParameters { layerMask = detectionMask };

    for (int i = 0; i < rayCount; i++)
    {
        Vector3 direction = Quaternion.Euler(0, i * (360f / rayCount), 0) * transform.forward;
        commands[i] = new RaycastCommand(origin, direction, queryParams, maxRayDistance);
    }

    RaycastCommand.ScheduleBatch(commands, raycastHits, 1).Complete();
    commands.Dispose();

    // Fixed lane detection scan
    NativeArray<RaycastCommand> laneCommands = new NativeArray<RaycastCommand>(laneRayCount, Allocator.TempJob);
    QueryParameters laneQueryParams = new QueryParameters { layerMask = laneDetectionMask }; // Added missing definition
    int frontRays = laneRayCount / 2;
    int backRays = laneRayCount - frontRays;

    // Front rays
    float frontAngleIncrement = frontRaySpreadAngle / (frontRays - 1);
    float frontStartAngle = -frontRaySpreadAngle / 2;
    
    for (int i = 0; i < frontRays; i++)
    {
        float horizontalAngle = frontStartAngle + i * frontAngleIncrement;
        Vector3 direction = Quaternion.Euler(
            -laneRayDownwardAngle,
            horizontalAngle, 
            0
        ) * transform.forward;
        
        laneCommands[i] = new RaycastCommand(
            transform.position + Vector3.up * 0.5f,
            direction, 
            laneQueryParams,  // Now using properly defined parameters
            laneRayMaxDistance
        );
    }

    // Back rays
    float backAngleIncrement = backRaySpreadAngle / (backRays - 1);
    float backStartAngle = 180 - backRaySpreadAngle / 2;
    
    for (int i = 0; i < backRays; i++)
    {
        float horizontalAngle = backStartAngle + i * backAngleIncrement;
        Vector3 direction = Quaternion.Euler(
            -laneRayDownwardAngle,
            horizontalAngle, 
            0
        ) * transform.forward;
        
        laneCommands[frontRays + i] = new RaycastCommand(
            transform.position + Vector3.up * 0.5f,
            direction, 
            laneQueryParams,  // Now using properly defined parameters
            laneRayMaxDistance
        );
    }

RaycastCommand.ScheduleBatch(laneCommands, laneRaycastHits, 1).Complete();
    laneCommands.Dispose();

    // NEW: Lane ray debugging
    if (showLaneRays)
    {
        // Debug.Log("--- LANE RAY UPDATE ---");
        for (int i = 0; i < laneRayCount; i++)
        {
            Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
            Vector3 direction = laneRayCommands[i].direction;
            float distance = laneRaycastHits[i].distance;
            bool isLane = laneRaycastHits[i].collider != null && 
                        laneRaycastHits[i].collider.CompareTag("Lane");

            // Visual debug in Scene view
            Debug.DrawRay(rayOrigin, direction * (isLane ? distance : laneRayMaxDistance),
                        isLane ? hitColor : missColor, 0.1f);

            // Console debug
            // Debug.Log($"Ray {i}: " + 
                    // $"Detected {(isLane ? "LANE" : "nothing")} " +
                    // $"(Distance: {(isLane ? distance.ToString("F1") : "N/A")})");
        }
    }
}
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetKey(KeyCode.W) ? 1 : Input.GetKey(KeyCode.S) ? -1 : 0;
        continuousActions[1] = Input.GetAxis("Horizontal");
        continuousActions[2] = Input.GetKey(KeyCode.Space) ? 1 : 0;
        // Debug.Log("Heuristic Mode is Active");
    }

    void OnDestroy()
    {
        if (raycastHits.IsCreated)
            raycastHits.Dispose();
        if (laneRaycastHits.IsCreated)
            laneRaycastHits.Dispose();
    }

}