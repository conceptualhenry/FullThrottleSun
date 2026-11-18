using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP Renderer Feature: depth-aware screen-space volumetric light scattering
/// (Tyndall / crepuscular rays). Add this to PC_Renderer.
/// </summary>
public sealed class VolumetricLightScatteringFeature : ScriptableRendererFeature
{
    [Serializable]
    public class Settings
    {
        [Range(0f, 8f)] public float intensity = 2.2f;
        [Range(0.01f, 1.5f)] public float sunRadius = 0.35f;
        [Range(0.5f, 1f)] public float decay = 0.92f;
        [Range(0.1f, 4f)] public float exposure = 1.1f;
        [Range(8, 64)] public int sampleCount = 32;
        [Range(0.2f, 2f)] public float density = 0.95f;
        [Range(0.2f, 1.5f)] public float falloff = 0.85f;
        [Tooltip("Eye-space meters: closer than this contributes little (car interior).")]
        public float depthNear = 8f;
        [Tooltip("Eye-space meters: farther than this fully contributes (distant view / sky).")]
        public float depthFar = 40f;
        public Color color = new Color(1f, 0.93f, 0.75f, 1f);
        [Tooltip("Only apply shafts while the camera is inside Car/SunZone.")]
        public bool requireSunZone = true;
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    [SerializeField] Settings settings = new Settings();
    [SerializeField] Shader shader;

    Material material;
    VolumetricLightScatteringPass pass;

    public override void Create()
    {
        if (shader == null)
            shader = Shader.Find("Hidden/FullThrottle/VolumetricLightScattering");

        if (shader == null)
        {
            pass = null;
            return;
        }

        if (material == null || material.shader != shader)
            material = CoreUtils.CreateEngineMaterial(shader);

        pass = new VolumetricLightScatteringPass(material);
        pass.renderPassEvent = settings != null
            ? settings.renderPassEvent
            : RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (pass == null || material == null)
            return;

        CameraType camType = renderingData.cameraData.cameraType;
        if (camType != CameraType.Game && camType != CameraType.VR)
            return;

        pass.renderPassEvent = settings.renderPassEvent;
        pass.Setup(settings, renderingData.cameraData.camera);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
        material = null;
        pass = null;
    }
}
