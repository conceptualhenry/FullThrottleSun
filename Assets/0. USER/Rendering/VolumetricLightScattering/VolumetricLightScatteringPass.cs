using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP Render Graph pass: screen-space volumetric light scattering with an
/// occlusion mask (only sky / far depths emit; near props stay black so shafts
/// break when geometry covers the sun disk).
/// </summary>
public sealed class VolumetricLightScatteringPass : ScriptableRenderPass
{
    const int PassOcclusion = 0;
    const int PassScatter = 1;

    static readonly int SunParamsId = Shader.PropertyToID("_SunParams");
    static readonly int RayParamsId = Shader.PropertyToID("_RayParams");
    static readonly int VlsColorId = Shader.PropertyToID("_VLSColor");
    static readonly int DepthFadeId = Shader.PropertyToID("_DepthFade");
    static readonly int OcclusionTexId = Shader.PropertyToID("_OcclusionTex");

    readonly Material material;
    VolumetricLightScatteringFeature.Settings settings;

    Vector2 sunUV;
    float intensity;
    bool active;

    public VolumetricLightScatteringPass(Material material)
    {
        this.material = material;
        requiresIntermediateTexture = true;
    }

    public void Setup(VolumetricLightScatteringFeature.Settings settings, Camera camera)
    {
        this.settings = settings;
        ConfigureInput(ScriptableRenderPassInput.Depth);

        active = false;
        intensity = 0f;
        sunUV = new Vector2(0.5f, 0.5f);

        if (material == null || camera == null || settings == null)
            return;

        var car = GameObject.Find("Car");
        Transform sun = car != null ? car.transform.Find("Sun_") : null;

        Vector3 sunWorld;
        if (sun != null)
        {
            sunWorld = sun.position;
        }
        else
        {
            Light light = RenderSettings.sun;
            if (light == null)
                light = Object.FindFirstObjectByType<Light>();
            if (light == null || light.type != LightType.Directional)
                return;
            sunWorld = camera.transform.position - light.transform.forward * 500f;
        }

        Vector3 vp = camera.WorldToViewportPoint(sunWorld);
        if (vp.z <= 0f)
            return;

        sunUV = new Vector2(vp.x, vp.y);
        if (sunUV.x < -0.35f || sunUV.x > 1.35f || sunUV.y < -0.35f || sunUV.y > 1.35f)
            return;

        float centerDist = Vector2.Distance(sunUV, new Vector2(0.5f, 0.5f));
        float look = 1f - Mathf.Clamp01(centerDist / Mathf.Max(0.05f, settings.falloff));
        intensity = settings.intensity * look * look;

        if (settings.requireSunZone)
        {
            Transform zoneT = car != null ? car.transform.Find("SunZone") : null;
            Collider zone = zoneT != null ? zoneT.GetComponent<Collider>() : null;
            if (zone == null || !zone.enabled)
            {
                intensity = 0f;
            }
            else
            {
                Vector3 p = camera.transform.position;
                if ((zone.ClosestPoint(p) - p).sqrMagnitude >= 0.0001f)
                    intensity = 0f;
            }
        }

        active = intensity > 0.001f;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (!active || material == null)
            return;

        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        if (resourceData.isActiveTargetBackBuffer)
            return;

        TextureHandle srcColor = resourceData.activeColorTexture;
        if (!srcColor.IsValid())
            return;

        material.SetVector(SunParamsId, new Vector4(sunUV.x, sunUV.y, settings.sunRadius, intensity));
        material.SetVector(RayParamsId, new Vector4(settings.decay, settings.exposure, settings.sampleCount, settings.density));
        material.SetColor(VlsColorId, settings.color);
        // x = near (little emit), y = far (full emit) — same soft ramp as original VLS.
        material.SetVector(DepthFadeId, new Vector4(
            settings.depthNear,
            Mathf.Max(settings.depthNear + 0.01f, settings.depthFar),
            0f, 0f));

        TextureDesc occlDesc = srcColor.GetDescriptor(renderGraph);
        occlDesc.name = "_VLS_Occlusion";
        occlDesc.width = Mathf.Max(1, occlDesc.width / 2);
        occlDesc.height = Mathf.Max(1, occlDesc.height / 2);
        occlDesc.depthBufferBits = 0;
        occlDesc.clearBuffer = true;
        occlDesc.clearColor = Color.black;
        TextureHandle occlusion = renderGraph.CreateTexture(occlDesc);

        var occlBlit = new RenderGraphUtils.BlitMaterialParameters(srcColor, occlusion, material, PassOcclusion);
        using (var builder = renderGraph.AddBlitPass(occlBlit, passName: "VLS_Occlusion", returnBuilder: true))
        {
            if (resourceData.cameraDepthTexture.IsValid())
                builder.UseTexture(resourceData.cameraDepthTexture);
        }

        TextureDesc destDesc = srcColor.GetDescriptor(renderGraph);
        destDesc.name = "_VLS_Destination";
        destDesc.depthBufferBits = 0;
        destDesc.clearBuffer = false;
        TextureHandle destination = renderGraph.CreateTexture(destDesc);

        using (var builder = renderGraph.AddRasterRenderPass<ScatterPassData>(
                   "VLS_Scatter", out var passData))
        {
            passData.material = material;
            passData.source = srcColor;
            passData.occlusion = occlusion;

            builder.UseTexture(srcColor, AccessFlags.Read);
            builder.UseTexture(occlusion, AccessFlags.Read);
            if (resourceData.cameraDepthTexture.IsValid())
                builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
            builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

            builder.SetRenderFunc(static (ScatterPassData data, RasterGraphContext ctx) =>
            {
                data.material.SetTexture(OcclusionTexId, data.occlusion);
                Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1f, 1f, 0f, 0f), data.material, PassScatter);
            });
        }

        resourceData.cameraColor = destination;
    }

    sealed class ScatterPassData
    {
        public Material material;
        public TextureHandle source;
        public TextureHandle occlusion;
    }
}
