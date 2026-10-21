using UnityEngine;

public class MoveForward : MonoBehaviour
{
    public float speed = 3f;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        rb.MovePosition(
            rb.position + Vector3.forward * speed * Time.fixedDeltaTime
        );
    }

    void OnCollisionEnter(Collision collision)
    {
        print("11：" + collision.gameObject.name);
    }
}