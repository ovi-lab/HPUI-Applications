Shader "Custom/URPGradient"
{
    Properties
    {
        _StartColor ("Start Color", Color) = (1,0,0,1)
        _EndColor ("End Color", Color) = (0,0,1,1)
        _GradientAxis ("Gradient Axis", Vector) = (0,1,0,0)
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _StartColor;
                half4 _EndColor;
                float4 _GradientAxis;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 axis = normalize(_GradientAxis.xyz);
                float t = dot(IN.positionWS, axis);
                t = saturate(t);
                
                half4 color = lerp(_StartColor, _EndColor, t);
                return color;
            }
            ENDHLSL
        }
    }
}
