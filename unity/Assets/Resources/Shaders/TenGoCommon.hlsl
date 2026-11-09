// Outils communs aux shaders de tenGO.
//
// Couleurs : elles arrivent en sRGB brut (SetVector, sans conversion), comme
// les UIColor de SpriteKit. Le projet est en espace Gamma (ProjectSetup) : le
// mélange se fait donc en sRGB, exactement comme SpriteKit et Compose. Sur les
// fonds crème du jeu, un mélange linéaire éclaircirait les voiles sombres
// (tracé « encre », socles, ombres) de 10 à 30 %.
#ifndef TENGO_COMMON_INCLUDED
#define TENGO_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

// sRGB prémultiplié → espace de sortie du projet.
half3 TenGoOut(half3 srgbPremultiplied)
{
#ifdef UNITY_COLORSPACE_GAMMA
    return srgbPremultiplied;
#else
    return SRGBToLinear(max(srgbPremultiplied, 0));
// Masque de découpe (SKCropNode des pages défilantes) : rectangle en
// coordonnées monde (xmin, ymin, xmax, ymax), bord adouci d'un pixel. Le
// rectangle par défaut (±1e6) ne découpe rien.
float TenGoClipMask(float2 ws, float4 rect)
{
    float2 d = min(ws - rect.xy, rect.zw - ws);
    float m = min(d.x, d.y);
    float aa = max(fwidth(m), 1e-5);
    return saturate(m / aa + 0.5);
}

#endif
}

// Temps d'animation commun (FxTime.Now) : captures et tests reproductibles.
float _TenGoTime;

// Couverture d'une distance signée (négative à l'intérieur), bord adouci
// d'un pixel, ou de `soft` unités (ombres douces).
float TenGoCover(float d, float soft)
{
    float aa = max(fwidth(d), 1e-5);
    float w = max(aa, soft);
    return 1.0 - smoothstep(-w, w, d);
}

// Rectangle arrondi : demi-côtés b, rayon d'angle r.
float TenGoRoundRect(float2 p, float2 b, float r)
{
    r = min(r, min(b.x, b.y));
    float2 q = abs(p) - b + r;
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
}

// Ellipse de demi-axes r (approximation au premier ordre, exacte pour un cercle).
float TenGoEllipse(float2 p, float2 r)
{
    if (abs(r.x - r.y) < 1e-5) return length(p) - r.x;
    float k0 = length(p / r);
    float k1 = length(p / (r * r));
    return k0 * (k0 - 1.0) / max(k1, 1e-5);
}

// Distance au segment [a, b] ; `t` = position projetée (0-1).
float TenGoSegment(float2 p, float2 a, float2 b, out float t)
{
    float2 pa = p - a, ba = b - a;
    t = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-8));
    return length(pa - ba * t);
}

// Masque de découpe (SKCropNode des pages défilantes) : rectangle en
// coordonnées monde (xmin, ymin, xmax, ymax), bord adouci d'un pixel. Le
// rectangle par défaut (±1e6) ne découpe rien.
float TenGoClipMask(float2 ws, float4 rect)
{
    float2 d = min(ws - rect.xy, rect.zw - ws);
    float m = min(d.x, d.y);
    float aa = max(fwidth(m), 1e-5);
    return saturate(m / aa + 0.5);
}

#endif
