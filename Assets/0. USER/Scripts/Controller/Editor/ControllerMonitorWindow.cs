using UnityEditor;
using UnityEngine;

namespace FullThrottleSun.Controller
{
    /// <summary>
    /// Live view of every VirtualController input while in Play mode. Open via Tools > Controller Monitor.
    /// </summary>
    public class ControllerMonitorWindow : EditorWindow
    {
        const float StickSize = 110f;
        const float StickDeadzone = 0.1f;
        const float ButtonWidth = 56f;
        const float ButtonHeight = 24f;
        const float DpadCell = 26f;
        const float GyroDisplayRange = 500f;
        const float AccelDisplayRange = 2f;

        static readonly Color OnColor = new Color(0.30f, 0.85f, 0.40f);
        static readonly Color OffColor = new Color(0.20f, 0.20f, 0.20f);
        static readonly Color BarColor = new Color(0.30f, 0.60f, 1.00f);
        static readonly Color GuideColor = new Color(1f, 1f, 1f, 0.35f);

        GUIStyle centeredLabel;
        Vector2 scrollPosition;

        [MenuItem("Tools/Controller Monitor")]
        static void Open() => GetWindow<ControllerMonitorWindow>("Controller Monitor");

        void OnEnable() => EditorApplication.update += RepaintWhilePlaying;

        void OnDisable() => EditorApplication.update -= RepaintWhilePlaying;

        void RepaintWhilePlaying()
        {
            if (EditorApplication.isPlaying)
                Repaint();
        }

        void OnGUI()
        {
            centeredLabel ??= new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode to see controller input.", MessageType.Info);
                return;
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(scrollPosition))
            {
                scrollPosition = scroll.scrollPosition;

                string device = VirtualController.IsConnected ? VirtualController.DeviceName : "No gamepad connected";
                EditorGUILayout.LabelField("Device", device, EditorStyles.boldLabel);
                EditorGUILayout.Space();

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawHand(ControllerHand.Left, "X", "Y");
                    DrawHand(ControllerHand.Right, "A", "B");
                }

                EditorGUILayout.Space();
                DrawMotion();
            }
        }

        static void DrawMotion()
        {
            var m = DualSenseMotion.Current;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Motion (DualSense, uncalibrated)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Status", m.status ?? "-");
                EditorGUILayout.LabelField("Connection", m.connection.ToString());
                EditorGUILayout.LabelField("Sample rate", $"{m.sampleRate:0} Hz");

                if (!m.hasMotionData)
                    return;

                EditorGUILayout.Space();
                GUILayout.Label("Gyroscope (°/s)", EditorStyles.miniBoldLabel);
                Vector3 gyro = m.AngularVelocity;
                DrawCenteredBar("X", gyro.x, GyroDisplayRange, m.rawGyro.x);
                DrawCenteredBar("Y", gyro.y, GyroDisplayRange, m.rawGyro.y);
                DrawCenteredBar("Z", gyro.z, GyroDisplayRange, m.rawGyro.z);

                EditorGUILayout.Space();
                GUILayout.Label("Accelerometer (g)", EditorStyles.miniBoldLabel);
                Vector3 accel = m.Acceleration;
                DrawCenteredBar("X", accel.x, AccelDisplayRange, m.rawAccel.x);
                DrawCenteredBar("Y", accel.y, AccelDisplayRange, m.rawAccel.y);
                DrawCenteredBar("Z", accel.z, AccelDisplayRange, m.rawAccel.z);

                EditorGUILayout.LabelField("Sensor timestamp", m.sensorTimestamp.ToString());
            }
        }

        /// <summary>Bar grows left or right from the middle; value is clamped to ±range for display only.</summary>
        static void DrawCenteredBar(string label, float value, float range, int raw)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 18f);
            var labelRect = new Rect(rect.x, rect.y, 20f, rect.height);
            var barRect = new Rect(rect.x + 24f, rect.y + 2f, rect.width - 24f - 150f, rect.height - 4f);
            var valueRect = new Rect(barRect.xMax + 6f, rect.y, 144f, rect.height);

            GUI.Label(labelRect, label);
            EditorGUI.DrawRect(barRect, OffColor);

            float half = barRect.width * 0.5f;
            float fill = Mathf.Clamp(value / range, -1f, 1f) * half;
            float center = barRect.x + half;
            var fillRect = fill >= 0f
                ? new Rect(center, barRect.y, fill, barRect.height)
                : new Rect(center + fill, barRect.y, -fill, barRect.height);
            EditorGUI.DrawRect(fillRect, BarColor);
            EditorGUI.DrawRect(new Rect(center - 1f, barRect.y, 2f, barRect.height), GuideColor);

            GUI.Label(valueRect, $"{value,8:+0.00;-0.00;0.00}  ({raw})");
        }

        void DrawHand(ControllerHand hand, string primaryName, string secondaryName)
        {
            var s = VirtualController.GetState(hand);
            bool isLeft = hand == ControllerHand.Left;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinWidth(260)))
            {
                GUILayout.Label($"{hand} hand", EditorStyles.boldLabel);

                DrawBar("Trigger", s.trigger);
                DrawBar("Grip", s.grip);
                EditorGUILayout.Space();

                DrawStick(s.thumbstick);
                EditorGUILayout.Space();

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawButton(primaryName, s.primary);
                    DrawButton(secondaryName, s.secondary);
                    DrawButton("Stick", s.thumbstickClick);
                    if (isLeft)
                        DrawButton("Menu", s.menu);
                    GUILayout.FlexibleSpace();
                }

                if (isLeft)
                {
                    EditorGUILayout.Space();
                    DrawDpad(s);
                }
            }
        }

        static void DrawBar(string label, float value)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 18f);
            var labelRect = new Rect(rect.x, rect.y, 60f, rect.height);
            var barRect = new Rect(rect.x + 64f, rect.y + 2f, rect.width - 64f - 48f, rect.height - 4f);
            var valueRect = new Rect(barRect.xMax + 6f, rect.y, 42f, rect.height);

            float threshold = VirtualControllerState.PressThreshold;
            bool pressed = value >= threshold;

            GUI.Label(labelRect, label);
            EditorGUI.DrawRect(barRect, OffColor);
            EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, barRect.width * Mathf.Clamp01(value), barRect.height),
                pressed ? OnColor : BarColor);
            EditorGUI.DrawRect(new Rect(barRect.x + barRect.width * threshold - 1f, barRect.y, 2f, barRect.height), GuideColor);
            GUI.Label(valueRect, value.ToString("0.00"));
        }

        void DrawStick(Vector2 value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect rect = GUILayoutUtility.GetRect(StickSize, StickSize, GUILayout.Width(StickSize), GUILayout.Height(StickSize));
                float magnitude = Mathf.Clamp01(value.magnitude);
                bool active = magnitude > StickDeadzone;

                if (Event.current.type == EventType.Repaint)
                {
                    Vector2 center = rect.center;
                    float radius = StickSize * 0.5f - 4f;

                    Handles.color = GuideColor;
                    Handles.DrawWireDisc(center, Vector3.forward, radius);
                    Handles.DrawLine(center + Vector2.left * radius, center + Vector2.right * radius);
                    Handles.DrawLine(center + Vector2.up * radius, center + Vector2.down * radius);

                    Vector2 dot = center + new Vector2(value.x, -value.y) * radius;
                    Handles.color = active ? OnColor : Color.white;
                    Handles.DrawLine(center, dot);
                    Handles.DrawSolidDisc(dot, Vector3.forward, 6f);
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label("Thumbstick", EditorStyles.boldLabel);
                    GUILayout.Label($"X: {value.x:+0.00;-0.00;0.00}");
                    GUILayout.Label($"Y: {value.y:+0.00;-0.00;0.00}");
                    GUILayout.Label($"Magnitude: {magnitude:0.00}");
                    GUILayout.Label(active ? $"Angle: {StickAngle(value):0}°" : "Angle: -");
                }
            }
        }

        /// <summary>0° is straight up, increasing clockwise.</summary>
        static float StickAngle(Vector2 value)
        {
            float angle = Mathf.Atan2(value.x, value.y) * Mathf.Rad2Deg;
            return angle < 0f ? angle + 360f : angle;
        }

        void DrawButton(string label, bool pressed)
        {
            Rect rect = GUILayoutUtility.GetRect(ButtonWidth, ButtonHeight, GUILayout.Width(ButtonWidth), GUILayout.Height(ButtonHeight));
            EditorGUI.DrawRect(rect, pressed ? OnColor : OffColor);
            GUI.Label(rect, label, centeredLabel);
        }

        void DrawDpad(VirtualControllerState s)
        {
            GUILayout.Label("D-pad", EditorStyles.boldLabel);
            Rect area = GUILayoutUtility.GetRect(DpadCell * 3f, DpadCell * 3f, GUILayout.Width(DpadCell * 3f), GUILayout.Height(DpadCell * 3f));

            DrawDpadCell(area, 1, 0, "▲", s.dpadUp);
            DrawDpadCell(area, 0, 1, "◀", s.dpadLeft);
            DrawDpadCell(area, 2, 1, "▶", s.dpadRight);
            DrawDpadCell(area, 1, 2, "▼", s.dpadDown);
        }

        void DrawDpadCell(Rect area, int column, int row, string label, bool pressed)
        {
            var rect = new Rect(area.x + column * DpadCell + 1f, area.y + row * DpadCell + 1f, DpadCell - 2f, DpadCell - 2f);
            EditorGUI.DrawRect(rect, pressed ? OnColor : OffColor);
            GUI.Label(rect, label, centeredLabel);
        }
    }
}
