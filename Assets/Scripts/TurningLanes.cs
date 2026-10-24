using UnityEngine;
using UnityEngine.InputSystem;

public class TurningLanes : MonoBehaviour
{
    //private Quaternion rotation;
    private float yRot;
    public Animator anim;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //rotation = transform.rotation;
        yRot = transform.eulerAngles.y;
        anim = GetComponent<Animator>();
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

        //right turn
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            Debug.Log("E pressed");
            anim.Play("RightBlinker");

            if(deltaAngle > 0.01f)
            {
                Debug.Log("Good turn");
            }

            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                anim.Play("Idle");
            }
        }
        else
        {
            Debug.Log("Illigal turn");
        }

        //left turn
        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            Debug.Log("Q pressed");
            anim.Play("LeftBlinker");

            if (deltaAngle < -0.01f)
            {
                Debug.Log("Good turn");
            }

            if (Keyboard.current.qKey.wasPressedThisFrame)
            {
                anim.Play("Idle");
            }
        }
        else
        {
            Debug.Log("Illigal turn");
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
