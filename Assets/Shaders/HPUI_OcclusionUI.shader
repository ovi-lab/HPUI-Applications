Shader "HPUI/OcclusionUI"
{
    // HPUI interface shader with hand-occlusion translucency.
    //
    // Stereo VR compatible — uses URP's SAMPLE_DEPTH_TEXTURE_PROJ macro which
    // correctly handles Texture2DArray depth textures in single-pass instanced VR.
    //
    // Sliders:
    //   _BehindHandOpacity: 0 = hard cull when behind hand, 1 = fully visible behind hand
    //   _OcclusionBias:     depth bias (meters) to prevent self-occlusion
    //   _OcclusionSoftness: transition zone (meters)
    //   _OcclusionDebug:    toggle red/green debug heatmap

    Properties
    {
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [MainColor]   _BaseColor("Color", Color) = (1, 1, 1, 1)

        [Header(Occlusion)]
        _BehindHandOpacity ("Behind-Hand Opacity",     Range(0, 1)) = 0.3
        _OcclusionBias    ("Occlusion Bias (m)",       Float)       = 0.002
        _OcclusionSoftness("Occlusion Edge Softness (m)", Float)    = 0.003
        [Toggle] _OcclusionDebug("Debug Mode (red=occluded, green=visible)", Float) = 0

        [Header(Visual Tweaks)]
        _RimPower  ("Rim Light Power", Range(0, 5))    = 1.5
        _RimColor  ("Rim Light Color", Color)          = (0.3, 0.3, 0.5, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always   // Don't let Z-test cull occluded fragments;
                            // the shader handles translucency via depth sampling
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Camera depth texture — TEXTURE2D_X_FLOAT handles stereo VR
            // (Texture2DArray in single-pass instanced) automatically when
            // combined with UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX.
            TEXTURE2D_X_FLOAT(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _BehindHandOpacity;
                float  _OcclusionBias;
                float  _OcclusionSoftness;
                float  _OcclusionDebug;
                half   _RimPower;
                half4  _RimColor;
            CBUFFER_END

            // ───── Vertex / fragment structs with stereo support ─────
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID   // required for single-pass instanced VR
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float3 viewDirWS  : TEXCOORD3;
                float4 screenPos  : TEXCOORD4;    // for depth texture sampling in VR
                UNITY_VERTEX_OUTPUT_STEREO        // required for single-pass instanced VR
            };

            // ───── Vertex shader ─────
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs   normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS   = normInputs.normalWS;
                output.viewDirWS  = GetWorldSpaceViewDir(posInputs.positionWS);
                output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);

                // ComputeScreenPos produces per-eye-correct screen UVs for VR.
                // SAMPLE_DEPTH_TEXTURE_PROJ + UNITY_PROJ_COORD will use this
                // to pick the correct array slice in single-pass instanced.
                output.screenPos  = ComputeScreenPos(output.positionCS);

                return output;
            }

            // ───── Fragment shader ─────
            half4 frag(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // ── 1. Base color ──
                half4 texColor  = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 baseColor = texColor * _BaseColor;

                // ── 2. Simple lighting ──
                Light mainLight = GetMainLight();
                half3 lightColor = mainLight.color;
                half3 lightDir   = mainLight.direction;
                half3 normalWS   = normalize(input.normalWS);
                half3 viewDirWS  = normalize(input.viewDirWS);

                half NdotL   = dot(normalWS, lightDir);
                half diffuse = saturate(NdotL * 0.5 + 0.5);
                half3 ambient  = half3(0.25, 0.25, 0.3);
                half3 litColor = baseColor.rgb * (ambient + lightColor * diffuse * 0.8);

                // ── 3. Rim light ──
                half NdotV = saturate(dot(normalWS, viewDirWS));
                half rim   = pow(1.0 - NdotV, _RimPower);
                litColor  += rim * _RimColor.rgb * _RimColor.a;

                // ── 4. Depth-based occlusion ──
                // ComputeScreenPos + SAMPLE_TEXTURE2D_X correctly handle:
                //  - Per-eye viewport UVs in stereo VR
                //  - Texture2DArray slice selection via UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneRawDepth    = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r;
                float sceneLinearDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
                float surfaceDepth     = input.positionCS.w;

                // Positive = scene is farther than UI (UI in front → visible)
                // Negative = scene is closer than UI (hand occluding → translucent)
                float depthDiff = sceneLinearDepth - surfaceDepth;

                // Clamp to avoid far-plane data triggering false occlusion
                depthDiff = clamp(depthDiff, -1.0, 1.0);

                // smoothstep: 0 when (depthDiff + bias) ≤ 0 (hand occluding)
                //             1 when (depthDiff + bias) ≥ softness (UI clearly in front)
                // occlusion: 0 = hand in front, 1 = UI in front
                float occlusion = smoothstep(0, _OcclusionSoftness, depthDiff + _OcclusionBias);

                // ── 5. Final alpha ──
                half finalAlpha = lerp(_BehindHandOpacity * baseColor.a, baseColor.a, occlusion);
                finalAlpha = min(finalAlpha, baseColor.a);

                // ── Debug heatmap ──
                if (_OcclusionDebug > 0.5)
                {
                    half3 debugColor = lerp(half3(1, 0, 0), half3(0, 1, 0), occlusion);
                    return half4(debugColor, 1.0);
                }

                return half4(litColor, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}