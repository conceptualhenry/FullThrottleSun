using System.Collections;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SleepTimer : MonoBehaviour
{
    public Slider energySlider;
    public float energyTimer = 10;
    public float maxTime = 10;
    private bool stopTimer = false;

    public TextMeshProUGUI reasonText;
    public GameObject lostScreen;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        energySlider.maxValue = energyTimer;
        energySlider.value = energyTimer;
        StartCoroutine(StartTimerClock());

        lostScreen.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if (!stopTimer && Keyboard.current.shiftKey.wasPressedThisFrame)
        {
            RestartTimer();
        }
    }

    IEnumerator StartTimerClock()
    {
        while(stopTimer == false)
        {
            energyTimer -= Time.deltaTime;
            energyTimer = Mathf.Max(0, energyTimer);

            if (energyTimer <= 0)
            {
                stopTimer = true;
                lostScreen.SetActive(true);
                reasonText.text = "Falling asleep at the wheel";
                yield break;
            }

            if (stopTimer == false)
            {
                energySlider.value = energyTimer;
            }

            yield return null;
        }
    }

    public void RestartTimer()
    {
        energyTimer = maxTime;
        energySlider.value = energyTimer;
    }
}
