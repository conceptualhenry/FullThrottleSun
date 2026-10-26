using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

public class TriggerEvents : MonoBehaviour
{
    [Header("Events")]
    public UnityEvent onTriggerEnter = new UnityEvent();
    public UnityEvent onTriggerExit = new UnityEvent();

    private float speedLimit = 0f;
    public GameObject playerObj;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            //onTriggerEnter.SpeedCheck();
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            onTriggerExit.Invoke();
        }
    }

    void SpeedCheck()
    {
        Debug.Log("Speed Check");
        //float speed = other.attachedRigidbody.linearVelocity.magnitude;
        Rigidbody rb = playerObj.GetComponent<Rigidbody>();
        float speed = rb.linearVelocity.magnitude;
        Debug.Log($"{speed}");

        if (speed > speedLimit)
        {
            Debug.Log("Too fast!");
        }
        else
        {
            Debug.Log("You're good!");
            GetComponent<Collider>().enabled = false;
        }
    }
}
