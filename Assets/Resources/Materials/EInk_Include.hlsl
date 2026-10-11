#ifndef EINK_EFFECT_INCLUDED
#define EINK_EFFECT_INCLUDED

static const float EInk_Bayer4x4[16] = {
    0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
    12.0/16.0, 4.0/16.0, 14.0/16.0,  6.0/16.0,
    3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
    15.0/16.0, 7.0/16.0, 13.0/16.0,  5.0/16.0
};

float EInk_Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float EInk_DitherAt(float2 pixelCoord)
{
    int x = (int)fmod(abs(pixelCoord.x), 4.0);
    int y = (int)fmod(abs(pixelCoord.y), 4.0);
    return EInk_Bayer4x4[y * 4 + x];
}

// UV              : 0..1 UV del panel
// ScreenPixels     : resolucion de la grilla e-ink falsa (ej. 96,64). Mas alto = menos pixelado.
// Luminance        : 0..1, que tan "clara" es esa zona antes de cuantizar (podes meter un Simple Noise grande para manchas viejas, o 0.5 fijo para panel plano)
// Time             : tiempo (nodo Time > Time)
// BoneColor        : color hueso claro (papel viejo)
// InkColor         : color tinta oscura
// DitherLevels     : cantidad de escalones de gris del e-ink (3-4 = look autentico)
// AgeGrain         : 0..1 cantidad de grano/mancha de vejez sutil
// GlitchSpeed      : franjas de glitch por segundo aprox
// GlitchAmount     : 0..1 que tan seguido/visible es el glitch (usar valores chicos, 0.05-0.15, para que sea "super chico")
void EInkEffect_float(
    float2 UV,
    float2 ScreenPixels,
    float Luminance,
    float Time,
    float3 BoneColor,
    float3 InkColor,
    float DitherLevels,
    float AgeGrain,
    float GlitchSpeed,
    float GlitchAmount,
    out float3 Color,
    out float GlitchMask)
{
    float2 pixelCoord = floor(UV * ScreenPixels);
    float2 pixUV = pixelCoord / ScreenPixels;

    // grano de vejez: manchas suaves de baja frecuencia sobre la luminancia
    float grain = (EInk_Hash21(pixUV * 13.7) - 0.5) * AgeGrain;
    float lum = saturate(Luminance + grain);

    float dither = EInk_DitherAt(pixelCoord);
    float levels = max(DitherLevels, 1.0);
    float steppedLum = saturate(floor(lum * levels + dither) / levels);

    // glitch: franjas horizontales finas, muy poco frecuentes, corrimiento de 1 pixel
    float rowSeed = floor(pixelCoord.y / 3.0);
    float timeBucket = floor(Time * GlitchSpeed);
    float trigger = EInk_Hash21(float2(rowSeed, timeBucket));
    float bandActive = step(1.0 - saturate(GlitchAmount) * 0.05, trigger);

    float shiftHash = EInk_Hash21(float2(timeBucket, rowSeed + 0.5));
    float2 shiftedCoord = pixelCoord;
    shiftedCoord.x += bandActive * ((shiftHash > 0.5) ? 1.0 : -1.0);

    float ditherShifted = EInk_DitherAt(shiftedCoord);
    float glitchLum = saturate(floor(lum * levels + ditherShifted) / levels);
    steppedLum = lerp(steppedLum, glitchLum, bandActive);

    GlitchMask = bandActive;
    Color = lerp(InkColor, BoneColor, steppedLum);
}

#endif
