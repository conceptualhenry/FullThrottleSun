using System;
using UnityEditor;
using UnityEngine;

namespace FullThrottleSun.Controller
{
    /// <summary>
    /// Edit keyboard bindings in the Editor (writes the same PlayerPrefs the game uses).
    /// Open via Tools &gt; Keyboard Bindings.
    /// </summary>
    public class KeyboardBindingsWindow : EditorWindow
    {
        Vector2 scroll;
        KeyboardAction? listening;

        [MenuItem("Tools/Keyboard Bindings")]
        static void Open() => GetWindow<KeyboardBindingsWindow>("Keyboard Bindings");

        void OnEnable() => EditorApplication.update += Tick;
        void OnDisable()
        {
            EditorApplication.update -= Tick;
            listening = null;
            KeyboardBindings.CancelRebind();
        }

        void Tick()
        {
            if (!listening.HasValue)
                return;
            if (KeyboardBindings.TickRebind())
            {
                listening = null;
                Repaint();
            }
            else if (!KeyboardBindings.IsListening)
            {
                listening = null;
                Repaint();
            }
            else
            {
                Repaint();
            }
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("游戏键盘绑定（与 Play 模式共用 PlayerPrefs）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "默认：WASD 驾驶，方向键视角，空格手刹，C 切换镜头，IJKL 灯光，Esc 菜单。\n" +
                "Play 里按 F1 也可打开同样的重绑界面。",
                MessageType.Info);

            if (listening.HasValue)
            {
                EditorGUILayout.HelpBox($"按下新按键以绑定：{KeyboardBindings.DisplayName(listening.Value)}（Esc 取消）", MessageType.Warning);
                if (GUILayout.Button("取消"))
                {
                    KeyboardBindings.CancelRebind();
                    listening = null;
                }
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (KeyboardAction action in Enum.GetValues(typeof(KeyboardAction)))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(KeyboardBindings.DisplayName(action), GUILayout.Width(200f));
                if (GUILayout.Button(KeyboardBindings.KeyLabel(KeyboardBindings.GetKey(action)), GUILayout.Width(120f)))
                {
                    listening = action;
                    KeyboardBindings.BeginRebind(action);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("恢复默认 Reset Defaults"))
                KeyboardBindings.ResetToDefaults();
        }
    }
}
