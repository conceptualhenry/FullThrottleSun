using UnityEngine;
using UnityEngine.InputSystem;

namespace FullThrottleSun.Controller
{
    /// <summary>
    /// Created automatically on startup; updates VirtualController once per frame before game scripts.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class VirtualControllerRunner : MonoBehaviour
    {
        const string ActionsResourcePath = "VirtualControllerActions";

        IControllerSource[] sources;
        InputSettings originalSettings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<VirtualControllerRunner>() != null)
                return;

            var go = new GameObject("[VirtualController]");
            DontDestroyOnLoad(go);
            go.AddComponent<VirtualControllerRunner>();
        }

        void Awake()
        {
            KeepDevicesEnabledWhenUnfocused();

            var actions = Resources.Load<InputActionAsset>(ActionsResourcePath);
            if (actions == null)
            {
                Debug.LogError($"[VirtualController] Missing Resources/{ActionsResourcePath}.inputactions");
                enabled = false;
                return;
            }

            sources = new IControllerSource[] { new GamepadControllerSource(actions), new XRControllerSource() };
            VirtualController.SetSources(sources);
        }

        void OnEnable()
        {
            if (sources != null)
                foreach (var source in sources)
                    source.Enable();
            DualSenseMotion.Start();
        }

        void OnDisable()
        {
            if (sources != null)
                foreach (var source in sources)
                    source.Disable();
            DualSenseMotion.Stop();
        }

        void Update() => VirtualController.Tick();

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                return;
            foreach (var device in InputSystem.devices)
            {
                if (device is Gamepad && !device.enabled)
                    InputSystem.EnableDevice(device);
            }
        }

        void OnDestroy()
        {
            VirtualController.SetSources();
            if (originalSettings != null)
                InputSystem.settings = originalSettings;
        }

        /// <summary>
        /// By default the Input System resets and disables gamepads when the app loses focus, and the DualSense
        /// does not always come back afterwards. A runtime copy is used so the project's settings asset is untouched.
        /// </summary>
        void KeepDevicesEnabledWhenUnfocused()
        {
            originalSettings = InputSystem.settings;
            var settings = Instantiate(originalSettings);
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings;
        }
    }
}
