// Cercle néon en SDF : contour net + halo exponentiel + remplissage,
// sans post-traitement. Dessiné sur un quad de demi-côté _Extent (unités
// locales) dont les UV vont de -1 à 1 ; GlowCircle règle les propriétés par
// MaterialPropertyBlock. Mélange prémultiplié : le contour et le remplissage
// couvrent le fond, le halo s'y ajoute (alpha nul) comme une lumière.
Shader "TenGo/GlowCircle"
{
    Properties
    {
        _Color ("Couleur", Color) = (0.369, 0.922, 0.831, 1)
        _Radius ("Rayon (unités)", Float) = 0.5
        _Thickness ("Épaisseur du contour (unités)", Float) = 0.04
        _GlowIntensity ("Intensité du halo", Float) = 1
        _GlowRange ("Portée du halo (unités)", Float) = 0.3
        _Fill ("Remplissage (0-1)", Range(0, 1)) = 0
        _CoreWhite ("Cœur blanchi du contour (0-1)", Range(0, 1)) = 0.35
        _Extent ("Demi-côté du quad (unités)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            // Sans LightMode : passe « SRPDefaultUnlit », dessinée par le
            // renderer 2D comme par le renderer universel.
            Name "GlowCircle"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Radius;
                float _Thickness;
                float _GlowIntensity;
                float _GlowRange;
                float _Fill;
                float _CoreWhite;
                float _Extent;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 local : TEXCOORD0; // position locale en unités, centre = 0
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float2 p = v.uv * 2.0 - 1.0;
                float2 local = p * _Extent;
                o.positionCS = TransformObjectToHClip(float3(local, 0));
                o.local = local;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float d = length(i.local) - _Radius;           // distance signée au cercle
                float aa = max(fwidth(d), 1e-5);              // anticrénelage d'un pixel
                float half_t = max(_Thickness * 0.5, 0.0);
                float ringD = abs(d) - half_t;                 // distance au contour
                float ring = 1.0 - smoothstep(-aa, aa, ringD);
                float inside = 1.0 - smoothstep(-aa, aa, d);
                float fill = saturate(_Fill) * inside * (1.0 - ring);

                // Halo : décroissance exponentielle depuis le bord du contour,
                // ramenée à zéro en douceur à _GlowRange.
                float e = max(ringD, 0.0);
                float range = max(_GlowRange, 1e-4);
                float falloff = exp(-3.0 * e / range) * saturate(1.0 - e / range);
                // L'intérieur luit moins que l'extérieur (le remplissage le porte).
                float side = d < 0.0 ? 0.55 : 1.0;
                float glow = _GlowIntensity * falloff * side * (1.0 - ring);

                half3 col = _Color.rgb;
                half3 core = lerp(col, half3(1, 1, 1), _CoreWhite);
                half a = saturate(ring + fill) * _Color.a;
                half3 rgb = (core * ring + col * fill) * _Color.a + col * glow * _Color.a;
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
