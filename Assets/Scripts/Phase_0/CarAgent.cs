using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class CarAgent : Agent
{
    public float maxSpeed = 30f;
    public float acceleration = 4f;
    public float deceleration = 4f;
    public float handBrakeDeceleration = 10f;
    public float maxSteeringAngle = 25f; // Steering angle in degrees
    public float steeringSpeed = 100f;

    // Raycast settings
    public float[] raycastDistances = { 6f, 6f, 4.6f, 4.6f, 5f, 5f }; // Distances for each raycast
    public float[] raycastAngles = { -30f, 30f, -90f, 90f, 180f, 0f }; // Angles for each raycast (in degrees)

    // Debug option to toggle raycasts
    public bool showRaycasts = true;

    // Reward display fields
    [SerializeField] private float currentTotalReward; // Current total reward
    [SerializeField] private float[] raycastRewards; // Rewards for each raycast

    private Rigidbody rb;
    private float currentSpeed = 0f;
    private float currentSteeringAngle = 0f;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private float timeInSamePosition;
    private Vector3 lastPosition;

    // New variables for reversing penalty
    private float timeReversing = 0f; // Tracks how long the car has been reversing
    private bool isReversing = false; // Tracks if the car is currently reversing

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        startPosition = transform.localPosition;
        startRotation = transform.localRotation;
        lastPosition = startPosition;
        timeInSamePosition = 0f;

        // Initialize raycast rewards array
        raycastRewards = new float[raycastDistances.Length];
    }

    public override void OnEpisodeBegin()
    {
        transform.localPosition = startPosition;
        transform.localRotation = startRotation;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        currentSpeed = 0f;
        currentSteeringAngle = 0f;
        timeInSamePosition = 0f;
        lastPosition = startPosition;

        // Reset raycast rewards
        for (int i = 0; i < raycastRewards.Length; i++)
        {
            raycastRewards[i] = 0f;
        }

        // Reset reversing timer
        timeReversing = 0f;
        isReversing = false;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Add observations: speed, steering angle, and velocity
        sensor.AddObservation(currentSpeed / maxSpeed); // Normalized speed
        sensor.AddObservation(currentSteeringAngle / maxSteeringAngle); // Normalized steering angle
        sensor.AddObservation(rb.velocity.x);
        sensor.AddObservation(rb.velocity.z);

        // Add observation: distance from the center of the road
        float distanceFromCenter = transform.localPosition.x; // Assuming the road is centered at X = 0
        sensor.AddObservation(distanceFromCenter);

        // Add raycast observations
        for (int i = 0; i < raycastDistances.Length; i++)
        {
            float rayDistance = raycastDistances[i];
            float rayAngle = raycastAngles[i];
            Vector3 rayDirection = transform.localRotation * Quaternion.Euler(0, rayAngle, 0) * Vector3.forward;
            bool hitWall = Physics.Raycast(transform.position, rayDirection, out RaycastHit hit, rayDistance);

            if (hitWall && hit.collider.CompareTag("Wall"))
            {
                sensor.AddObservation(hit.distance / rayDistance); // Normalized distance to wall
            }
            else
            {
                sensor.AddObservation(1f); // No wall detected (max distance)
            }
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Get actions: acceleration, steering, and handbrake
        float accelerateInput = actions.ContinuousActions[0]; // Range: [-1, 1]
        float steerInput = actions.ContinuousActions[1]; // Range: [-1, 1]
        float handBrakeInput = actions.ContinuousActions[2]; // Range: [0, 1]

        // Apply acceleration
        if (accelerateInput > 0)
        {
            currentSpeed = Mathf.Min(currentSpeed + acceleration * Time.deltaTime, maxSpeed);
            isReversing = false; // Not reversing if moving forward
        }
        else if (accelerateInput < 0)
        {
            currentSpeed = Mathf.Max(currentSpeed - deceleration * Time.deltaTime, -maxSpeed / 2); // Reverse speed is slower
            isReversing = true; // Reversing if moving backward
        }
        else
        {
            // Natural deceleration
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0, deceleration * Time.deltaTime);
            isReversing = false; // Not reversing if not moving backward
        }

        // Apply handbrake
        if (handBrakeInput > 0.5f)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0, handBrakeDeceleration * Time.deltaTime);
            isReversing = false; // Not reversing if handbrake is applied
        }

        // Apply steering
        float targetSteeringAngle = steerInput * maxSteeringAngle;
        currentSteeringAngle = Mathf.MoveTowards(currentSteeringAngle, targetSteeringAngle, steeringSpeed * Time.deltaTime);

        // Move the car
        Vector3 movement = transform.forward * currentSpeed * Time.deltaTime;
        rb.MovePosition(rb.position + movement);

        // Rotate the car based on steering angle
        if (currentSpeed != 0)
        {
            float turnAngle = currentSteeringAngle * currentSpeed / maxSpeed * Time.deltaTime;
            Quaternion turnRotation = Quaternion.Euler(0, turnAngle, 0);
            rb.MoveRotation(rb.rotation * turnRotation);
        }

        // Reward for moving forward
        if (movement.magnitude > 0 && !isReversing)
        {
            AddReward(0.1f);
        }

        // Penalty for reversing
        if (isReversing)
        {
            timeReversing += Time.deltaTime; // Increment reversing timer
            if (timeReversing > 0.8f) // Apply penalty after 0.8 seconds
            {
                AddReward(-0.05f); // Small negative reward for reversing too long
            }
        }
        else
        {
            timeReversing = 0f; // Reset reversing timer if not reversing
        }

        // Check if the car is stuck
        if (Vector3.Distance(transform.localPosition, lastPosition) < 0.1f)
        {
            timeInSamePosition += Time.deltaTime;
            if (timeInSamePosition >= 3f)
            {
                AddReward(-0.01f); // Small negative reward for being stuck
                timeInSamePosition = 0f; // Reset timer
            }
        }
        else
        {
            timeInSamePosition = -0.05f;
        }

        lastPosition = transform.localPosition;

        // Apply negative rewards based on raycasts
        for (int i = 0; i < raycastDistances.Length; i++)
        {
            float rayDistance = raycastDistances[i];
            float rayAngle = raycastAngles[i];
            Vector3 rayDirection = transform.localRotation * Quaternion.Euler(0, rayAngle, 0) * Vector3.forward;

            if (Physics.Raycast(transform.position, rayDirection, out RaycastHit hit, rayDistance))
            {
                if (hit.collider.CompareTag("Wall"))
                {
                    float normalizedDistance = hit.distance / rayDistance;
                    float rayReward = -0.05f * (1 - normalizedDistance); // Negative reward based on proximity to wall
                    AddReward(rayReward);
                    raycastRewards[i] = rayReward; // Store raycast-specific reward
                }
            }
            else
            {
                raycastRewards[i] = 0.01f; // No reward if no wall is detected
            }
        }

        // Update current total reward
        currentTotalReward = GetCumulativeReward();

        // Check for episode reset conditions
        CheckForEpisodeReset();
    }

    private void CheckForEpisodeReset()
    {
        // Reset if the car strays too far from the center
        float maxDistanceFromCenter = 2f; // Half the width of the road
        if (Mathf.Abs(transform.localPosition.x) > maxDistanceFromCenter)
        {
            AddReward(-1f); // Large negative reward for going off the road
            EndEpisode(); // Reset the episode
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            AddReward(-1f); // Large negative reward for hitting a wall
            EndEpisode(); // Reset the episode
        }
    }

    private void Update()
    {
        // Draw raycasts in the scene view for debugging (if enabled)
        if (showRaycasts)
        {
            for (int i = 0; i < raycastDistances.Length; i++)
            {
                float rayDistance = raycastDistances[i];
                float rayAngle = raycastAngles[i];
                Vector3 rayDirection = transform.localRotation * Quaternion.Euler(0, rayAngle, 0) * Vector3.forward;
                Debug.DrawLine(transform.position, transform.position + rayDirection * rayDistance, Color.red);
            }
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Get the continuous actions from the action buffer
        var continuousActionsOut = actionsOut.ContinuousActions;

        // Map keyboard input to actions
        float accelerateInput = Input.GetAxis("Vertical"); // W/S or Up/Down Arrow for acceleration
        float steerInput = Input.GetAxis("Horizontal"); // A/D or Left/Right Arrow for steering
        float handBrakeInput = Input.GetKey(KeyCode.Space) ? 1f : 0f; // Spacebar for handbrake

        // Assign the inputs to the action buffer
        continuousActionsOut[0] = accelerateInput; // Acceleration
        continuousActionsOut[1] = steerInput; // Steering
        continuousActionsOut[2] = handBrakeInput; // Handbrake
    }
}