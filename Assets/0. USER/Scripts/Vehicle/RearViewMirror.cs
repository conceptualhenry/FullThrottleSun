using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Renders the assigned scene <see cref="mirrorCam"/> into this glass while in first person.
    /// Aim / FOV / culling mask on the Camera in the Inspector — runtime does not overwrite them.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public class RearViewMirror : MonoBehaviour
    {
        public enum Kind { Interior, Left, Right }

        [SerializeField] Kind kind = Kind.Interior;
        [SerializeField] CarFollowCamera viewCamera;
        [Tooltip("Scene camera under this mirror. Aim it yourself; Play only samples it.")]
        [SerializeField] Camera mirrorCam;
        [SerializeField] int resolution = 384;
        [Tooltip("Barrel warp on the glass (0 = flat).")]
        [Range(0f, 1.5f)] [SerializeField] float convexStrength = 0.55f;
        [Min(1)] [SerializeField] int frameInterval = 1;

        Renderer mirrorRenderer;
        RenderTexture rt;
        Material matInstance;
        int frameCounter;
        bool ownsCamera;
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

            EnsureCamera();
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
            mirrorCam.fieldOfView = 115f;
            mirrorCam.nearClipPlane = 0.05f;
            mirrorCam.farClipPlane = 150f;
            mirrorCam.cullingMask = ~0;
            ownsCamera = true;
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
            if (matInstance.HasProperty("_BaseMap"))
            {
                matInstance.SetTextureScale("_BaseMap", Vector2.one);
                matInstance.SetTextureOffset("_BaseMap", Vector2.zero);
            }

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
    }
}
