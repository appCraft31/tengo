// Lots de disques doux (éclats de PopEffects, confettis de WinCelebration) :
// un quad par particule, couleur sRGB et alpha par sommet, profil de
// FXTextures.dot calculé ici. Additif ou normal selon _Params.x.
Shader "TenGo/Dots"
{
    Properties
    {
        _Params ("Additif, alpha hérité", Vector) = (0, 1, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Dots"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "TenGoCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Params;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 p : TEXCOORD0; float4 color : COLOR; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.p = v.uv * 2.0 - 1.0;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float x = length(i.p);
                float prof = x < 0.55 ? lerp(1.0, 0.85, x / 0.55) : lerp(0.85, 0.0, saturate((x - 0.55) / 0.45));
                half a = saturate(i.color.a * prof * _Params.y);
                return half4(TenGoOut(i.color.rgb * a), _Params.x > 0.5 ? 0 : a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
