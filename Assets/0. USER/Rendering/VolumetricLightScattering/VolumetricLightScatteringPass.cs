using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP Render Graph pass: screen-space volumetric light scattering (Tyndall / god rays)
/// using camera depth occlusion — not a mesh billboard.
/// </summary>
public sealed class VolumetricLightScatteringPass : ScriptableRenderPass
{
    static readonly int SunParamsId = Shader.PropertyToID("_SunParams");
    static readonly int RayParamsId = Shader.PropertyToID("_RayParams");
    static readonly int VlsColorId = Shader.PropertyToID("_VLSColor");
    static readonly int DepthFadeId = Shader.PropertyToID("_DepthFade");

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
        // Allow a little off-screen so shafts can still enter the frame.
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
        material.SetVector(DepthFadeId, new Vector4(settings.depthNear, Mathf.Max(settings.depthNear + 0.01f, settings.depthFar), 0f, 0f));

        TextureDesc desc = srcColor.GetDescriptor(renderGraph);
        desc.name = "_VLS_Destination";
        desc.depthBufferBits = 0;
        desc.clearBuffer = false;
        TextureHandle destination = renderGraph.CreateTexture(desc);

        var blit = new RenderGraphUtils.BlitMaterialParameters(srcColor, destination, material, 0);
        using (var builder = renderGraph.AddBlitPass(blit, passName: "VolumetricLightScattering", returnBuilder: true))
        {
            if (resourceData.cameraDepthTexture.IsValid())
                builder.UseTexture(resourceData.cameraDepthTexture);
        }

        resourceData.cameraColor = destination;
    }
}
