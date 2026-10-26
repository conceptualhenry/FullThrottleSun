using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FullThrottleSun.Controller
{
    /// <summary>
    /// In-game Settings overlay. Open with the Menu binding (Esc by default) or F1.
    /// Controls page remaps <see cref="KeyboardBindings"/>. Bootstrapped automatically.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class SettingsMenu : MonoBehaviour
    {
        static readonly (string title, KeyboardAction[] actions)[] ControlGroups =
        {
            ("Driving", new[]
            {
                KeyboardAction.Throttle,
                KeyboardAction.Brake,
                KeyboardAction.SteerLeft,
                KeyboardAction.SteerRight,
                KeyboardAction.Handbrake
            }),
            ("Camera", new[]
            {
                KeyboardAction.LookLeft,
                KeyboardAction.LookRight,
                KeyboardAction.LookUp,
                KeyboardAction.LookDown,
                KeyboardAction.CameraSwitch
            }),
            ("Lights", new[]
            {
                KeyboardAction.LightLeft,
                KeyboardAction.LightRight,
                KeyboardAction.LightUp,
                KeyboardAction.LightDown
            }),
            ("System", new[]
            {
                KeyboardAction.Menu,
                KeyboardAction.Secondary,
                KeyboardAction.GripLeft,
                KeyboardAction.GripRight
            })
        };

        static SettingsMenu instance;

        [SerializeField] Key alternateToggleKey = Key.F1;

        bool open;
        Vector2 scroll;
        float savedTimeScale = 1f;
        CursorLockMode savedLockMode;
        bool savedCursorVisible;

        GUIStyle titleStyle;
        GUIStyle sectionStyle;
        GUIStyle rowStyle;
        GUIStyle buttonStyle;
        GUIStyle hintStyle;
        Texture2D panelTex;
        Texture2D dimTex;

        public static bool IsOpen => instance != null && instance.open;

        public static void Show()
        {
            EnsureInstance();
            instance.SetOpen(true);
        }

        public static void Hide()
        {
            if (instance == null)
                return;
            instance.SetOpen(false);
        }

        public static void Toggle()
        {
            EnsureInstance();
            instance.SetOpen(!instance.open);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap() => EnsureInstance();

        static void EnsureInstance()
        {
            if (instance != null)
                return;
            var go = new GameObject("[SettingsMenu]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SettingsMenu>();
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                if (open)
                    RestorePlayState();
                instance = null;
            }
            if (panelTex != null)
                Destroy(panelTex);
            if (dimTex != null)
                Destroy(dimTex);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (open && KeyboardBindings.IsListening)
            {
                KeyboardBindings.TickRebind();
                return;
            }

            bool menuPressed = KeyboardBindings.WasPressedThisFrame(KeyboardAction.Menu);
            bool altPressed = keyboard[alternateToggleKey].wasPressedThisFrame;
            if (menuPressed || altPressed)
                SetOpen(!open);
        }

        void SetOpen(bool value)
        {
            if (open == value)
                return;

            if (value)
            {
                KeyboardBindings.CancelRebind();
                savedTimeScale = Time.timeScale;
                savedLockMode = Cursor.lockState;
                savedCursorVisible = Cursor.visible;
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                open = true;
            }
            else
            {
                KeyboardBindings.CancelRebind();
                RestorePlayState();
                open = false;
            }
        }

        void RestorePlayState()
        {
            Time.timeScale = savedTimeScale > 0f ? savedTimeScale : 1f;
            Cursor.lockState = savedLockMode;
            Cursor.visible = savedCursorVisible;
        }

        void OnGUI()
        {
            if (!open)
                return;

            EnsureStyles();

            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), dimTex);

            float width = Mathf.Min(520f, Screen.width - 40f);
            float height = Mathf.Min(640f, Screen.height - 40f);
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.DrawTexture(rect, panelTex);

            GUILayout.BeginArea(new Rect(rect.x + 20f, rect.y + 16f, rect.width - 40f, rect.height - 32f));

            GUILayout.Label("Settings", titleStyle);
            GUILayout.Label(
                $"Press {KeyboardBindings.KeyLabel(KeyboardBindings.GetKey(KeyboardAction.Menu))} or {KeyboardBindings.KeyLabel(alternateToggleKey)} to close",
                hintStyle);
            GUILayout.Space(10f);

            if (KeyboardBindings.IsListening)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Press a new key…", sectionStyle);
                GUILayout.Label(KeyboardBindings.DisplayName(KeyboardBindings.ListeningAction.Value), titleStyle);
                GUILayout.Space(8f);
                GUILayout.Label("Esc to cancel", hintStyle);
                GUILayout.Space(12f);
                if (GUILayout.Button("Cancel", buttonStyle, GUILayout.Height(36f)))
                    KeyboardBindings.CancelRebind();
                GUILayout.FlexibleSpace();
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label("Controls", sectionStyle);
            GUILayout.Space(4f);

            scroll = GUILayout.BeginScrollView(scroll);
            for (int g = 0; g < ControlGroups.Length; g++)
            {
                var group = ControlGroups[g];
                GUILayout.Label(group.title, sectionStyle);
                GUILayout.Space(2f);
                for (int i = 0; i < group.actions.Length; i++)
                {
                    var action = group.actions[i];
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(KeyboardBindings.DisplayName(action), rowStyle, GUILayout.Width(220f));
                    string keyName = KeyboardBindings.KeyLabel(KeyboardBindings.GetKey(action));
                    if (GUILayout.Button(keyName, buttonStyle, GUILayout.Width(150f), GUILayout.Height(28f)))
                        KeyboardBindings.BeginRebind(action);
                    GUILayout.EndHorizontal();
                    GUILayout.Space(3f);
                }
                GUILayout.Space(10f);
            }
            GUILayout.EndScrollView();

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Defaults", buttonStyle, GUILayout.Height(34f)))
                KeyboardBindings.ResetToDefaults();
            if (GUILayout.Button("Resume", buttonStyle, GUILayout.Height(34f)))
                SetOpen(false);
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        void EnsureStyles()
        {
            if (titleStyle != null)
                return;

            panelTex = MakeTex(new Color(0.08f, 0.09f, 0.11f, 0.96f));
            dimTex = MakeTex(new Color(0f, 0f, 0f, 0.55f));

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.75f, 0.85f, 1f) }
            };
            rowStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.92f, 0.92f, 0.92f) }
            };
            hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.65f, 0.65f, 0.65f) }
            };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 13 };
        }

        static Texture2D MakeTex(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point
            };
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }
    }
}
