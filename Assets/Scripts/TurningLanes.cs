using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.SceneManagement;

public class TurningLanes : MonoBehaviour
{
    //private Quaternion rotation;
    private float yRot;
    public Animator anim;

    private bool rightBlinkerOn = false;
    private bool leftBlinkerOn = false;

    public TextMeshProUGUI reasonText;
    public Canvas lostScreen;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //rotation = transform.rotation;
        yRot = transform.eulerAngles.y;
        anim = GetComponent<Animator>();
        lostScreen.enabled = false;
    }

    // Update is called once per frame
    void Update()
    {
        //float angleRotated = Quaternion.Angle(rotation, transform.rotation);
        float angleRotated = transform.eulerAngles.y;
        float deltaAngle = Mathf.DeltaAngle(yRot, angleRotated);

        /*if(angleRotated > 0.01f)
        {
            Debug.Log($"Rotated {angleRotated} degrees");
        }

        float currentY = transform.eulerAngles.y;
        Debug.Log($"Angle {currentY} degrees");
        */

        if(Keyboard.current.qKey.wasPressedThisFrame)
        {
            leftBlinkerOn = !leftBlinkerOn;
            rightBlinkerOn = false;
            anim.Play(leftBlinkerOn ? "LeftBlinker" : "Idle");
        }

        if(Keyboard.current.eKey.wasPressedThisFrame)
        {
            rightBlinkerOn = !rightBlinkerOn;
            leftBlinkerOn = false;
            anim.Play(rightBlinkerOn ? "RightBlinker" : "Idle");
        }
        if(Keyboard.current.rKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        if(deltaAngle < -0.01f)
        {
            if(leftBlinkerOn)
            {
                Debug.Log("Good turn");
            }
            else
            {
                lostScreen.enabled = true;
                reasonText.text = "Failure to indicate turn.";
                Debug.Log("Illigal turn");
            }
        }
        else if (deltaAngle > 0.01f)
        {
            if(rightBlinkerOn)
            {
                Debug.Log("Good turn");
            }
            else
            {
                lostScreen.enabled = true;
                reasonText.text = "Failure to indicate turn.";
                Debug.Log("Illegal turn");
            }
        }

        yRot = angleRotated;

    }

    /*So press button to indicate
     * Dashboard materials change color and blink
     * the game has to know that it is a turn (so keep track of rotations)
     * A way to point out turning points?
     * 
     */
}
