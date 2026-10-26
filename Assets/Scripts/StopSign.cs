using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class StopSign : MonoBehaviour
{
    private float speedLimit = 1f;
    public GameObject theLine;

    public TextMeshProUGUI reasonText;
    public Canvas lostScreen;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
       // Debug.Log("start");   
    }

    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
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
                reasonText.text = "Failure to stop.";
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
