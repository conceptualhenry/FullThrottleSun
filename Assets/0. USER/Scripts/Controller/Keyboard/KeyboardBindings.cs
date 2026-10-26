using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FullThrottleSun.Controller
{
    /// <summary>
    /// Remappable keyboard bindings. Defaults ship in code; player overrides live in PlayerPrefs.
    /// Pressed state is read through the Input System <see cref="Keyboard"/>.
    /// </summary>
    public static class KeyboardBindings
    {
        const string PrefsKey = "FullThrottleSun.KeyboardBindings.v1";

        static readonly Dictionary<KeyboardAction, Key> map = new Dictionary<KeyboardAction, Key>();
        static bool loaded;
        static KeyboardAction? listening;

        public static event Action Changed;
        /// <summary>Fired when a rebind capture starts or finishes (argument is the action, or null when cancelled / done).</summary>
        public static event Action<KeyboardAction?> RebindStateChanged;

        public static bool IsListening => listening.HasValue;
        public static KeyboardAction? ListeningAction => listening;

        public static IReadOnlyDictionary<KeyboardAction, Key> Defaults { get; } = new Dictionary<KeyboardAction, Key>
        {
            { KeyboardAction.Throttle, Key.W },
            { KeyboardAction.Brake, Key.S },
            { KeyboardAction.SteerLeft, Key.A },
            { KeyboardAction.SteerRight, Key.D },
            { KeyboardAction.Handbrake, Key.Space },
            { KeyboardAction.LookLeft, Key.LeftArrow },
            { KeyboardAction.LookRight, Key.RightArrow },
            { KeyboardAction.LookUp, Key.UpArrow },
            { KeyboardAction.LookDown, Key.DownArrow },
            { KeyboardAction.CameraSwitch, Key.C },
            { KeyboardAction.LightLeft, Key.J },
            { KeyboardAction.LightRight, Key.L },
            { KeyboardAction.LightUp, Key.I },
            { KeyboardAction.LightDown, Key.K },
            { KeyboardAction.Menu, Key.Escape },
            { KeyboardAction.Secondary, Key.LeftShift },
            { KeyboardAction.GripLeft, Key.Q },
            { KeyboardAction.GripRight, Key.E }
        };

        public static string DisplayName(KeyboardAction action)
        {
            switch (action)
            {
                case KeyboardAction.Throttle: return "油门 Throttle";
                case KeyboardAction.Brake: return "刹车 Brake";
                case KeyboardAction.SteerLeft: return "左转 Steer Left";
                case KeyboardAction.SteerRight: return "右转 Steer Right";
                case KeyboardAction.Handbrake: return "手刹 Handbrake";
                case KeyboardAction.LookLeft: return "视角左 Look Left";
                case KeyboardAction.LookRight: return "视角右 Look Right";
                case KeyboardAction.LookUp: return "视角上 Look Up";
                case KeyboardAction.LookDown: return "视角下 Look Down";
                case KeyboardAction.CameraSwitch: return "切换镜头 Camera";
                case KeyboardAction.LightLeft: return "灯光左 Light Left";
                case KeyboardAction.LightRight: return "灯光右 Light Right";
                case KeyboardAction.LightUp: return "灯光上 Light Up";
                case KeyboardAction.LightDown: return "灯光下 Light Down";
                case KeyboardAction.Menu: return "菜单 Menu";
                case KeyboardAction.Secondary: return "次要键 Secondary";
                case KeyboardAction.GripLeft: return "左握 Grip L";
                case KeyboardAction.GripRight: return "右握 Grip R";
                default: return action.ToString();
            }
        }

        public static Key GetKey(KeyboardAction action)
        {
            EnsureLoaded();
            return map.TryGetValue(action, out var key) ? key : Defaults[action];
        }

        public static void SetKey(KeyboardAction action, Key key)
        {
            EnsureLoaded();
            if (key == Key.None)
                return;

            // Drop duplicates so one physical key only drives one action.
            var toClear = new List<KeyboardAction>();
            foreach (var pair in map)
            {
                if (pair.Key != action && pair.Value == key)
                    toClear.Add(pair.Key);
            }
            for (int i = 0; i < toClear.Count; i++)
                map[toClear[i]] = Key.None;

            map[action] = key;
            Save();
            Changed?.Invoke();
        }

        public static void ResetToDefaults()
        {
            map.Clear();
            foreach (var pair in Defaults)
                map[pair.Key] = pair.Value;
            Save();
            CancelRebind();
            Changed?.Invoke();
        }

        public static bool IsPressed(KeyboardAction action)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return false;
            var key = GetKey(action);
            return key != Key.None && keyboard[key].isPressed;
        }

        public static bool WasPressedThisFrame(KeyboardAction action)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return false;
            var key = GetKey(action);
            return key != Key.None && keyboard[key].wasPressedThisFrame;
        }

        /// <summary>−1 when negative is held, +1 when positive is held, 0 if neither / both.</summary>
        public static float GetAxis(KeyboardAction negative, KeyboardAction positive)
        {
            float v = 0f;
            if (IsPressed(negative)) v -= 1f;
            if (IsPressed(positive)) v += 1f;
            return v;
        }

        public static bool AnyBoundKeyPressed()
        {
            EnsureLoaded();
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return false;
            foreach (var pair in map)
            {
                if (pair.Value != Key.None && keyboard[pair.Value].isPressed)
                    return true;
            }
            return false;
        }

        public static void BeginRebind(KeyboardAction action)
        {
            listening = action;
            RebindStateChanged?.Invoke(action);
        }

        public static void CancelRebind()
        {
            if (!listening.HasValue)
                return;
            listening = null;
            RebindStateChanged?.Invoke(null);
        }

        /// <summary>Call once per frame while a rebind is open. Returns true when a key was captured.</summary>
        public static bool TickRebind()
        {
            if (!listening.HasValue)
                return false;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                CancelRebind();
                return false;
            }

            var keys = keyboard.allKeys;
            for (int i = 0; i < keys.Count; i++)
            {
                var control = keys[i];
                if (control == null || !control.wasPressedThisFrame)
                    continue;
                Key key = control.keyCode;
                if (key == Key.None || key == Key.Escape)
                    continue;

                var action = listening.Value;
                listening = null;
                SetKey(action, key);
                RebindStateChanged?.Invoke(null);
                return true;
            }

            return false;
        }

        public static string KeyLabel(Key key)
        {
            if (key == Key.None)
                return "—";
            return key.ToString();
        }

        static void EnsureLoaded()
        {
            if (loaded)
                return;
            loaded = true;
            map.Clear();
            foreach (var pair in Defaults)
                map[pair.Key] = pair.Value;

            string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json))
                return;

            try
            {
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data?.entries == null)
                    return;
                for (int i = 0; i < data.entries.Length; i++)
                {
                    var e = data.entries[i];
                    if (!Enum.TryParse(e.action, out KeyboardAction action))
                        continue;
                    if (!Enum.TryParse(e.key, out Key key))
                        continue;
                    map[action] = key;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KeyboardBindings] Failed to load prefs: {ex.Message}");
            }
        }

        static void Save()
        {
            var entries = new List<SaveEntry>(map.Count);
            foreach (var pair in map)
            {
                entries.Add(new SaveEntry
                {
                    action = pair.Key.ToString(),
                    key = pair.Value.ToString()
                });
            }
            var data = new SaveData { entries = entries.ToArray() };
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            map.Clear();
            loaded = false;
            listening = null;
            Changed = null;
            RebindStateChanged = null;
        }

        [Serializable]
        class SaveData
        {
            public SaveEntry[] entries;
        }

        [Serializable]
        class SaveEntry
        {
            public string action;
            public string key;
        }

        /// <summary>Debug dump of the current map.</summary>
        public static string DebugDump()
        {
            EnsureLoaded();
            var sb = new StringBuilder();
            foreach (KeyboardAction action in Enum.GetValues(typeof(KeyboardAction)))
                sb.Append(action).Append('=').Append(GetKey(action)).Append('\n');
            return sb.ToString();
        }
    }
}
