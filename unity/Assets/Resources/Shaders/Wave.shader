// Vague du thème Océan (ThemeBackground.makeSineNode) : trait de sinusoïde
// y = A·sin(2πx/λ), épaisseur 4, bouts ronds. Distance approchée par
// |y − f(x)| / √(1 + f'(x)²), exacte à mieux qu'un pixel pour ces amplitudes.
Shader "TenGo/Wave"
{
    Properties
    {
        _Color ("Couleur sRGB", Vector) = (1, 1, 1, 0.2)
        _Wave ("Amplitude, longueur d'onde, demi-épaisseur, demi-largeur (unités)", Vector) = (0.16, 2.4, 0.02, 9)
        _Params ("Alpha hérité", Vector) = (1, 0, 0, 0)
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
            Name "Wave"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "TenGoCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _Wave;
                float4 _Params;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 local : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float2 ext = float2(_Wave.w + _Wave.z, _Wave.x + _Wave.z + 0.02);
                float2 local = (v.uv * 2.0 - 1.0) * ext;
                o.positionCS = TransformObjectToHClip(float3(local, 0));
                o.local = local;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float k = 6.2831853 / max(_Wave.y, 1e-4);
                float x = clamp(i.local.x, -_Wave.w, _Wave.w);
                float f = _Wave.x * sin(k * x);
                float slope = _Wave.x * k * cos(k * x);
                float dy = (i.local.y - f) / sqrt(1.0 + slope * slope);
                float d = length(float2(i.local.x - x, dy)) - _Wave.z;
                float a = TenGoCover(d, 0) * _Color.a * _Params.x;
                return half4(TenGoOut(_Color.rgb * a), a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
