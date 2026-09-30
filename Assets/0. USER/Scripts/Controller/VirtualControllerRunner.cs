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

        IControllerSource source;

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
            var actions = Resources.Load<InputActionAsset>(ActionsResourcePath);
            if (actions == null)
            {
                Debug.LogError($"[VirtualController] Missing Resources/{ActionsResourcePath}.inputactions");
                enabled = false;
                return;
            }

            source = new GamepadControllerSource(actions);
            VirtualController.SetSource(source);
        }

        void OnEnable()
        {
            source?.Enable();
            DualSenseMotion.Start();
        }

        void OnDisable()
        {
            source?.Disable();
            DualSenseMotion.Stop();
        }

        void Update() => VirtualController.Tick();

        void OnDestroy() => VirtualController.SetSource(null);
    }
}
