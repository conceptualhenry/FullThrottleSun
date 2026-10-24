using UnityEngine;

public class StopSign : MonoBehaviour
{
    private float speedLimit = 1f;
    public GameObject theLine;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
       // Debug.Log("start");   
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        //Debug.Log("detect");
        if(other.CompareTag("Player"))
        {
            Debug.Log("I'm here");
            float speed = other.attachedRigidbody.linearVelocity.magnitude;
            Debug.Log($"{speed}");

            if(speed > speedLimit)
            {
               Debug.Log("Too fast!");
            }
            else
            {
                Debug.Log("You're good!");
                GetComponent<Collider>().enabled = false;
                theLine.SetActive(false);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if(other.CompareTag("Player"))
        { 
            GetComponent<Collider>().enabled = true;
            Debug.Log("Left the event");
            theLine.SetActive(true);
        }
    }
}
