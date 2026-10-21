using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;

public class RadioPlayer : MonoBehaviour
{
    //we're gonna have to create some struct system for the radio stations themselves
    //so then during transitions we can say currentstation.voicelines etc etc

    enum State
    {
        Transition, Voice, Music
    }

    State currentState;
    State prevState;

    AudioSource source;

    [Header("Defaults")]

    [SerializeField] List<AudioClip> Transitions = new List<AudioClip>();

    [Header("106.9: The Drive")]

    [SerializeField] List<AudioClip> TheDriveVoice = new List<AudioClip>();
    [SerializeField] List<AudioClip> TheDriveMusic = new List<AudioClip>();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        source = GetComponent<AudioSource>();
        source.clip = TheDriveVoice[0];
        currentState = State.Voice;
        source.Play();
        StartCoroutine(PlayRadio());
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    //this code modified from https://gamedev.stackexchange.com/questions/169339/efficient-way-to-detect-when-audio-clip-ends

    private IEnumerator PlayRadio()
    {
        var waitForClipRemainingTime = new WaitForSeconds((source.clip.length - source.time));
        yield return waitForClipRemainingTime;
        ChooseNewRadio();
    }

    void ChooseNewRadio()
    {
        //if not in transition, enter transition
        if (currentState != State.Transition)
        {
            prevState = currentState;
            currentState = State.Transition;

            source.clip = Transitions[Random.Range(0, Transitions.Count)];
        }
        //if coming out of transition, check if it's time for voice or music & switch to whichever
        else if (prevState == State.Voice)
        {
            source.clip = TheDriveMusic[Random.Range(0, TheDriveMusic.Count)];
            currentState = State.Music;
        }
        else if (prevState == State.Music) 
        {
            source.clip = TheDriveVoice[Random.Range(0, TheDriveVoice.Count)];
            currentState = State.Voice;
        }
        else
        {
            Debug.Log("radio states broken :(");
        }


        source.Play();
        StartCoroutine(PlayRadio());

    }

}
