using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class GenerarTexturaVidrioGastado
{
    private const int Ancho = 400;
    private const int Alto = 600;
    private const string CarpetaSalida = "Assets/Materials/Texturas";
    private const string NombreArchivo = "VidrioGastadoBorde.png";

    [MenuItem("Tools/In Side Us/Generar Textura Vidrio Gastado")]
    public static void Generar()
    {
        var rng = new System.Random(Environment.TickCount);
        var pixeles = new Color[Ancho * Alto];

        // Tinte base de vidrio grueso, muy sutil, parejo en toda la superficie
        const float alphaBase = 12f / 255f;
        var colorVidrio = new Color(0.90f, 0.93f, 0.96f);

        const float margenBorde = 46f;

        for (int y = 0; y < Alto; y++)
        {
            for (int x = 0; x < Ancho; x++)
            {
                float distBorde = Mathf.Min(x, Ancho - 1 - x, y, Alto - 1 - y);

                float alpha = alphaBase;

                if (distBorde < margenBorde)
                {
                    float t = 1f - (distBorde / margenBorde);
                    float aumentoBorde = Mathf.Pow(t, 1.6f) * (110f / 255f);
                    alpha += aumentoBorde;
                }

                if (distBorde < 4f)
                {
                    alpha += ((4f - distBorde) / 4f) * (90f / 255f);
                }

                pixeles[y * Ancho + x] = new Color(colorVidrio.r, colorVidrio.g, colorVidrio.b, Mathf.Clamp01(alpha));
            }
        }

        // Rayones finos, solo dentro del margen de borde
        int cantidadRayones = 55;
        for (int i = 0; i < cantidadRayones; i++)
        {
            Vector2 inicio = PuntoAleatorioEnBorde(rng, margenBorde);
            float angulo = (float)(rng.NextDouble() * Mathf.PI * 2f);
            float largo = 10f + (float)rng.NextDouble() * 30f;
            Vector2 fin = inicio + new Vector2(Mathf.Cos(angulo), Mathf.Sin(angulo)) * largo;
            float intensidad = 130f + (float)rng.NextDouble() * 90f;
            DibujarLinea(pixeles, inicio, fin, intensidad / 255f, 0.9f + (float)rng.NextDouble() * 0.6f);
        }

        // Mellas chicas (picaduras/chips), solo en el margen de borde
        int cantidadMellas = 30;
        for (int i = 0; i < cantidadMellas; i++)
        {
            Vector2 centro = PuntoAleatorioEnBorde(rng, margenBorde);
            float radio = 2f + (float)rng.NextDouble() * 4.5f;
            float intensidad = 90f + (float)rng.NextDouble() * 100f;
            DibujarMancha(pixeles, centro, radio, intensidad / 255f, new Color(0.75f, 0.78f, 0.80f));
        }

        // Manchas/velado suave, mas grandes y difusas, tambien solo en el borde
        int cantidadManchas = 8;
        for (int i = 0; i < cantidadManchas; i++)
        {
            Vector2 centro = PuntoAleatorioEnBorde(rng, margenBorde * 0.8f);
            float radio = 14f + (float)rng.NextDouble() * 20f;
            float intensidad = 10f + (float)rng.NextDouble() * 18f;
            DibujarMancha(pixeles, centro, radio, intensidad / 255f, colorVidrio);
        }

        var tex = new Texture2D(Ancho, Alto, TextureFormat.RGBA32, false, false);
        tex.SetPixels(pixeles);
        tex.Apply();

        byte[] png = tex.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(tex);

        if (!Directory.Exists(CarpetaSalida))
        {
            Directory.CreateDirectory(CarpetaSalida);
        }

        string rutaCompleta = Path.Combine(CarpetaSalida, NombreArchivo);
        File.WriteAllBytes(rutaCompleta, png);

        AssetDatabase.Refresh();

        var importer = AssetImporter.GetAtPath(rutaCompleta) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        Debug.Log("Textura generada en: " + rutaCompleta);
    }

    private static Vector2 PuntoAleatorioEnBorde(System.Random rng, float margen)
    {
        // Elige un punto uniforme dentro de la franja de borde (0..margen desde el filo)
        int lado = rng.Next(4);
        float a = (float)rng.NextDouble() * Ancho;
        float b = (float)rng.NextDouble() * margen;
        switch (lado)
        {
            case 0: return new Vector2((float)rng.NextDouble() * Ancho, b);
            case 1: return new Vector2((float)rng.NextDouble() * Ancho, Alto - 1 - b);
            case 2: return new Vector2(b, (float)rng.NextDouble() * Alto);
            default: return new Vector2(Ancho - 1 - b, (float)rng.NextDouble() * Alto);
        }
    }

    private static void DibujarLinea(Color[] pixeles, Vector2 a, Vector2 b, float alpha, float grosor)
    {
        int pasos = Mathf.CeilToInt(Vector2.Distance(a, b)) + 1;
        for (int i = 0; i <= pasos; i++)
        {
            float t = i / (float)pasos;
            Vector2 p = Vector2.Lerp(a, b, t);
            DibujarMancha(pixeles, p, grosor, alpha, new Color(0.95f, 0.96f, 0.97f));
        }
    }

    private static void DibujarMancha(Color[] pixeles, Vector2 centro, float radio, float alphaMax, Color color)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(centro.x - radio));
        int maxX = Mathf.Min(Ancho - 1, Mathf.CeilToInt(centro.x + radio));
        int minY = Mathf.Max(0, Mathf.FloorToInt(centro.y - radio));
        int maxY = Mathf.Min(Alto - 1, Mathf.CeilToInt(centro.y + radio));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), centro);
                if (dist > radio) continue;

                float falloff = 1f - (dist / radio);
                float alpha = falloff * alphaMax;

                int idx = y * Ancho + x;
                Color actual = pixeles[idx];
                float nuevaAlpha = Mathf.Clamp01(actual.a + alpha);
                Color mezclada = Color.Lerp(actual, color, alpha);
                mezclada.a = nuevaAlpha;
                pixeles[idx] = mezclada;
            }
        }
    }
}
