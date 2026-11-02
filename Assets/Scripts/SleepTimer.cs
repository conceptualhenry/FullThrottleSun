using System.Collections;
using TMPro;
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

    void Start()
    {
        if (energySlider != null)
        {
            energySlider.maxValue = energyTimer;
            energySlider.value = energyTimer;
        }
        StartCoroutine(StartTimerClock());

        if (lostScreen != null)
            lostScreen.SetActive(false);
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (!stopTimer && keyboard != null && keyboard.shiftKey.wasPressedThisFrame)
            RestartTimer();
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
                if (lostScreen != null)
                    lostScreen.SetActive(true);
                if (reasonText != null)
                    reasonText.text = "Falling asleep at the wheel";
                yield break;
            }

            if (stopTimer == false && energySlider != null)
                energySlider.value = energyTimer;

            yield return null;
        }
    }

    public void RestartTimer()
    {
        energyTimer = maxTime;
        energySlider.value = energyTimer;
    }
}
