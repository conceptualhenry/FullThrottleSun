using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EventManager : MonoBehaviour
{
    private Action[] actions;

    public MeshRenderer carMDoorMesh;
    public int elementIndex = 2;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        actions = new Action[] { Phone, Glove, Window };
    }

    // Update is called once per frame
    void Update()
    {
        //Set actions to invoke after a random set of time
        //int rand = UnityEngine.Random.Range(0, actions.Length);
        //actions[rand]?.Invoke();
    }

    void Phone()
    {
        Debug.Log("Phone Ringing");
        //phone rings
        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            //answer
            //Might need to add a cooldown to this function if they answer the call
        }
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            //decline
            //They call repeatedly to annoy you
        }
    }

    void Glove()
    {
        Debug.Log("Glove Box Open!");
    }

    void Window()
    {
        //if the window stays down noise intensifies
        //Car door FL
    }
}
