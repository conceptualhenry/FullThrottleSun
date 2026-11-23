using System;
using System.Collections;
using System.Collections.Generic;
using FMOD.Studio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using static Oculus.Interaction.Context;
using Random = UnityEngine.Random;

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

    [SerializeField] FMODUnity.EventReference Zap;
    [SerializeField] FMODUnity.EventReference Voice;
    [SerializeField] FMODUnity.EventReference Music;

    FMOD.Studio.EventInstance currentPlaying;

    FMOD.Studio.PLAYBACK_STATE state;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentPlaying = FMODUnity.RuntimeManager.CreateInstance(Voice);
        currentPlaying.start();
        currentState = State.Voice;
    }

    

    /*[AOT.MonoPInvokeCallback(typeof(FMOD.Studio.EVENT_CALLBACK))]
    static FMOD.RESULT DialogueEventCallback(FMOD.Studio.EVENT_CALLBACK_TYPE type, IntPtr instancePtr, IntPtr parameterPtr)
    {
        print("callback!");
        
        return FMOD.RESULT.OK;
    }*/

    // Update is called once per frame
    void Update()
    {
        currentPlaying.getPlaybackState(out state);
        if (state == PLAYBACK_STATE.STOPPED)
        {
            currentPlaying = FMODUnity.RuntimeManager.CreateInstance(ChooseNewRadio());
            currentPlaying.start();
        }
    }

 
    FMODUnity.EventReference ChooseNewRadio()
    {

        //if not in transition, enter transition
        if (currentState != State.Transition)
        {
            prevState = currentState;
            currentState = State.Transition;

            return Zap;
        }
        //if coming out of transition, check if it's time for voice or music & switch to whichever
        else if (prevState == State.Voice)
        {
            currentState = State.Music;

            return Music;
        }
        else if (prevState == State.Music) 
        {
            currentState = State.Voice;

            return Voice;
        }
        else
        {
            Debug.Log("radio states broken :(");
            return Zap;
        }
    }

}
