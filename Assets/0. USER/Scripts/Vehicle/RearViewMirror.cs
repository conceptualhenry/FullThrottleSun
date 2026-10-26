using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Renders the assigned scene <see cref="mirrorCam"/> into this glass while in first person.
    /// Mesh UVs are remapped from the car atlas island onto the full RenderTexture.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public class RearViewMirror : MonoBehaviour
    {
        public enum Kind { Interior, Left, Right }

        [SerializeField] Kind kind = Kind.Interior;
        [SerializeField] CarFollowCamera viewCamera;
        [Tooltip("Scene camera under this mirror. Aim it yourself; Play only samples it. Leave empty to auto-create.")]
        [SerializeField] Camera mirrorCam;
        [SerializeField] int resolution = 512;
        [Tooltip("Barrel warp on the glass (0 = flat).")]
        [Range(0f, 1.5f)] [SerializeField] float convexStrength = 0.55f;
        [Min(1)] [SerializeField] int frameInterval = 1;
        [Tooltip("If true, auto-created cameras are aimed for a rear view. Ignored when you assign mirrorCam.")]
        [SerializeField] bool autoAimOwnedCamera = true;

        Renderer mirrorRenderer;
        RenderTexture rt;
        Material matInstance;
        int frameCounter;
        bool ownsCamera;
        Vector2 uvScale = Vector2.one;
        Vector2 uvOffset = Vector2.zero;
        readonly RenderPipeline.StandardRequest renderRequest = new RenderPipeline.StandardRequest();

        public Camera MirrorCamera => mirrorCam;

        void Awake()
        {
            mirrorRenderer = GetComponent<Renderer>();
            if (viewCamera == null)
                viewCamera = FindAnyObjectByType<CarFollowCamera>();
            if (kind == Kind.Interior && name.Contains("FL"))
                kind = Kind.Left;
            else if (kind == Kind.Interior && name.Contains("FR"))
                kind = Kind.Right;

            ComputeAtlasUvRemap();
            EnsureCamera();
            if (ownsCamera && autoAimOwnedCamera)
                AimOwnedCamera();
            EnsureRenderTexture();
            EnsureMaterial();
            ConfigureCameraForRender();
        }

        void OnDestroy()
        {
            if (ownsCamera && mirrorCam != null)
                Destroy(mirrorCam.gameObject);
            if (rt != null)
            {
                rt.Release();
                Destroy(rt);
            }
            if (matInstance != null)
                Destroy(matInstance);
        }

        void LateUpdate()
        {
            if (mirrorCam == null || rt == null)
                return;

            bool want = viewCamera != null && viewCamera.IsFirstPerson;
            if (!want)
                return;

            if (matInstance != null && matInstance.HasProperty("_Convex"))
                matInstance.SetFloat("_Convex", convexStrength);

            frameCounter++;
            if (frameCounter % frameInterval != 0)
                return;

            // Keep the Camera GameObject always active so Scene/Inspector preview works.
            // Component stays disabled; we only sample via SubmitRenderRequest / Render.
            bool was = mirrorRenderer.enabled;
            mirrorRenderer.enabled = false;
            RenderToTexture();
            mirrorRenderer.enabled = was;
        }

        void RenderToTexture()
        {
            if (mirrorCam == null || rt == null)
                return;

            // Ensure Everything if someone left the mask at Nothing (common serialization gotcha).
            if (mirrorCam.cullingMask == 0)
                mirrorCam.cullingMask = ~0;

            mirrorCam.targetTexture = rt;
            renderRequest.destination = rt;
            if (RenderPipeline.SupportsRenderRequest(mirrorCam, renderRequest))
            {
                RenderPipeline.SubmitRenderRequest(mirrorCam, renderRequest);
                return;
            }

            mirrorCam.Render();
        }

        void EnsureCamera()
        {
            if (mirrorCam != null)
            {
                ownsCamera = false;
                return;
            }

            var existing = GetComponentInChildren<Camera>(true);
            if (existing != null)
            {
                mirrorCam = existing;
                ownsCamera = false;
                return;
            }

            var go = new GameObject("MirrorCam");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            mirrorCam = go.AddComponent<Camera>();
            mirrorCam.fieldOfView = kind == Kind.Interior ? 70f : 95f;
            mirrorCam.nearClipPlane = 0.08f;
            mirrorCam.farClipPlane = 400f;
            mirrorCam.cullingMask = ~0;
            ownsCamera = true;
        }

        /// <summary>
        /// Auto cameras start with identity rotation (= car forward). Rear / side mirrors need to look back.
        /// </summary>
        void AimOwnedCamera()
        {
            if (mirrorCam == null)
                return;

            Transform t = mirrorCam.transform;
            switch (kind)
            {
                case Kind.Left:
                    // Back and outboard (left).
                    t.localRotation = Quaternion.Euler(12f, -150f, 0f);
                    t.localPosition = t.localRotation * new Vector3(0f, 0f, 0.12f);
                    mirrorCam.fieldOfView = 95f;
                    break;
                case Kind.Right:
                    t.localRotation = Quaternion.Euler(12f, 150f, 0f);
                    t.localPosition = t.localRotation * new Vector3(0f, 0f, 0.12f);
                    mirrorCam.fieldOfView = 95f;
                    break;
                default:
                    // Interior: look through the cabin / rear glass.
                    t.localRotation = Quaternion.Euler(5f, 180f, 0f);
                    t.localPosition = t.localRotation * new Vector3(0f, 0f, 0.1f);
                    mirrorCam.fieldOfView = 70f;
                    break;
            }
        }

        void EnsureRenderTexture()
        {
            if (rt != null)
                return;
            rt = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32)
            {
                name = name + " RT",
                filterMode = FilterMode.Bilinear,
                antiAliasing = 1
            };
            rt.Create();
        }

        void ConfigureCameraForRender()
        {
            if (mirrorCam == null)
                return;

            // Never deactivate the GameObject — that makes the Camera preview look empty.
            mirrorCam.gameObject.SetActive(true);
            mirrorCam.enabled = false;
            mirrorCam.stereoTargetEye = StereoTargetEyeMask.None;
            mirrorCam.depth = -20;
            mirrorCam.clearFlags = CameraClearFlags.Skybox;
            mirrorCam.allowHDR = false;
            mirrorCam.allowMSAA = false;
            if (mirrorCam.farClipPlane < 100f)
                mirrorCam.farClipPlane = 400f;
            if (mirrorCam.cullingMask == 0)
                mirrorCam.cullingMask = ~0;
            mirrorCam.targetTexture = rt;

            var add = mirrorCam.GetComponent<UniversalAdditionalCameraData>();
            if (add == null)
                add = mirrorCam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            add.renderType = CameraRenderType.Base;
            add.renderPostProcessing = false;
            add.renderShadows = false;
        }

        void EnsureMaterial()
        {
            var shader = Shader.Find("FullThrottleSun/ConvexMirror");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Texture");
            matInstance = shader != null
                ? new Material(shader)
                : new Material(mirrorRenderer.sharedMaterial);
            matInstance.name = name + " MirrorRT";
            matInstance.renderQueue = (int)RenderQueue.Geometry;
            matInstance.SetOverrideTag("RenderType", "Opaque");
            if (matInstance.HasProperty("_Surface"))
                matInstance.SetFloat("_Surface", 0f);
            if (matInstance.HasProperty("_ZWrite"))
                matInstance.SetFloat("_ZWrite", 1f);
            if (matInstance.HasProperty("_BaseColor"))
                matInstance.SetColor("_BaseColor", Color.white);
            if (matInstance.HasProperty("_Color"))
                matInstance.SetColor("_Color", Color.white);
            if (matInstance.HasProperty("_Convex"))
                matInstance.SetFloat("_Convex", convexStrength);

            AssignTexture(rt);
            ApplyUvRemap();

            mirrorRenderer.material = matInstance;
        }

        void AssignTexture(Texture tex)
        {
            if (matInstance.HasProperty("_BaseMap"))
                matInstance.SetTexture("_BaseMap", tex);
            if (matInstance.HasProperty("_MainTex"))
                matInstance.SetTexture("_MainTex", tex);
            matInstance.mainTexture = tex;
        }

        /// <summary>
        /// Car mirror meshes share a tiny atlas island (e.g. UV 0.10–0.19). Without remapping,
        /// the RenderTexture is sampled as a flat speck of colour.
        /// </summary>
        void ComputeAtlasUvRemap()
        {
            uvScale = Vector2.one;
            uvOffset = Vector2.zero;

            Mesh mesh = null;
            var mf = GetComponent<MeshFilter>();
            if (mf != null)
                mesh = mf.sharedMesh;
            if (mesh == null)
            {
                var smr = GetComponent<SkinnedMeshRenderer>();
                if (smr != null)
                    mesh = smr.sharedMesh;
            }
            if (mesh == null)
                return;

            var uvs = mesh.uv;
            if (uvs == null || uvs.Length == 0)
                return;

            Vector2 min = uvs[0];
            Vector2 max = uvs[0];
            for (int i = 1; i < uvs.Length; i++)
            {
                min = Vector2.Min(min, uvs[i]);
                max = Vector2.Max(max, uvs[i]);
            }

            Vector2 size = max - min;
            // Full 0–1 UVs need no remap. Degenerate islands are left alone.
            if (size.x < 1e-4f || size.y < 1e-4f)
                return;
            if (size.x > 0.95f && size.y > 0.95f && min.x > -0.05f && min.y > -0.05f)
                return;

            uvScale = new Vector2(1f / size.x, 1f / size.y);
            uvOffset = new Vector2(-min.x * uvScale.x, -min.y * uvScale.y);
        }

        void ApplyUvRemap()
        {
            if (matInstance == null)
                return;
            if (matInstance.HasProperty("_BaseMap"))
            {
                matInstance.SetTextureScale("_BaseMap", uvScale);
                matInstance.SetTextureOffset("_BaseMap", uvOffset);
            }
            if (matInstance.HasProperty("_MainTex"))
            {
                matInstance.SetTextureScale("_MainTex", uvScale);
                matInstance.SetTextureOffset("_MainTex", uvOffset);
            }
        }
    }
}
