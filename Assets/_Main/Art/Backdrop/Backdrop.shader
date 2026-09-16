Shader "MatchRacers/Backdrop"
{
    Properties
    {
        _BaseShade ("Base Shade", Range(0, 1)) = 0.6
        _TopColor ("Top Color", Color) = (0.35, 0.16, 0.5, 1)
        _TopBlend ("Top Blend", Range(0, 1)) = 0.3
        _TopFalloff ("Top Falloff", Range(1, 16)) = 4
        _WindowColor ("Window Color", Color) = (1, 0.72, 0.4, 1)
        _WindowColorAlt ("Window Color Alt", Color) = (0.45, 0.8, 1, 1)
        _WindowIntensity ("Window Intensity", Range(0, 16)) = 2
        _WindowSize ("Window Size", Vector) = (2.6, 3.4, 0.5, 0.45)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Backdrop"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _BaseShade;
                half4 _TopColor;
                float _TopBlend;
                float _TopFalloff;
                half4 _WindowColor;
                half4 _WindowColorAlt;
                float _WindowIntensity;
                float4 _WindowSize;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 data : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 data : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                output.data = input.data;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float height = saturate(input.data.y);
                half3 color = input.color.rgb * lerp(_BaseShade, 1.0, height);
                color = lerp(color, _TopColor.rgb, _TopBlend * pow(height, _TopFalloff));

                float2 grid = input.uv / max(_WindowSize.xy, 0.01);
                float2 cell = floor(grid);
                float2 local = abs(frac(grid) - 0.5);
                float inside = step(local.x, _WindowSize.z * 0.5) * step(local.y, _WindowSize.w * 0.5);
                float lit = step(Hash(cell + input.data.x * 61.0), input.color.a) * inside * step(1.0, cell.y);
                half3 window = lerp(_WindowColor.rgb, _WindowColorAlt.rgb, step(0.7, Hash(cell.yx + input.data.x * 17.0)));
                color += window * (_WindowIntensity * lit);

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Off
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
