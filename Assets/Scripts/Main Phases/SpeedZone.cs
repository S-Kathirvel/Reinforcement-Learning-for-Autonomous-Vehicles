using UnityEngine;
using Unity.MLAgents;

public class SpeedZone : MonoBehaviour
{
    [Header("Speed Settings")]
    public float targetSpeed = 20f;
    [Range(0.5f, 2f)] 
    public float speedTolerance = 1.2f;

    [Header("Visualization")]
    public Color zoneColor = new Color(0.2f, 0.8f, 0.2f, 0.3f);

    void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        if (col is BoxCollider box)
        {
            Gizmos.color = zoneColor;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}