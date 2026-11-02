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

    void Start()
    {
        anim = GetComponent<Animator>();
        if (lostScreen != null)
            lostScreen.SetActive(false);
    }

    void Update()
    {
        float angleRotated = transform.eulerAngles.y;
        totalTurnPoint = Mathf.DeltaAngle(startingTurnPoint, angleRotated);

        if (Mathf.Abs(totalTurnPoint) < 2f)
        {
            startingTurnPoint = angleRotated;
            totalTurnPoint = 0f;
        }

        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if(keyboard.qKey.wasPressedThisFrame)
        {
            leftBlinkerOn = true;
            rightBlinkerOn = false;
            turnReached = false;
            anim.Play("LeftBlinker");

            startingTurnPoint = angleRotated;
            totalTurnPoint = 0f;
        }

        if(keyboard.eKey.wasPressedThisFrame)
        {
            rightBlinkerOn = true;
            leftBlinkerOn = false;
            turnReached = false;
            anim.Play( "RightBlinker");

            startingTurnPoint = angleRotated;
            totalTurnPoint = 0f;
        }


        if(keyboard.rKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        if(totalTurnPoint < -20f)
        {
            if(leftBlinkerOn)
            {
                turnReached = true;
            }
            else if(!turnReached)
            {
                ShowLost("Failure to indicate left turn.");
            }
        }
        else if (totalTurnPoint > 20f)
        {
            if(rightBlinkerOn)
            {
                turnReached = true;
            }
            else if (!turnReached)
            {
                ShowLost("Failure to indicate right turn.");
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

    void ShowLost(string reason)
    {
        if (lostScreen != null)
            lostScreen.SetActive(true);
        if (reasonText != null)
            reasonText.text = reason;
    }
}
