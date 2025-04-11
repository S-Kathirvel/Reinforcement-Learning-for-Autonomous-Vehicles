using UnityEngine;
using Unity.MLAgents;

public class MovingObstacle : MonoBehaviour
{
    private float speed;

    public void Initialize(float moveSpeed)
    {
        speed = moveSpeed;
        Destroy(gameObject, 30f); // Destroy after 30 seconds
    }

    private void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }
}
