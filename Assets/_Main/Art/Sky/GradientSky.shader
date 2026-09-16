Shader "MatchRacers/Gradient Sky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.05, 0.06, 0.14, 1)
        _HorizonColor ("Horizon", Color) = (0.45, 0.2, 0.5, 1)
        _BottomColor ("Bottom", Color) = (0.03, 0.03, 0.05, 1)
        _HorizonHeight ("Horizon Height", Range(-0.5, 0.5)) = 0
        _TopFalloff ("Top Falloff", Range(0.1, 8)) = 1.5
        _BottomFalloff ("Bottom Falloff", Range(0.1, 8)) = 3
        _HorizonGlow ("Horizon Glow", Range(0, 8)) = 1
        _HorizonGlowWidth ("Horizon Glow Width", Range(1, 64)) = 12
        _SunColor ("Sun Color", Color) = (1, 0.6, 0.3, 1)
        _SunDirection ("Sun Direction", Vector) = (0, 0.1, 1, 0)
        _SunSize ("Sun Size", Range(1, 4096)) = 600
        _SunHalo ("Sun Halo", Range(1, 256)) = 12
        _SunIntensity ("Sun Intensity", Range(0, 16)) = 2
        _Exposure ("Exposure", Range(0, 8)) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
                float _HorizonHeight;
                float _TopFalloff;
                float _BottomFalloff;
                float _HorizonGlow;
                float _HorizonGlowWidth;
                half4 _SunColor;
                float4 _SunDirection;
                float _SunSize;
                float _SunHalo;
                float _SunIntensity;
                float _Exposure;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float height = direction.y - _HorizonHeight;

                float above = pow(saturate(height), 1.0 / _TopFalloff);
                float below = pow(saturate(-height), 1.0 / _BottomFalloff);
                half3 color = height >= 0.0
                    ? lerp(_HorizonColor.rgb, _TopColor.rgb, above)
                    : lerp(_HorizonColor.rgb, _BottomColor.rgb, below);

                color += _HorizonColor.rgb * (_HorizonGlow * exp(-abs(height) * _HorizonGlowWidth));

                float3 sunDirection = normalize(_SunDirection.xyz + float3(0.0, 1e-4, 0.0));
                float sun = saturate(dot(direction, sunDirection));
                color += _SunColor.rgb * _SunIntensity * (pow(sun, _SunSize) * 4.0 + pow(sun, _SunHalo) * 0.35);

                return half4(color * _Exposure, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
