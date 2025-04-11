using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SimpleCarController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 60f;
    public float turnSpeed = 40f;
    public float maxSpeed = 201f;
    
    private Rigidbody _rb;
    private float _verticalInput;
    private float _horizontalInput;

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.centerOfMass = Vector3.zero; // Adjust if needed
    }

    void Update()
    {
        // Get input
        _verticalInput = Input.GetAxis("Vertical");
        _horizontalInput = Input.GetAxis("Horizontal");
    }

    void FixedUpdate()
    {
        // Forward/backward movement
        if (_verticalInput != 0)
        {
            Vector3 force = transform.forward * _verticalInput * speed;
            _rb.AddForce(force, ForceMode.Acceleration);
        }

        // Speed limit
        _rb.velocity = Vector3.ClampMagnitude(_rb.velocity, maxSpeed);

        // Steering
        if (_horizontalInput != 0)
        {
            float turn = _horizontalInput * turnSpeed * Time.fixedDeltaTime;
            Quaternion turnRotation = Quaternion.Euler(0f, turn, 0f);
            _rb.MoveRotation(_rb.rotation * turnRotation);
        }
    }
}