// Forme vectorielle de SpriteKit (SKShapeNode) en SDF : rectangle arrondi (et
// cercle), ellipse, disque doux (FXTextures.dot) ou anneau (FXTextures.ring).
// Remplissage + contour centré sur le tracé (lineWidth), bord adouci d'un
// pixel ou flou (ombre douce). Quad agrandi dans le vertex shader à _Extent ;
// réglages par MaterialPropertyBlock (ShapeView.cs).
Shader "TenGo/Shape"
{
    Properties
    {
        _FillColor ("Remplissage sRGB", Vector) = (1, 1, 1, 1)
        _StrokeColor ("Contour sRGB", Vector) = (0, 0, 0, 0)
        _Box ("Demi-largeur, demi-hauteur, rayon d'angle, épaisseur (unités)", Vector) = (0.5, 0.5, 0.5, 0)
        _Params ("Type, flou (unités), additif, alpha hérité", Vector) = (0, 0, 0, 1)
        _Extent ("Demi-côtés du quad (unités)", Vector) = (0.6, 0.6, 0, 0)
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
            Name "Shape"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "TenGoCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _FillColor;
                float4 _StrokeColor;
                float4 _Box;
                float4 _Params;
                float4 _Extent;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 local : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float2 local = (v.uv * 2.0 - 1.0) * _Extent.xy;
                o.positionCS = TransformObjectToHClip(float3(local, 0));
                o.local = local;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                int kind = (int)(_Params.x + 0.5);
                float soft = _Params.y;
                float inherited = _Params.w;
                float2 b = _Box.xy;
                half4 fill = _FillColor;
                half4 stroke = _StrokeColor;
                float strokeW = _Box.w;

                if (kind == 2)
                {
                    // Disque doux : dégradé radial 1 → 0,85 (55 %) → 0 (FXTextures.dot).
                    float x = length(i.local) / max(b.x, 1e-5);
                    float a = x < 0.55 ? lerp(1.0, 0.85, x / 0.55) : lerp(0.85, 0.0, saturate((x - 0.55) / 0.45));
                    a *= fill.a * inherited;
                    return half4(TenGoOut(fill.rgb * a), _Params.z > 0.5 ? 0 : a);
                }

                float d;
                if (kind == 3)
                {
                    // Anneau de FXTextures.ring : 128 px, trait de 6 px.
                    d = length(i.local) - b.x * (61.0 / 64.0);
                    strokeW = b.x * (6.0 / 64.0);
                    stroke = fill;
                    fill = half4(0, 0, 0, 0);
                }
                else if (kind == 1) d = TenGoEllipse(i.local, b);
                else d = TenGoRoundRect(i.local, b, _Box.z);

                float inside = TenGoCover(d, soft);
                float ring = strokeW > 0 ? TenGoCover(abs(d) - strokeW * 0.5, soft) : 0.0;
                half fa = saturate(fill.a * inside);
                half sa = saturate(stroke.a * ring);
                half3 premult = stroke.rgb * sa + fill.rgb * fa * (1 - sa);
                half a = sa + fa * (1 - sa);
                premult *= inherited;
                a *= inherited;
                return half4(TenGoOut(premult), _Params.z > 0.5 ? 0 : a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
