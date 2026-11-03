using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class TimerManager : MonoBehaviour
{
    public Volume volume;
    public Vignette vignette;
    private float elapsedTime = 0f;
    private bool isFading = false;
    private float startIntensity = 0f;

    [Header("Sleep Settings")]
    public float energyTimer;
    public float maxTime;
    private bool stopTimer = false;
    [SerializeField] private float duration;
    [SerializeField] private float maxIntensity;

    public TextMeshProUGUI reasonText;
    public GameObject lostScreen;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
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

        if(isFading && vignette != null)
        {
            elapsedTime += Time.deltaTime;
            float fadeComplete = elapsedTime / duration;

            vignette.intensity.value = Mathf.Lerp(startIntensity, maxIntensity, fadeComplete);

            if(fadeComplete >= 1f)
            {
                isFading = false;
            }
        }
    }

    private void Awake()
    {
        volume = GetComponent<Volume>();
        if(!volume.profile.TryGet(out vignette))
        {
            Debug.Log("Not found");
        }
    }

    IEnumerator StartTimerClock()
    {
        while (stopTimer == false)
        {
            energyTimer -= Time.deltaTime;
            energyTimer = Mathf.Max(0, energyTimer);

            if(energyTimer < (maxTime / 2f))
            {
                TriggerVignette();
            }

            if (energyTimer <= 0)
            {
                stopTimer = true;
                lostScreen.SetActive(true);
                reasonText.text = "Falling asleep at the wheel";
                yield break;
            }

            if (stopTimer == false)
            {
                //energySlider.value = energyTimer;
            }

            yield return null;
        }
    }

    public void RestartTimer()
    {
        energyTimer = maxTime;
    }

    public void TriggerVignette()
    {
        if (vignette == null) return;

        startIntensity = vignette.intensity.value;
        elapsedTime = 0f;
        isFading = true;
    }
}
