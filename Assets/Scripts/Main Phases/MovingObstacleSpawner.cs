using System.Collections;
using UnityEngine;
using Unity.MLAgents;
using System.Collections.Generic;

public class MovingObstacleSpawner : MonoBehaviour
{
    public GameObject obstaclePrefab;
    public Transform agent;
    public List<Transform> roads;

    [Header("Base Settings")]
    public float spawnMinTime = 3f;
    public float spawnMaxTime = 9f;
    public float minSpeed = 5f;
    public float maxSpeed = 15f;

    [Header("Curriculum Parameters")]
    [SerializeField] private float currentSpawnRate = 1f;
    [SerializeField] private float currentSpeedMultiplier = 1f;

    [Header("Spawner Intensity")]
    [Range(0f, 1f)] public float spawnIntensity = 1f;

    private float roadMinX = -50f;
    private float roadMaxX = -10f;
    private float spawnY = 2f;

    private void Start()
    {
        InitializeRoadBounds();
        SetSpawnRate(1f);
        SetSpeedMultiplier(1.5f);
        StartCoroutine(SpawnObstacle());
    }

    public void SetSpawnRate(float rate)
    {
        currentSpawnRate = Mathf.Clamp01(rate);
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        currentSpeedMultiplier = Mathf.Clamp(multiplier, 0.5f, 2f);
    }

    private void InitializeRoadBounds()
    {
        if (roads == null || roads.Count == 0) return;

        roadMinX = float.MaxValue;
        roadMaxX = float.MinValue;

        foreach (var road in roads)
        {
            Renderer r = road.GetComponent<Renderer>();
            if (r)
            {
                Bounds b = r.bounds;
                roadMinX = Mathf.Min(roadMinX, b.min.x);
                roadMaxX = Mathf.Max(roadMaxX, b.max.x);
            }
        }
    }

    private IEnumerator SpawnObstacle()
{
    while (true)
    {
        if (ShouldSpawn())
        {
            int maxSpawn = Mathf.RoundToInt(Mathf.Lerp(0f, 3f, spawnIntensity));
            int spawnCount = Random.Range(0, maxSpawn + 1); // 0 to maxSpawn inclusive

            for (int i = 0; i < spawnCount; i++)
            {
                SpawnSingleObstacle();
            }
        }

        yield return new WaitForSeconds(CalculateWaitTime());
    }
}


    private bool ShouldSpawn()
    {
        return spawnIntensity > 0.01f &&
               Random.value < spawnIntensity &&
               agent != null;
    }

    private void SpawnSingleObstacle()
    {
        Vector3 spawnPos = calculateSpawnPosition();
        GameObject obstacle = Instantiate(
            obstaclePrefab,
            spawnPos,
            Quaternion.identity,
            transform
        );

        InitializeObstacle(obstacle);
        Destroy(obstacle,30f); // Destroy after 30 seconds to prevent memory leaks
    }
    private void OnTriggerEnter(Collider other)
    {
    if (other.CompareTag("Finish"))
    {
        Destroy(gameObject);
    }
    }


    private Vector3 calculateSpawnPosition()
    {
        if (agent == null) return Vector3.zero;

        float minZ = agent.position.z + 60f;
        float maxZ = minZ + 80f;

        float spawnX;
        int safeAttempts = 0;
        do
        {
            spawnX = Random.Range(roadMinX, roadMaxX);
            safeAttempts++;
        } while (Mathf.Abs(spawnX - agent.position.x) < 2f && safeAttempts < 5);

        return new Vector3(
            spawnX,
            spawnY,
            Random.Range(minZ, maxZ)
        );
    }

    private void InitializeObstacle(GameObject obstacle)
    {
        MovingObstacle script = obstacle.GetComponent<MovingObstacle>() ??
                                obstacle.AddComponent<MovingObstacle>();

        float speed = Random.Range(minSpeed, maxSpeed) * currentSpeedMultiplier;
        script.Initialize(speed);
    }

    private float CalculateWaitTime()
    {
        float intensityFactor = Mathf.Lerp(2f, 0.35f, spawnIntensity);
        return Random.Range(spawnMinTime, spawnMaxTime) * intensityFactor;
    }
}
