using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class Blindness : MonoBehaviour
{
    [SerializeField] Camera cam;

    [SerializeField] float increaseRate = 1.1f;
    [SerializeField] float decreaseRate = .9f;
    [SerializeField] float waitTime = 5f;
    [SerializeField] float sizeMin = .05f;
    [SerializeField] float sizeMax = 50f;
    GameObject image;

    bool shrinkingOn = false;
    bool restartAllowed = true;

    //START HERE TODO HENRY THIS IS WHAT YOU'RE WORKING ON https://discussions.unity.com/t/stopcoroutine-is-not-stopping-my-coroutines/134523
    Coroutine blindnessTimer;

    LayerMask sunLayerMask;
    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        image = this.gameObject;
        sunLayerMask = LayerMask.GetMask("Sun");
        blindnessTimer = StartCoroutine(blindnessWait());
    }

    // Update is called once per frame
    void Update()
    {

        RaycastHit hit;

        float newScale = 1;

        //raycast and see if it hits anything on the sun layer (the sun)
        if (Physics.Raycast(cam.transform.position, cam.transform.TransformDirection(Vector3.forward), out hit, 20, sunLayerMask) && image.transform.localScale.y < sizeMax)
        {
            //scale up if so
            newScale = image.transform.localScale.y + (increaseRate/100);
            image.transform.localScale = new Vector3(newScale * 2, newScale, 1);
            if (restartAllowed)
            {
                StopCoroutine(blindnessTimer);
                print("coroutine SHOULD have stoped");
                restartAllowed = false;
                StartCoroutine(restartTimer());
            }    
        }

        //scale down otherwise (unless already at min size)
        else if (image.transform.localScale.y > sizeMin && shrinkingOn)
        {
            newScale = image.transform.localScale.y - (decreaseRate/100);
            image.transform.localScale = new Vector3(newScale * 2, newScale, 1);
        }
        
    }

    IEnumerator blindnessWait()
    {
        shrinkingOn = false;
        print("wait started");
        yield return new WaitForSeconds(waitTime);
        print("wait ended");
        shrinkingOn = true;
    }

    IEnumerator restartTimer()
    {
        yield return new WaitForSeconds(.25f);
        restartAllowed = true;
        blindnessTimer = StartCoroutine(blindnessWait());
    }
}
