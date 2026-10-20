using UnityEngine;

public class StopSign : MonoBehaviour
{
    private float speedLimit = 0f;

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
            float speed = other.attachedRigidbody.linearVelocity.magnitude;
            Debug.Log("{speed}");

            if(speed > speedLimit)
            {
                Debug.Log("Too fast");
            }
        }
    }
}
