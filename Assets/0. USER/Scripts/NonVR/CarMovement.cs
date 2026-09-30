using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class CarMovement : MonoBehaviour
{

    [SerializeField] PlayerInput PI;

    [Header("Looking")]
    [SerializeField] float lookSpeed;
    float rotationX;
    float rotationY;
    [SerializeField] Camera playerCam;
    [SerializeField] float lookXLimit;
    [SerializeField] float lookYLimitLeft;
    [SerializeField] float lookYLimitRight;

    [Header("Driving")]
    [SerializeField] InputActionReference wasd;
    [SerializeField] InputActionReference accel;
    [SerializeField] InputActionReference decel;
    [SerializeField] Rigidbody rb;
    [SerializeField] float carSpeed;
    [SerializeField] float turnSpeed;
    float carVal;
    float turnVal;
    Vector2 moveDirection;

    float accelSpeed;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Cursor.visible = false;
    }

    // Update is called once per frame
    void Update()
    {
        lookUpdate();

        moveDirection = wasd.action.ReadValue<Vector2>();
    }

    public void OnHorn()
    {
        print("HONK");
    }

    public void OnSpeed(InputValue s)
    {
        carVal = s.Get<float>();
    }

    public void OnTurn(InputValue s)
    {
        print(s.Get<float>());
        turnVal = s.Get<float>();
    }

    private void FixedUpdate()
    {
        rb.AddForce(transform.forward * carSpeed * carVal);

        if (turnVal != 0)
        {
            /*
            print(transform.rotation.y);
            Quaternion turnTo = new Quaternion(transform.rotation.x, transform.rotation.y + turnVal, transform.rotation.z, transform.rotation.w);
            print(turnTo.ToString());
            transform.rotation = Quaternion.Slerp(transform.rotation, turnTo, turnSpeed);
            */
            transform.Rotate(new Vector3(0, turnVal * turnSpeed, 0), Space.World);
        }
        
    }

    void lookUpdate()
    {
        Vector2 mousePosition = Mouse.current.position.ReadValue();

        rotationX = Mathf.Clamp(-mousePosition.y * lookSpeed, -lookXLimit, lookXLimit);

        rotationY = Mathf.Clamp(mousePosition.x * lookSpeed, lookYLimitLeft, lookYLimitRight);

        //print(mousePosition.y + ", " + rotationX + ", " + rotationY);

        playerCam.transform.localRotation = Quaternion.Euler(rotationX, rotationY, 0);
    }
}
