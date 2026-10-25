using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Headlights, tail / brake / turn signal lamps for the Classic car light meshes
    /// (Light_LF_* = left, Light_RT_* = right; _W1/_W2 headlights, _Y front amber, _R rear red).
    /// </summary>
    [RequireComponent(typeof(TireFrictionCar))]
    public class CarLights : MonoBehaviour
    {
        public enum BeamMode { Off, Low, High }
        public enum TurnSignal { Off, Left, Right }

        [Header("Refs (auto-filled if empty)")]
        [SerializeField] Renderer leftTail;
        [SerializeField] Renderer rightTail;
        [SerializeField] Renderer leftHeadW1;
        [SerializeField] Renderer leftHeadW2;
        [SerializeField] Renderer rightHeadW1;
        [SerializeField] Renderer rightHeadW2;
        [SerializeField] Renderer leftTurnY;
        [SerializeField] Renderer rightTurnY;

        [Header("Night")]
        [Tooltip("Directional light intensity below this turns parking + low beam on.")]
        [SerializeField] float nightSunThreshold = 0.35f;
        [SerializeField] bool autoNightLights = true;

        [Header("Blink / cancel")]
        [SerializeField] float blinkHz = 1.5f;
        [Tooltip("Turn signal shuts off by itself after this many seconds.")]
        [SerializeField] float turnAutoOffSeconds = 60f;
        [Tooltip("Steering-wheel degrees (FP rim / TP virtual) in the signal direction that arms cancel.")]
        [SerializeField] float turnCancelSteerDegrees = 15f;
        [Tooltip("After armed, cancel when the wheel returns inside this many degrees of centre.")]
        [SerializeField] float turnCancelReturnDegrees = 15f;

        [Header("Emission multipliers")]
        [SerializeField] float tailEmission = 1.1f;
        [SerializeField] float brakeEmission = 3.2f;
        [SerializeField] float turnEmission = 3.5f;
        [SerializeField] float headW2Emission = 1.2f;
        [SerializeField] float headLowEmission = 1.5f;
        [SerializeField] float headHighEmission = 3f;

        [Header("Unity lights — head (W1 weak/strong, W2 parking)")]
        [SerializeField] float lowIntensity = 2.2f;
        [SerializeField] float highIntensity = 5.5f;
        [SerializeField] float parkingIntensity = 0.7f;
        [SerializeField] float lowSpotAngle = 75f;
        [SerializeField] float highSpotAngle = 40f;
        [SerializeField] float parkingSpotAngle = 110f;
        [SerializeField] float lowPitchDown = 10f;
        [SerializeField] float highPitchDown = 2f;
        [SerializeField] float headRange = 45f;

        [Header("Unity lights — rear (_R: short spots aimed aft, outside the body)")]
        [SerializeField] float rearTailIntensity = 0.9f;
        [SerializeField] float rearBrakeIntensity = 2.0f;
        [SerializeField] float rearTurnIntensity = 1.6f;
        [SerializeField] float rearRange = 1.4f;
        [SerializeField] float rearSpotAngle = 75f;
        [SerializeField] float rearPushOut = 0.14f;
        [Header("Unity lights — front amber turn (_Y: short spots aimed outboard)")]
        [SerializeField] float frontTurnIntensity = 1.0f;
        [SerializeField] float frontTurnRange = 1.3f;
        [SerializeField] float frontTurnSpotAngle = 70f;
        [SerializeField] float frontPushOut = 0.12f;

        [Header("Debug")]
        [SerializeField] BeamMode beam = BeamMode.Off;
        [SerializeField] TurnSignal turn = TurnSignal.Off;
        [SerializeField] bool night;
        [SerializeField] bool blinkOn;

        TireFrictionCar car;
        Material leftTailMat, rightTailMat, leftW1Mat, leftW2Mat, rightW1Mat, rightW2Mat, leftYMat, rightYMat;
        Color leftTailEmis, rightTailEmis, leftW1Emis, leftW2Emis, rightW1Emis, rightW2Emis, leftYEmis, rightYEmis;

        Light leftLow, leftHigh, leftPark, rightLow, rightHigh, rightPark;
        Light leftTailLight, leftBrakeLight, leftRearTurn, rightTailLight, rightBrakeLight, rightRearTurn;
        Light leftFrontTurn, rightFrontTurn;

        float blinkPhase;
        float turnOnTime = -1f;
        /// <summary>True after the wheel has gone past the arm threshold in the signal direction.</summary>
        bool turnCancelArmed;

        public BeamMode Beam
        {
            get => beam;
            set => beam = value;
        }

        public TurnSignal Turn
        {
            get => turn;
            set
            {
                turn = value;
                turnOnTime = value == TurnSignal.Off ? -1f : Time.time;
                turnCancelArmed = false;
            }
        }

        public bool IsNight => night;

        public void SetBeam(BeamMode mode) => beam = mode;

        public void ToggleBeamHighLow()
        {
            if (beam == BeamMode.High)
                beam = BeamMode.Low;
            else
                beam = BeamMode.High;
        }

        /// <summary>Stick flick → that side on; same side again cancels; other side switches.</summary>
        public void PulseTurn(TurnSignal side)
        {
            if (side == TurnSignal.Off)
                return;
            if (turn == side)
            {
                turn = TurnSignal.Off;
                turnOnTime = -1f;
                turnCancelArmed = false;
            }
            else
            {
                turn = side;
                turnOnTime = Time.time;
                turnCancelArmed = false;
            }
        }

        public void CancelTurn()
        {
            turn = TurnSignal.Off;
            turnOnTime = -1f;
            turnCancelArmed = false;
        }

        void Awake()
        {
            car = GetComponent<TireFrictionCar>();
            AutoFind();
            BindMaterials();
            DullCarBodyOpaqueMaterials();
            BuildUnityLights();
            Apply(forceOff: true);
        }

        void Update()
        {
            if (autoNightLights)
                UpdateNight();

            UpdateTurnCancel();

            blinkPhase += Time.deltaTime * blinkHz;
            blinkOn = (blinkPhase % 1f) < 0.5f;
            Apply(forceOff: false);
        }

        void UpdateTurnCancel()
        {
            if (turn == TurnSignal.Off)
                return;

            if (turnOnTime >= 0f && Time.time - turnOnTime >= turnAutoOffSeconds)
            {
                CancelTurn();
                return;
            }

            if (car == null)
                return;

            // Same source for FP grabbed wheel and TP virtual wheel (degrees, right +).
            float wheel = car.SteeringWheelAngle;
            float arm = Mathf.Max(0.1f, turnCancelSteerDegrees);
            float ret = Mathf.Max(0f, turnCancelReturnDegrees);

            if (turn == TurnSignal.Right)
            {
                if (wheel >= arm)
                    turnCancelArmed = true;
                // Armed by turning right past threshold → cancel when unwinding back toward centre.
                if (turnCancelArmed && wheel <= ret)
                    CancelTurn();
            }
            else if (turn == TurnSignal.Left)
            {
                if (wheel <= -arm)
                    turnCancelArmed = true;
                if (turnCancelArmed && wheel >= -ret)
                    CancelTurn();
            }
        }

        void UpdateNight()
        {
            float sun = 0f;
            if (RenderSettings.sun != null && RenderSettings.sun.enabled)
                sun = RenderSettings.sun.intensity;
            else
            {
                foreach (var l in FindObjectsByType<Light>())
                {
                    if (l.type == LightType.Directional && l.enabled)
                        sun = Mathf.Max(sun, l.intensity);
                }
            }

            bool nowNight = sun < nightSunThreshold;
            if (nowNight == night)
                return;
            night = nowNight;
            // Entering night: ensure at least low beam. Leaving night: keep beam if player set high/low manually.
            if (night && beam == BeamMode.Off)
                beam = BeamMode.Low;
        }

        void AutoFind()
        {
            leftTail = leftTail != null ? leftTail : FindRend("Light_LF_R");
            rightTail = rightTail != null ? rightTail : FindRend("Light_RT_R");
            leftHeadW1 = leftHeadW1 != null ? leftHeadW1 : FindRend("Light_LF_W1");
            leftHeadW2 = leftHeadW2 != null ? leftHeadW2 : FindRend("Light_LF_W2");
            rightHeadW1 = rightHeadW1 != null ? rightHeadW1 : FindRend("Light_RT_W1");
            rightHeadW2 = rightHeadW2 != null ? rightHeadW2 : FindRend("Light_RT_W2");
            leftTurnY = leftTurnY != null ? leftTurnY : FindRend("Light_LF_Y");
            rightTurnY = rightTurnY != null ? rightTurnY : FindRend("Light_RT_Y");
        }

        Renderer FindRend(string objectName)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == objectName)
                    return t.GetComponent<Renderer>();
            return null;
        }

        void BindMaterials()
        {
            leftTailMat = Inst(leftTail, out leftTailEmis);
            rightTailMat = Inst(rightTail, out rightTailEmis);
            leftW1Mat = Inst(leftHeadW1, out leftW1Emis);
            leftW2Mat = Inst(leftHeadW2, out leftW2Emis);
            rightW1Mat = Inst(rightHeadW1, out rightW1Emis);
            rightW2Mat = Inst(rightHeadW2, out rightW2Emis);
            leftYMat = Inst(leftTurnY, out leftYEmis);
            rightYMat = Inst(rightTurnY, out rightYEmis);
        }

        static Material Inst(Renderer r, out Color baseEmission)
        {
            baseEmission = Color.black;
            if (r == null)
                return null;
            var mat = r.material; // instance
            if (mat.HasProperty("_EmissionColor"))
            {
                baseEmission = mat.GetColor("_EmissionColor");
                if (baseEmission.maxColorComponent < 0.05f)
                    baseEmission = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
            }
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return mat;
        }

        static void EnsureOpaqueLens(Material mat)
        {
            if (mat == null)
                return;
            if (mat.HasProperty("_Surface"))
                mat.SetFloat("_Surface", 0f);
            if (mat.HasProperty("_Blend"))
                mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend"))
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (mat.HasProperty("_DstBlend"))
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            if (mat.HasProperty("_SrcBlendAlpha"))
                mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            if (mat.HasProperty("_DstBlendAlpha"))
                mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.Zero);
            if (mat.HasProperty("_ZWrite"))
                mat.SetFloat("_ZWrite", 1f);
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = -1;
            mat.SetOverrideTag("RenderType", "Opaque");
            if (mat.HasProperty("_BaseColor"))
            {
                var c = mat.GetColor("_BaseColor");
                c.a = 1f;
                mat.SetColor("_BaseColor", c);
            }
            if (mat.HasProperty("_Color"))
            {
                var c = mat.GetColor("_Color");
                c.a = 1f;
                mat.SetColor("_Color", c);
            }
            KillSpecular(mat);
        }

        /// <summary>
        /// Opaque body / door / trim should not specular-bounce lamp point/spot lights through the cabin.
        /// Skips glass, mirrors, and the lamp meshes themselves (those use emission).
        /// </summary>
        void DullCarBodyOpaqueMaterials()
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r == null)
                    continue;
                string n = r.name;
                if (n.StartsWith("Light_"))
                    continue;
                if (n.IndexOf("Mirror", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                var mats = r.materials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var mat = mats[i];
                    if (mat == null)
                        continue;
                    bool transparent = mat.HasProperty("_Surface") && mat.GetFloat("_Surface") > 0.5f;
                    if (transparent)
                        continue;
                    if (mat.name.IndexOf("Glass", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                    if (mat.name.IndexOf("Mirror", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                    KillSpecular(mat);
                    changed = true;
                }
                if (changed)
                    r.materials = mats;
            }
        }

        static void KillSpecular(Material mat)
        {
            if (mat == null)
                return;
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", 0.05f);
            if (mat.HasProperty("_Glossiness"))
                mat.SetFloat("_Glossiness", 0.05f);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", 0f);
            if (mat.HasProperty("_SpecularHighlights"))
                mat.SetFloat("_SpecularHighlights", 0f);
            if (mat.HasProperty("_EnvironmentReflections"))
                mat.SetFloat("_EnvironmentReflections", 0f);
            if (mat.HasProperty("_GlossyReflections"))
                mat.SetFloat("_GlossyReflections", 0f);
        }

        void BuildUnityLights()
        {
            ClearHostLights(leftHeadW1);
            ClearHostLights(leftHeadW2);
            ClearHostLights(rightHeadW1);
            ClearHostLights(rightHeadW2);
            ClearHostLights(leftTail);
            ClearHostLights(rightTail);
            ClearHostLights(leftTurnY);
            ClearHostLights(rightTurnY);

            // Keep StopSignal / turn lens covers opaque — glow comes from emission + short point lights.
            EnsureOpaqueLens(leftTailMat);
            EnsureOpaqueLens(rightTailMat);
            EnsureOpaqueLens(leftYMat);
            EnsureOpaqueLens(rightYMat);

            leftLow = MakeSpot(leftHeadW1, "Low", lowSpotAngle, lowIntensity);
            leftHigh = MakeSpot(leftHeadW1, "High", highSpotAngle, highIntensity);
            leftPark = MakeSpot(leftHeadW2, "Park", parkingSpotAngle, parkingIntensity);
            rightLow = MakeSpot(rightHeadW1, "Low", lowSpotAngle, lowIntensity);
            rightHigh = MakeSpot(rightHeadW1, "High", highSpotAngle, highIntensity);
            rightPark = MakeSpot(rightHeadW2, "Park", parkingSpotAngle, parkingIntensity);

            // Spots sit just outside the lens and aim away from the cabin so light cannot flood through the body.
            leftTailLight = MakeExteriorSpot(leftTail, "Tail", rearSpotAngle, rearTailIntensity, rearRange, new Color(1f, 0.05f, 0.02f), -transform.forward, rearPushOut);
            leftBrakeLight = MakeExteriorSpot(leftTail, "Brake", rearSpotAngle, rearBrakeIntensity, rearRange, new Color(1f, 0.02f, 0.02f), -transform.forward, rearPushOut);
            leftRearTurn = MakeExteriorSpot(leftTail, "Turn", rearSpotAngle, rearTurnIntensity, rearRange, new Color(1f, 0.45f, 0.02f), -transform.forward, rearPushOut);
            rightTailLight = MakeExteriorSpot(rightTail, "Tail", rearSpotAngle, rearTailIntensity, rearRange, new Color(1f, 0.05f, 0.02f), -transform.forward, rearPushOut);
            rightBrakeLight = MakeExteriorSpot(rightTail, "Brake", rearSpotAngle, rearBrakeIntensity, rearRange, new Color(1f, 0.02f, 0.02f), -transform.forward, rearPushOut);
            rightRearTurn = MakeExteriorSpot(rightTail, "Turn", rearSpotAngle, rearTurnIntensity, rearRange, new Color(1f, 0.45f, 0.02f), -transform.forward, rearPushOut);

            Vector3 leftOut = (-transform.right + transform.forward * 0.25f).normalized;
            Vector3 rightOut = (transform.right + transform.forward * 0.25f).normalized;
            leftFrontTurn = MakeExteriorSpot(leftTurnY, "FrontTurn", frontTurnSpotAngle, frontTurnIntensity, frontTurnRange, new Color(1f, 0.55f, 0.05f), leftOut, frontPushOut);
            rightFrontTurn = MakeExteriorSpot(rightTurnY, "FrontTurn", frontTurnSpotAngle, frontTurnIntensity, frontTurnRange, new Color(1f, 0.55f, 0.05f), rightOut, frontPushOut);
        }

        static void ClearHostLights(Renderer host)
        {
            if (host == null)
                return;
            for (int i = host.transform.childCount - 1; i >= 0; i--)
            {
                var child = host.transform.GetChild(i);
                if (child.name.StartsWith("Light_") && child.GetComponent<Light>() != null)
                    Destroy(child.gameObject);
            }
        }

        Light MakeSpot(Renderer host, string label, float angle, float intensity)
        {
            if (host == null)
                return null;
            var go = new GameObject("Light_" + label);
            go.transform.SetParent(host.transform, false);
            go.transform.position = host.bounds.center + transform.forward * 0.05f;
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = angle;
            light.innerSpotAngle = angle * 0.55f;
            light.range = headRange;
            light.intensity = intensity;
            light.color = new Color(1f, 0.96f, 0.85f);
            light.shadows = LightShadows.Soft;
            light.enabled = false;
            return light;
        }

        Light MakeExteriorSpot(Renderer host, string label, float angle, float intensity, float range, Color color, Vector3 aimDir, float pushOut)
        {
            if (host == null)
                return null;
            var go = new GameObject("Light_" + label);
            go.transform.SetParent(host.transform, false);
            Vector3 dir = aimDir.sqrMagnitude > 0.0001f ? aimDir.normalized : -transform.forward;
            go.transform.position = host.bounds.center + dir * pushOut;
            go.transform.rotation = Quaternion.LookRotation(dir, transform.up);
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = angle;
            light.innerSpotAngle = angle * 0.55f;
            light.range = range;
            light.intensity = intensity;
            light.color = color;
            light.shadows = LightShadows.None;
            // Everything — leak is prevented by aiming outward, not by hiding layers.
            light.cullingMask = ~0;
            light.enabled = false;
            return light;
        }

        void Apply(bool forceOff)
        {
            bool braking = car != null && (car.BrakeInput > 0.15f || car.HandbrakeInput);
            // Night parking: W2 + rear tails whenever night or any headlight mode is on.
            bool parking = !forceOff && (night || beam != BeamMode.Off);
            bool low = !forceOff && beam == BeamMode.Low;
            bool high = !forceOff && beam == BeamMode.High;
            // Auto night forces low if somehow Off while night handled in UpdateNight.

            bool leftBlink = !forceOff && turn == TurnSignal.Left && blinkOn;
            bool rightBlink = !forceOff && turn == TurnSignal.Right && blinkOn;

            // Head emissives + spots
            SetEmis(leftW2Mat, leftW2Emis, parking ? headW2Emission : 0f);
            SetEmis(rightW2Mat, rightW2Emis, parking ? headW2Emission : 0f);
            SetEmis(leftW1Mat, leftW1Emis, high ? headHighEmission : low ? headLowEmission : 0f);
            SetEmis(rightW1Mat, rightW1Emis, high ? headHighEmission : low ? headLowEmission : 0f);

            AimSpot(leftLow, transform.forward, lowPitchDown);
            AimSpot(leftHigh, transform.forward, highPitchDown);
            AimSpot(rightLow, transform.forward, lowPitchDown);
            AimSpot(rightHigh, transform.forward, highPitchDown);
            AimSpot(leftPark, transform.forward, 5f);
            AimSpot(rightPark, transform.forward, 5f);
            AimSpot(leftTailLight, -transform.forward, 0f);
            AimSpot(leftBrakeLight, -transform.forward, 0f);
            AimSpot(leftRearTurn, -transform.forward, 0f);
            AimSpot(rightTailLight, -transform.forward, 0f);
            AimSpot(rightBrakeLight, -transform.forward, 0f);
            AimSpot(rightRearTurn, -transform.forward, 0f);
            AimSpot(leftFrontTurn, (-transform.right + transform.forward * 0.25f).normalized, 0f);
            AimSpot(rightFrontTurn, (transform.right + transform.forward * 0.25f).normalized, 0f);

            SetLight(leftPark, parking);
            SetLight(rightPark, parking);
            SetLight(leftLow, low);
            SetLight(rightLow, low);
            SetLight(leftHigh, high);
            SetLight(rightHigh, high);

            // Rear red: tail / brake / turn share one mesh — emission takes the brightest role.
            float leftRearMul = 0f;
            float rightRearMul = 0f;
            if (parking)
            {
                leftRearMul = Mathf.Max(leftRearMul, tailEmission);
                rightRearMul = Mathf.Max(rightRearMul, tailEmission);
            }
            if (braking)
            {
                leftRearMul = Mathf.Max(leftRearMul, brakeEmission);
                rightRearMul = Mathf.Max(rightRearMul, brakeEmission);
            }
            if (leftBlink)
                leftRearMul = Mathf.Max(leftRearMul, turnEmission);
            if (rightBlink)
                rightRearMul = Mathf.Max(rightRearMul, turnEmission);

            SetEmis(leftTailMat, leftTailEmis, leftRearMul);
            SetEmis(rightTailMat, rightTailEmis, rightRearMul);
            SetLight(leftTailLight, parking && !braking && !leftBlink);
            SetLight(rightTailLight, parking && !braking && !rightBlink);
            SetLight(leftBrakeLight, braking);
            SetLight(rightBrakeLight, braking);
            SetLight(leftRearTurn, leftBlink);
            SetLight(rightRearTurn, rightBlink);
            SetLight(leftFrontTurn, leftBlink);
            SetLight(rightFrontTurn, rightBlink);

            SetEmis(leftYMat, leftYEmis, leftBlink ? turnEmission : 0f);
            SetEmis(rightYMat, rightYEmis, rightBlink ? turnEmission : 0f);
        }

        void AimSpot(Light spot, Vector3 forward, float pitchDown)
        {
            if (spot == null)
                return;
            if (forward.sqrMagnitude < 0.0001f)
                forward = transform.forward;
            spot.transform.rotation = Quaternion.LookRotation(forward.normalized, transform.up)
                                     * Quaternion.Euler(pitchDown, 0f, 0f);
        }

        static void SetLight(Light light, bool on)
        {
            if (light != null)
                light.enabled = on;
        }

        static void SetEmis(Material mat, Color baseEmis, float mul)
        {
            if (mat == null)
                return;
            if (mul <= 0.001f)
            {
                mat.SetColor("_EmissionColor", Color.black);
                return;
            }
            // HDR emission — URP expects boosted color.
            mat.SetColor("_EmissionColor", baseEmis * mul);
        }
    }
}
