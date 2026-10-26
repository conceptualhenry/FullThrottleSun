using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FullThrottleSun.Controller
{
    /// <summary>
    /// Runtime keyboard remapping overlay. Toggle with F1 (or the key set in the inspector).
    /// Bootstrapped automatically so every play session can rebind without placing a prefab.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class KeyboardBindingMenu : MonoBehaviour
    {
        [SerializeField] Key toggleKey = Key.F1;
        [SerializeField] bool visible;

        static KeyboardBindingMenu instance;
        Vector2 scroll;
        GUIStyle titleStyle;
        GUIStyle rowStyle;
        GUIStyle buttonStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null)
                return;
            var go = new GameObject("[KeyboardBindings]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<KeyboardBindingMenu>();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (!KeyboardBindings.IsListening && keyboard[toggleKey].wasPressedThisFrame)
                visible = !visible;

            if (visible && KeyboardBindings.IsListening)
                KeyboardBindings.TickRebind();
        }

        void OnGUI()
        {
            if (!visible)
                return;

            EnsureStyles();

            const float width = 420f;
            float height = Mathf.Min(560f, Screen.height - 40f);
            var rect = new Rect(20f, 20f, width, height);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + 10f, rect.width - 24f, rect.height - 20f));

            GUILayout.Label("键盘绑定 Keyboard Bindings", titleStyle);
            GUILayout.Label($"按 {KeyboardBindings.KeyLabel(toggleKey)} 关闭 · Esc 取消改键", rowStyle);
            GUILayout.Space(6f);

            if (KeyboardBindings.IsListening)
            {
                GUILayout.Label($"按下新按键：{KeyboardBindings.DisplayName(KeyboardBindings.ListeningAction.Value)}", titleStyle);
                if (GUILayout.Button("取消 Cancel", buttonStyle, GUILayout.Height(28f)))
                    KeyboardBindings.CancelRebind();
                GUILayout.EndArea();
                return;
            }

            scroll = GUILayout.BeginScrollView(scroll);
            foreach (KeyboardAction action in Enum.GetValues(typeof(KeyboardAction)))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(KeyboardBindings.DisplayName(action), rowStyle, GUILayout.Width(200f));
                string keyName = KeyboardBindings.KeyLabel(KeyboardBindings.GetKey(action));
                if (GUILayout.Button(keyName, buttonStyle, GUILayout.Width(140f), GUILayout.Height(26f)))
                    KeyboardBindings.BeginRebind(action);
                GUILayout.EndHorizontal();
                GUILayout.Space(2f);
            }
            GUILayout.EndScrollView();

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("恢复默认 Reset Defaults", buttonStyle, GUILayout.Height(30f)))
                KeyboardBindings.ResetToDefaults();
            if (GUILayout.Button("关闭 Close", buttonStyle, GUILayout.Height(30f)))
                visible = false;
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        void EnsureStyles()
        {
            if (titleStyle != null)
                return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            rowStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
            };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 13 };
        }
    }
}
