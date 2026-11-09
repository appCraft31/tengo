// Ligne de tracé (TrailRenderer.swift) : distance à une polyligne de 32 points
// au plus, bouts et jonctions arrondis (lineCap/lineJoin .round), pointillés
// facultatifs (copy(dashingWithPhase:lengths:)). Une seule distance pour tout
// le tracé : un trait translucide ne s'assombrit pas aux jonctions, comme un
// SKShapeNode. Le quad couvre la boîte englobante (TrailView.cs).
Shader "TenGo/Trail"
{
    Properties
    {
        _Color ("Couleur sRGB", Vector) = (1, 1, 1, 0.6)
        _Line ("Demi-épaisseur, nombre de points, période et longueur du tiret (unités)", Vector) = (0.03, 0, 0, 0)
        _Params ("Alpha hérité", Vector) = (1, 0, 0, 0)
        _TenGoClip ("Découpe monde (xmin, ymin, xmax, ymax)", Vector) = (-1000000, -1000000, 1000000, 1000000)
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
            Name "Trail"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "TenGoCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _Line;
                float4 _Params;
                float4 _TenGoClip;
            CBUFFER_END
            // x, y (unités locales), longueur cumulée au point.
            float4 _Points[32];

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 local : TEXCOORD0; float2 world : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.local = v.positionOS.xy;
                o.world = TransformObjectToWorld(v.positionOS.xyz).xy;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                int n = (int)(_Line.y + 0.5);
                float halfW = _Line.x;
                float period = _Line.z, dash = _Line.w;
                float best = 1e6;
                if (n == 1) best = length(i.local - _Points[0].xy);
                [loop] for (int k = 0; k + 1 < n; k++)
                {
                    float t;
                    float d = TenGoSegment(i.local, _Points[k].xy, _Points[k + 1].xy, t);
                    if (period > 0)
                    {
                        // Pointillés : distance au tiret le plus proche le long du tracé.
                        float s = lerp(_Points[k].z, _Points[k + 1].z, t);
                        float m = s - floor(s / period) * period;      // position dans la période
                        float along = m <= dash ? 0.0 : min(m - dash, period - m);
                        d = sqrt(d * d + along * along);
                    }
                    best = min(best, d);
                }
                float a = TenGoCover(best - halfW, 0) * _Color.a * _Params.x * TenGoClipMask(i.world, _TenGoClip);
                return half4(TenGoOut(_Color.rgb * a), a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
