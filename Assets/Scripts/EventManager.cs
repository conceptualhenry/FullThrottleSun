using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System;

public class EventManager : MonoBehaviour
{
    private Action[] actions;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        actions = new Action[] { Phone, Glove };
    }

    // Update is called once per frame
    void Update()
    {
        //int rand = UnityEngine.Random.Range(0, actions.Length);
        //actions[rand]?.Invoke();
    }

    void Phone()
    {
        Debug.Log("Phone Ringing");
    }

    void Glove()
    {
        Debug.Log("Glove Box Open!");
    }
}
