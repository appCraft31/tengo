// Icône vectorielle (VectorIcon.swift) : champ de distance calculé à
// l'exécution depuis les tracés SVG (IconLibrary.cs), trait de 2 unités SVG,
// teinté. Net à toute taille d'affichage raisonnable (≤ 4× la résolution du champ).
Shader "TenGo/IconSdf"
{
    Properties
    {
        _MainTex ("Champ de distance", 2D) = "white" {}
        _Color ("Couleur sRGB", Vector) = (0.35, 0.33, 0.31, 1)
        _Sdf ("Portée du champ, demi-épaisseur (unités SVG), demi-côté du quad (unités), alpha hérité", Vector) = (4, 1, 0.2, 1)
        _TenGoClip ("Découpe monde (xmin, ymin, xmax, ymax)", Vector) = (-1000000, -1000000, 1000000, 1000000)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "IconSdf"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "TenGoCommon.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _Sdf;
                float4 _TenGoClip;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 world : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float2 local = (v.uv * 2.0 - 1.0) * _Sdf.z;
                o.positionCS = TransformObjectToHClip(float3(local, 0));
                o.uv = v.uv;
                o.world = TransformObjectToWorld(float3(local, 0)).xy;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float raw = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).r;
                float d = (raw * 2.0 - 1.0) * _Sdf.x - _Sdf.y;   // unités SVG, négatif dans le trait
                float a = TenGoCover(d, 0) * _Color.a * _Sdf.w * TenGoClipMask(i.world, _TenGoClip);
                return half4(TenGoOut(_Color.rgb * a), a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
