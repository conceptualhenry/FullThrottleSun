Shader "Hidden/FullThrottle/VolumetricLightScattering"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        float4 _SunParams;  // xy = sun UV, z = sun radius, w = intensity
        float4 _RayParams;  // x = decay, y = exposure, z = samples, w = density
        float4 _VLSColor;
        float4 _DepthFade;  // x = near fade start, y = far fade end (eye-space meters)

        float FarMask(float rawDepth)
        {
            // Sky / missing depth → full contribute.
            #if UNITY_REVERSED_Z
            if (rawDepth <= 0.00001)
                return 1.0;
            #else
            if (rawDepth >= 0.99999)
                return 1.0;
            #endif

            float eye = LinearEyeDepth(rawDepth, _ZBufferParams);
            return smoothstep(_DepthFade.x, _DepthFade.y, eye);
        }

        float SunWeight(float2 uv, float2 sunUV, float sunRadius)
        {
            float2 d = uv - sunUV;
            d.x *= _ScreenParams.x / max(1.0, _ScreenParams.y);
            float dist = length(d);
            float w = saturate(1.0 - dist / max(1e-4, sunRadius));
            return w * w;
        }
        ENDHLSL

        // Screen-space volumetric light scattering (depth-aware Tyndall shafts).
        Pass
        {
            Name "VolumetricLightScattering"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragVLS

            half4 FragVLS(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                half3 scene = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;

                float2 sunUV = _SunParams.xy;
                float sunRadius = max(1e-4, _SunParams.z);
                float intensity = _SunParams.w;
                float decay = saturate(_RayParams.x);
                float exposure = _RayParams.y;
                int samples = (int)clamp(_RayParams.z, 4.0, 64.0);
                float density = _RayParams.w;

                float2 delta = (sunUV - uv) * density / samples;
                float2 coord = uv;
                float illumination = 1.0;
                float accum = 0.0;

                [loop]
                for (int i = 0; i < samples; i++)
                {
                    coord += delta;
                    if (coord.x < 0.0 || coord.x > 1.0 || coord.y < 0.0 || coord.y > 1.0)
                        break;

                    float rawDepth = SampleSceneDepth(coord);
                    float farMask = FarMask(rawDepth);
                    float sun = SunWeight(coord, sunUV, sunRadius);
                    accum += farMask * sun * illumination;
                    illumination *= decay;
                }

                float shafts = accum * exposure * intensity / samples;
                half3 rays = _VLSColor.rgb * shafts;
                return half4(scene + rays, 1);
            }
            ENDHLSL
        }
    }
}
