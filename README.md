# **Car Agent Setup with Unity ML-Agents**

This project involves training a car agent using Unity ML-Agents to navigate a straight road while avoiding walls. The agent uses raycasts to detect obstacles and receives rewards for staying centered and moving forward. Below is a detailed explanation of the **movement logic**, **agent logic**, **functions**, **observations**, and **overall setup**.

---

## **Table of Contents**
1. [Movement Logic](#movement-logic)
2. [Agent Logic](#agent-logic)
3. [Functions](#functions)
4. [Observations](#observations)
5. [Reward System](#reward-system)
6. [Training Setup](#training-setup)
7. [Testing Setup](#testing-setup)
8. [How to Use](#how-to-use)

---

## **Movement Logic**
The car agent moves based on three actions:
1. **Acceleration**: Controls forward and backward movement.
   - Positive input: Accelerate forward.
   - Negative input: Reverse.
2. **Steering**: Controls left and right movement.
   - Positive input: Steer right.
   - Negative input: Steer left.
3. **Handbrake**: Stops the car abruptly.

The car’s movement is physics-based, using Unity’s `Rigidbody` component. The steering angle is clamped between `-25` and `25` degrees to prevent unrealistic turns.

---

## **Agent Logic**
The agent uses **raycasts** to detect walls and obstacles. It receives observations about its environment and takes actions based on the trained model or heuristic input.

### **Key Components**
1. **Raycasts**:
   - Distances: `{ 6f, 6f, 4.6f, 4.6f }`
   - Angles: `{ -30f, 30f, -90f, 90f }`
   - Detects walls and calculates rewards based on proximity.

2. **Observations**:
   - Speed, steering angle, velocity, and raycast distances.

3. **Rewards**:
   - Positive for moving forward.
   - Negative for being near walls or hitting obstacles.
   - Small penalty for staying stuck.

---

## **Functions**
Here’s a breakdown of the key functions in the `CarAgent` script:

### **1. `Initialize()`**
- Initializes the agent’s starting position, rotation, and raycast rewards array.

### **2. `OnEpisodeBegin()`**
- Resets the agent’s position, velocity, and rewards at the start of each episode.

### **3. `CollectObservations(VectorSensor sensor)`**
- Collects observations for the agent:
  - Normalized speed and steering angle.
  - Velocity (x and z components).
  - Distance from the center of the road.
  - Raycast distances to walls.

### **4. `OnActionReceived(ActionBuffers actions)`**
- Processes actions (acceleration, steering, handbrake).
- Moves the car using `Rigidbody`.
- Applies rewards based on movement and raycast collisions.

### **5. `Heuristic(in ActionBuffers actionsOut)`**
- Maps keyboard input to actions for manual control:
  - **W/S or Up/Down Arrow**: Acceleration.
  - **A/D or Left/Right Arrow**: Steering.
  - **Spacebar**: Handbrake.

### **6. `CheckForEpisodeReset()`**
- Resets the episode if the car goes off the road.

### **7. `OnCollisionEnter(Collision collision)`**
- Resets the episode if the car collides with a wall.

### **8. `Update()`**
- Draws raycasts in the Scene view for debugging.

---

## **Observations**
The agent observes the following:
1. **Speed**: Normalized current speed.
2. **Steering Angle**: Normalized current steering angle.
3. **Velocity**: X and Z components of the car’s velocity.
4. **Distance from Center**: Distance from the center of the road.
5. **Raycast Distances**: Normalized distances to walls detected by raycasts.

---

## **Reward System**
The agent receives rewards based on its actions and environment:
1. **Positive Rewards**:
   - `+0.01` for moving forward.
2. **Negative Rewards**:
   - `-0.1 * (1 - normalizedDistance)` for being near a wall.
   - `-1.0` for hitting a wall or going off the road.
   - `-0.01` for staying stuck (no movement for 3 seconds).

---

## **Training Setup**
1. **Environment**:
   - Straight road with walls on both sides.
   - Agent starts at the beginning of the road.

2. **YAML Configuration**:
   ```yaml
   behaviors:
     CarBehavior:
       trainer_type: ppo
       hyperparameters:
         batch_size: 64
         buffer_size: 1024
         learning_rate: 3.0e-4
         beta: 5.0e-4
         epsilon: 0.2
         lambd: 0.95
         num_epoch: 3
         learning_rate_schedule: linear
       network_settings:
         normalize: true
         hidden_units: 128
         num_layers: 2
       reward_signals:
         extrinsic:
           gamma: 0.99
           strength: 1.0
       max_steps: 500000
       time_horizon: 64
       summary_freq: 10000
   ```

3. **Training Command**:
   ```bash
   mlagents-learn car_config.yaml --run-id=CarTraining --force
   ```

---

## **Testing Setup**
1. **Environment**:
   - Longer road (20 units) with narrower width (4 units).
   - Obstacles placed randomly on the road.

2. **Behavior Parameters**:
   - Assign the trained `.onnx` file.
   - Set **Behavior Type** to `Inference Only`.

3. **Evaluation Metrics**:
   - Success rate, collision rate, and average reward.

---

## **How to Use**
1. **Training**:
   - Set up the training environment.
   - Run the training command and press Play in Unity.

2. **Testing**:
   - Set up the testing environment.
   - Assign the trained model and observe the agent’s performance.

3. **Manual Control**:
   - Set **Behavior Type** to `Heuristic Only`.
   - Use the keyboard to control the car.

---

## **Future Improvements**
1. Add curved roads and dynamic obstacles.
2. Train with multiple environments for better generalization.
3. Use Cinemachine for better visualization during testing.

---

