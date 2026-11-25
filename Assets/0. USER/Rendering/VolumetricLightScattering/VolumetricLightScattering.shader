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

        TEXTURE2D_X(_OcclusionTex);
        float4 _OcclusionTex_TexelSize;

        float4 _SunParams;  // xy = sun UV, z = sun radius, w = intensity
        float4 _RayParams;  // x = decay, y = exposure, z = samples, w = density
        float4 _VLSColor;
        float4 _DepthFade;  // x = near (unused for source), y = min eye-depth that counts as light source

        // Near geometry = black (occludes). Sky / far = bright emitter.
        // Soft ramp recovers v1-like strength while still letting props cut shafts.
        float LightSourceMask(float rawDepth)
        {
            #if UNITY_REVERSED_Z
            if (rawDepth <= 0.00001)
                return 1.0;
            #else
            if (rawDepth >= 0.99999)
                return 1.0;
            #endif

            float eye = LinearEyeDepth(rawDepth, _ZBufferParams);
            // _DepthFade.x = near (dark), .y = far (full emit)
            return smoothstep(_DepthFade.x, max(_DepthFade.x + 0.01, _DepthFade.y), eye);
        }

        float SunDisk(float2 uv, float2 sunUV, float sunRadius)
        {
            float2 d = uv - sunUV;
            d.x *= _ScreenParams.x / max(1.0, _ScreenParams.y);
            float dist = length(d);
            float w = saturate(1.0 - dist / max(1e-4, sunRadius));
            // Broader falloff so the disk feeds stronger shafts like v1.
            return w * w * (0.35 + 0.65 * w);
        }

        float OcclusionAt(float2 uv)
        {
            float rawDepth = SampleSceneDepth(uv);
            float source = LightSourceMask(rawDepth);
            float disk = SunDisk(uv, _SunParams.xy, max(1e-4, _SunParams.z));
            // Mix: strong sun disk + softer far contribution toward the sun (v1 feel).
            return source * saturate(disk + 0.45 * source * disk);
        }
        ENDHLSL

        // Pass 0: occlusion mask (bright = unoccluded sun/sky disk).
        Pass
        {
            Name "VLS_Occlusion"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragOcclusion

            half4 FragOcclusion(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float o = OcclusionAt(input.texcoord);
                return half4(o, o, o, 1);
            }
            ENDHLSL
        }

        // Pass 1: radial blur the occlusion mask toward the sun, then add shafts.
        Pass
        {
            Name "VLS_Scatter"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragScatter

            half4 FragScatter(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                half3 scene = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;

                float2 sunUV = _SunParams.xy;
                float intensity = _SunParams.w;
                float decay = saturate(_RayParams.x);
                float exposure = _RayParams.y;
                int samples = (int)clamp(_RayParams.z, 4.0, 64.0);
                float density = _RayParams.w;

                float2 delta = (sunUV - uv) * density / samples;
                float2 coord = uv;
                float illumination = 1.0;
                float accum = 0.0;

                // Sample the precomputed occlusion mask along the screen-space ray.
                // Occluders are black in the mask → shafts break behind them.
                [loop]
                for (int i = 0; i < samples; i++)
                {
                    coord += delta;
                    if (coord.x < 0.0 || coord.x > 1.0 || coord.y < 0.0 || coord.y > 1.0)
                        break;

                    float occl = SAMPLE_TEXTURE2D_X_LOD(_OcclusionTex, sampler_LinearClamp, coord, 0).r;
                    accum += occl * illumination;
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
