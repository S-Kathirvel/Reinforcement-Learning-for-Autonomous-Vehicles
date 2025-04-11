// using UnityEngine;
// using Unity.MLAgents;

// public class SpawnCurriculumManager : MonoBehaviour
// {
//     [Header("Spawner References")]
//     public CarSpawner[] vehicleSpawners; // Array for multiple spawners
//     public MovingObstacleSpawner obstacleSpawner;

//     void Start()
//     {
//         if (Academy.IsInitialized)
//         {
//             Academy.Instance.OnEnvironmentReset += UpdateCurriculum;
//             UpdateCurriculum();
//         }
//     }

//     void UpdateCurriculum()
//     {
//         if (!Academy.IsInitialized) return;

//         var envParams = Academy.Instance.EnvironmentParameters;
        
//         // Apply to all vehicle spawner
        
//         // Obstacle control
//         obstacleSpawner.SetSpawnRate(envParams.GetWithDefault("obstacle_spawn_rate", 1f));
//         obstacleSpawner.SetSpeedMultiplier(envParams.GetWithDefault("obstacle_speed_multiplier", 1f));
//     }

//     void OnDestroy()
//     {
//         if (Academy.IsInitialized)
//         {
//             Academy.Instance.OnEnvironmentReset -= UpdateCurriculum;
//         }
//     }
// }