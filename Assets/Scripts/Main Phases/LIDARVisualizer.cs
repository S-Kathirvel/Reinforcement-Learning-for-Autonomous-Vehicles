using UnityEngine;
using Unity.MLAgents;

public class LIDARVisualizer : MonoBehaviour
{
    [Header("References")]
    public CarAgent_P2 carAgent;

    [Header("Visual Settings")]
    public bool showRays = true;
    public Gradient obstacleColorGradient;
    public Color laneHitColor = Color.green;
    public Color laneMissColor = Color.red;
    public float maxVisualDistance = 20f;

    void OnDrawGizmos()
    {
        if (!showRays || carAgent == null) return;

        Vector3 baseOrigin = carAgent.transform.position;
        
        // Draw obstacle detection rays
        if (Application.isPlaying && carAgent.raycastHits.IsCreated)
        {
            for (int i = 0; i < carAgent.rayCount; i++)
            {
                Vector3 direction = Quaternion.Euler(0, i * (360f / carAgent.rayCount), 0) * carAgent.transform.forward;
                float distance = carAgent.raycastHits[i].distance > 0 ? 
                    Mathf.Min(carAgent.raycastHits[i].distance, maxVisualDistance) : 
                    maxVisualDistance;

                Gizmos.color = obstacleColorGradient.Evaluate(distance / maxVisualDistance);
                Gizmos.DrawLine(baseOrigin, baseOrigin + direction * distance);
            }
        }

        // Draw lane detection rays with proper front/back split
        if (Application.isPlaying && carAgent.laneRaycastHits.IsCreated)
        {
            int frontRays = carAgent.laneRayCount / 2;
            int backRays = carAgent.laneRayCount - frontRays;
            Vector3 origin = baseOrigin + Vector3.up * 0.5f;

            // Front rays
            float frontAngleIncrement = carAgent.frontRaySpreadAngle / (frontRays - 1);
            float frontStartAngle = -carAgent.frontRaySpreadAngle / 2;
            
            for (int i = 0; i < frontRays; i++)
            {
                float horizontalAngle = frontStartAngle + i * frontAngleIncrement;
                Vector3 direction = Quaternion.Euler(
                    -carAgent.laneRayDownwardAngle,
                    horizontalAngle, 
                    0
                ) * carAgent.transform.forward;

                DrawLaneRayGizmo(origin, direction, i);
            }

            // Back rays
            float backAngleIncrement = carAgent.backRaySpreadAngle / (backRays - 1);
            float backStartAngle = 180 - carAgent.backRaySpreadAngle / 2;
            
            for (int i = 0; i < backRays; i++)
            {
                float horizontalAngle = backStartAngle + i * backAngleIncrement;
                Vector3 direction = Quaternion.Euler(
                    -carAgent.laneRayDownwardAngle,
                    horizontalAngle, 
                    0
                ) * carAgent.transform.forward;

                DrawLaneRayGizmo(origin, direction, frontRays + i);
            }
        }
    }

    void DrawLaneRayGizmo(Vector3 origin, Vector3 direction, int rayIndex)
    {
        if (rayIndex >= carAgent.laneRaycastHits.Length) return;

        bool isLane = carAgent.laneRaycastHits[rayIndex].collider != null && 
                    carAgent.laneRaycastHits[rayIndex].collider.CompareTag("Lane");
        
        float distance = carAgent.laneRaycastHits[rayIndex].distance > 0 ? 
            Mathf.Min(carAgent.laneRaycastHits[rayIndex].distance, carAgent.laneRayMaxDistance) : 
            carAgent.laneRayMaxDistance;

        Gizmos.color = isLane ? laneHitColor : laneMissColor;
        Gizmos.DrawLine(origin, origin + direction * distance);
    }
}