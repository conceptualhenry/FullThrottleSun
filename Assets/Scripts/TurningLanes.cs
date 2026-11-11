using FullThrottleSun.Vehicle;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Fail-check for turning without a signal. Blinkers themselves come from
/// <see cref="CarLights"/> (Lever2 / <see cref="LightStalk"/>). Signal presence is
/// latched for the current heading swing so CarLights auto-cancel still counts as indicated.
/// </summary>
[RequireComponent(typeof(CarLights))]
[DefaultExecutionOrder(50)]
public class TurningLanes : MonoBehaviour
{
    [Tooltip("Body yaw degrees (Unity Y, right +) past which a matching turn signal is required.")]
    [SerializeField] float failYawDegrees = 20f;
    [Tooltip("When |yaw| falls below this, the maneuver resets.")]
    [SerializeField] float resetYawDegrees = 5f;

    public TextMeshProUGUI reasonText;
    public GameObject lostScreen;

    CarLights lights;
    float startingYaw;
    float yawDelta;
    bool indicatedLeft;
    bool indicatedRight;
    bool turnOk;
    bool failed;

    void Awake()
    {
        lights = GetComponent<CarLights>();
        startingYaw = transform.eulerAngles.y;
    }

    void Start()
    {
        SetLostVisible(false);
    }

    void Update()
    {
        float yaw = transform.eulerAngles.y;
        yawDelta = Mathf.DeltaAngle(startingYaw, yaw);

        // Drift baseline while nearly straight so small corrections don't accumulate.
        if (Mathf.Abs(yawDelta) < 2f)
        {
            startingYaw = yaw;
            yawDelta = 0f;
        }

        LatchSignals();

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        if (!HasLostUi)
            return;

        float fail = Mathf.Max(1f, failYawDegrees);
        if (yawDelta < -fail)
            EvaluateTurn(left: true);
        else if (yawDelta > fail)
            EvaluateTurn(left: false);

        if (Mathf.Abs(yawDelta) < Mathf.Max(0.5f, resetYawDegrees))
            ResetManeuver(yaw);
    }

    void LatchSignals()
    {
        if (lights == null)
            return;
        if (lights.Turn == CarLights.TurnSignal.Left)
            indicatedLeft = true;
        if (lights.Turn == CarLights.TurnSignal.Right)
            indicatedRight = true;
    }

    void EvaluateTurn(bool left)
    {
        bool indicated = left ? indicatedLeft : indicatedRight;
        if (indicated)
        {
            turnOk = true;
            return;
        }

        if (turnOk || failed)
            return;

        failed = true;
        ShowLost(left
            ? "Failure to indicate left turn."
            : "Failure to indicate right turn.");
    }

    void ResetManeuver(float yaw)
    {
        turnOk = false;
        failed = false;
        indicatedLeft = false;
        indicatedRight = false;
        startingYaw = yaw;
        yawDelta = 0f;
    }

    bool HasLostUi => lostScreen;

    void SetLostVisible(bool visible)
    {
        if (!HasLostUi)
            return;
        lostScreen.SetActive(visible);
    }

    void ShowLost(string reason)
    {
        SetLostVisible(true);
        if (reasonText)
            reasonText.text = reason;
    }
}
