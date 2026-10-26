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
            EditorGUILayout.LabelField("Keyboard bindings (shared PlayerPrefs with Play mode)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Defaults: WASD drive, arrows look, Space handbrake, C camera, IJKL lights, Esc menu.\n" +
                "In Play mode, Esc or F1 opens the Settings remapping menu.",
                MessageType.Info);

            if (listening.HasValue)
            {
                EditorGUILayout.HelpBox($"Press a new key for: {KeyboardBindings.DisplayName(listening.Value)} (Esc to cancel)", MessageType.Warning);
                if (GUILayout.Button("Cancel"))
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
            if (GUILayout.Button("Reset Defaults"))
                KeyboardBindings.ResetToDefaults();
        }
    }
}
