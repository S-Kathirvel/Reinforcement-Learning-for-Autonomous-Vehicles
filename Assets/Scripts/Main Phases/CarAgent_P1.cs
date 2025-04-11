using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.Collections;
using Unity.MLAgents.Policies;

public class CarAgent_P1 : Agent
{
    [Header("Movement Parameters")]
    public float maxSpeed = 50f;
    public float accelerationRate = 20f;  // Increased from 10
    public float steeringRate = 80f;
    [SerializeField] private float naturalDecay = 0.99f;  // Changed from 0.95
    public float handbrakeDecay = 0.7f;
    public float maxSteeringAngle = 45f;

    [Header("Speed Zone Settings")]
    public float defaultSpeed = 15f;
    public float speedTolerance = 3f;
    [SerializeField] public bool inSpeedZone;
    public float targetSpeed;
    private float lastZoneSpeed;
    
    // New time tracking variables
    // private float timeOutsideZone;
    // private int stepsOutsideZone;

    [Header("Speed Enforcement")]
    public float minNonZoneSpeed = 12f;
    public float speedBuildUpFactor = 0.8f; 
    private float speedDeficitPenalty;

    [Header("LIDAR Settings")]
    public int rayCount = 18;
    public float maxRayDistance = 20f;
    public LayerMask detectionMask;
    private float[] rayRewards;

    [Header("Spawn Position")]
    public float spawnXMin = -25f;
    public float spawnXMax = 25f;
    public float spawnY = 1.25f;
    [SerializeField] public float spawnZ = -475f;

    [Header("Episode Settings")]
    public float maxEpisodeDuration = 90f;
    private float timeInEpisode;

    [Header("Dependencies")]
    public TrainingLogger logger;

    [SerializeField] public float currentSpeed;
    [SerializeField] private float currentSteering;
    private float currentAcceleration = 0f;
    private float currentRotation;
    public NativeArray<RaycastHit> raycastHits;
    private RaycastCommand[] rayCommands;
    private int currentStep;

    [Header("Curriculum Learning")]
    public float initialMinSpeed = 8f;
    public float initialMaxSpeed = 15f;
    public float CurriculumSteps = 2000000f;


    public override void Initialize()
    {
        InitializeLIDARSystem();
        rayRewards = new float[rayCount];
        targetSpeed = defaultSpeed;
        GetComponent<BehaviorParameters>().BehaviorType = BehaviorType.Default;
    }

    void InitializeLIDARSystem()
    {
        rayCommands = new RaycastCommand[rayCount];
        raycastHits = new NativeArray<RaycastHit>(rayCount, Allocator.Persistent);
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
        // timeOutsideZone = 0f;  // Reset time counter
        // stepsOutsideZone = 0;  // Reset step counter
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(currentSpeed / maxSpeed);
        sensor.AddObservation(currentRotation / maxSteeringAngle);
        sensor.AddObservation(inSpeedZone ? 1 : 0);
        sensor.AddObservation(targetSpeed / maxSpeed);

        for (int i = 0; i < rayCount; i++)
        {
            sensor.AddObservation(raycastHits[i].distance / maxRayDistance);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        ApplySpeedDecay();  // DECAY FIRST
        HandleMovement(actions.ContinuousActions);
        CalculateRewards();
        // LogTrainingData();
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
    float speedReward = CalculateSpeedReward();
    float collisionPenalty = CalculateCollisionRisk();
    float stationaryPenalty = (currentSpeed < 1f) ? -0.1f : 0f;
    
    // New speed maintenance penalty
    speedDeficitPenalty = (!inSpeedZone && currentSpeed < minNonZoneSpeed) ? 
        -0.05f * (minNonZoneSpeed - currentSpeed) : 0f;

    // Modified existing penalties with speed scaling
    float laneDriftPenalty = (Mathf.Abs(currentSteering) > 0.5f) ? 
        -0.02f * (currentSpeed/minNonZoneSpeed) : 0f;

    // Progressive collision penalty scaling
    float collisionSeverity = 1 + (currentSpeed/maxSpeed)*2f;
    collisionPenalty *= collisionSeverity;

    AddReward(speedReward + 
             collisionPenalty + 
             stationaryPenalty + 
             speedDeficitPenalty + 
             laneDriftPenalty);
}

float CalculateSpeedReward()
{
    if (!inSpeedZone)
    {
        // Progressive reward curve with minimum speed threshold
        float speedRatio = Mathf.Clamp01((currentSpeed - 8f) / (maxSpeed - 8f));
        return 0.3f * Mathf.Pow(speedRatio, speedBuildUpFactor);
    }

    // Existing zone reward logic
    if (currentSpeed < targetSpeed - speedTolerance)
    {
        return -0.1f * (1 + (targetSpeed - currentSpeed)/5f);
    }

    float speedDifference = currentSpeed - targetSpeed;
    return Mathf.Clamp(speedDifference * 0.02f, -0.1f, 0.15f);
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
                // Reset counters when entering new speed zone
                // timeOutsideZone = 0f;
                // stepsOutsideZone = 0;
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
        timeInEpisode += Time.fixedDeltaTime;
        if (timeInEpisode > maxEpisodeDuration)
        {
            AddReward(-0.5f);
            EndEpisode();
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            AddReward(-5f);
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
    }

    void Update()
    {
        if (Academy.Instance.IsCommunicatorOn)
        {
            float progress = Mathf.Clamp01(GetComponent<CarAgent_P1>().GetCumulativeReward() / 8f);
            minNonZoneSpeed = Mathf.Lerp(initialMinSpeed, initialMaxSpeed, progress);
        }
    }

    void PerformLIDARScan()
    {
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
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetKey(KeyCode.W) ? 1 : Input.GetKey(KeyCode.S) ? -1 : 0;
        continuousActions[1] = Input.GetAxis("Horizontal");
        continuousActions[2] = Input.GetKey(KeyCode.Space) ? 1 : 0;
        Debug.Log("Heuristic Mode is Active");
    }

    void OnDestroy()
    {
        if (raycastHits.IsCreated)
            raycastHits.Dispose();
    }
}