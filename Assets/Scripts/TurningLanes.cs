using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TurningLanes : MonoBehaviour
{
    private float startingTurnPoint;
    private bool trackingTurn = false;
    private float totalTurnPoint = 0f;
    private bool turnReached = false;

    public Animator anim;

    private bool rightBlinkerOn = false;
    private bool leftBlinkerOn = false;

    public TextMeshProUGUI reasonText;
    public GameObject lostScreen;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        anim = GetComponent<Animator>();
        lostScreen.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        float angleRotated = transform.eulerAngles.y;
        totalTurnPoint = Mathf.DeltaAngle(startingTurnPoint, angleRotated);

        if (Mathf.Abs(totalTurnPoint) < 2f)
        {
            startingTurnPoint = angleRotated;
            totalTurnPoint = 0f;
        }

        Debug.Log($"{totalTurnPoint}");

        if(Keyboard.current.qKey.wasPressedThisFrame)
        {
            leftBlinkerOn = true;
            rightBlinkerOn = false;
            turnReached = false;
            anim.Play("LeftBlinker");

            startingTurnPoint = angleRotated;
            totalTurnPoint = 0f;
        }

        if(Keyboard.current.eKey.wasPressedThisFrame)
        {
            rightBlinkerOn = true;
            leftBlinkerOn = false;
            turnReached = false;
            anim.Play( "RightBlinker");

            startingTurnPoint = angleRotated;
            totalTurnPoint = 0f;
        }


        if(Keyboard.current.rKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        if(totalTurnPoint < -20f)
        {
            if(leftBlinkerOn)
            {
                turnReached = true;
                Debug.Log("Good turn");
            }
            else if(!turnReached)
            {
                lostScreen.SetActive(true);
                reasonText.text = "Failure to indicate left turn.";
                Debug.Log("Illigal turn");
            }
        }
        else if (totalTurnPoint > 20f)
        {
            if(rightBlinkerOn)
            {
                turnReached = true;
                Debug.Log("Good turn");
            }
            else if (!turnReached)
            {
                lostScreen.SetActive(true);
                reasonText.text = "Failure to indicate right turn.";
                Debug.Log("Illegal turn");
            }
        }

        if(turnReached && Mathf.Abs(totalTurnPoint) < 5f)
        {
            leftBlinkerOn = false;
            rightBlinkerOn = false;
            turnReached = false;

            anim.Play("Idle");

            startingTurnPoint = angleRotated;
        }
        else if(Mathf.Abs(Mathf.DeltaAngle(startingTurnPoint, angleRotated)) < 1.5f)
        {
            startingTurnPoint = angleRotated;
        }
        

    }

    /*So press button to indicate
     * Dashboard materials change color and blink
     * the game has to know that it is a turn (so keep track of rotations)
     * A way to point out turning points?
     * 
     */
}
