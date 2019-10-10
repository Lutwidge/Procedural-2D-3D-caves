using UnityEngine;

public class Player : MonoBehaviour
{
    private Rigidbody _rb;
    private Vector3 _velocity;
    private bool _jump;
    [SerializeField] private float _speedFactor = 10;
    [SerializeField] private Vector3 _jumpForce;

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        _velocity = new Vector3(-Input.GetAxis("Horizontal"), 0, -Input.GetAxis("Vertical")).normalized * _speedFactor;
        if (Input.GetButtonDown("Jump"))
        {
            _rb.AddForce(_jumpForce, ForceMode.Impulse);
        }
    }

    void FixedUpdate()
    {
        _rb.MovePosition(_rb.position + _velocity * Time.fixedDeltaTime);  
    }
}
