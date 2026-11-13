using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Debug en Game del trazado que calcula WorldGenerator para las tuberías alrededor del jugador.
/// Celeste = fitting asignado, verde = brazos llegan al centro sin fitting, amarillo = probabilidad,
/// naranjo = cierre y morado = final del recorrido.
/// </summary>
public class DebugTuberiasModulares : MonoBehaviour
{
    private static readonly Color ColorGenera = new Color(0.1f, 0.9f, 1f);
    private static readonly Color ColorSinFitting = new Color(0.45f, 1f, 0.2f);
    private static readonly Color ColorProbabilidad = new Color(1f, 0.85f, 0.05f);
    private static readonly Color ColorCierre = new Color(1f, 0.42f, 0.05f);
    private static readonly Color ColorFinal = new Color(0.8f, 0.35f, 1f);

    private WorldGenerator wg;
    private readonly List<GameObject> dibujos = new List<GameObject>();
    private Material material;
    private Vector3Int celdaDibujada;
    private bool hayDibujo;

    void Awake() => wg = GetComponent<WorldGenerator>();

    void Update()
    {
        Camera cam = Camera.main;
        if (wg == null || cam == null || !wg.dibujarDebugTuberias)
        {
            Limpiar();
            return;
        }

        Vector3Int jugador = wg.MundoACelda(cam.transform.position);
        if (hayDibujo && jugador == celdaDibujada) return;

        Limpiar();
        celdaDibujada = jugador;
        hayDibujo = true;

        // Las tuberías solo se generan en ly=1 de cada macro chunk.
        int macroY = wg.CeldaAMacroChunk(jugador).y;
        int y = macroY * wg.macroSize + 1;
        int radio = Mathf.Max(1, wg.radioDebugTuberias);
        int genera = 0, sinFitting = 0, fallaProbabilidad = 0, cierres = 0, finales = 0;

        for (int x = jugador.x - radio; x <= jugador.x + radio; x++)
            for (int z = jugador.z - radio; z <= jugador.z + radio; z++)
            {
                var info = wg.DiagnosticarTuberia(new Vector3Int(x, y, z));
                if (info.salidas == 0) continue;

                Color color;
                switch (info.estado)
                {
                    case WorldGenerator.EstadoDebugTuberia.Genera:
                        color = ColorGenera;
                        genera++;
                        break;
                    case WorldGenerator.EstadoDebugTuberia.Probabilidad:
                        color = ColorProbabilidad;
                        fallaProbabilidad++;
                        break;
                    case WorldGenerator.EstadoDebugTuberia.SinFittingT:
                    case WorldGenerator.EstadoDebugTuberia.SinFittingCruce:
                        color = ColorSinFitting;
                        sinFitting++;
                        break;
                    case WorldGenerator.EstadoDebugTuberia.Cierre:
                        color = ColorCierre;
                        cierres++;
                        break;
                    case WorldGenerator.EstadoDebugTuberia.PocasSalidas:
                        color = ColorFinal;
                        finales++;
                        break;
                    default:
                        continue;
                }

                DibujarSalida(info.centro, info.bordeEste, info.este, color, $"{x},{y},{z} E");
                DibujarSalida(info.centro, info.bordeOeste, info.oeste, color, $"{x},{y},{z} O");
                DibujarSalida(info.centro, info.bordeNorte, info.norte, color, $"{x},{y},{z} N");
                DibujarSalida(info.centro, info.bordeSur, info.sur, color, $"{x},{y},{z} S");
                Crear(PrimitiveType.Sphere, info.centro, Vector3.one * 0.8f, Quaternion.identity,
                    color, $"Unión tubería {x},{y},{z}: {info.estado}");
            }

        Debug.Log($"[DebugTuberias] celda jugador {jugador} · se generan: {genera} · uniones sin fitting: {sinFitting} · probabilidad: {fallaProbabilidad} · cierres: {cierres} · finales: {finales}");
    }

    private void DibujarSalida(Vector3 centro, Vector3 borde, bool abierta, Color color, string nombre)
    {
        if (!abierta) return;
        Vector3 delta = borde - centro;
        Crear(PrimitiveType.Cube, (centro + borde) * 0.5f,
            new Vector3(0.35f, 0.35f, delta.magnitude), Quaternion.LookRotation(delta), color,
            "Ruta tubería " + nombre);
    }

    private void Crear(PrimitiveType tipo, Vector3 posicion, Vector3 escala, Quaternion rotacion, Color color, string nombre)
    {
        GameObject go = GameObject.CreatePrimitive(tipo);
        Collider col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        go.name = nombre;
        go.transform.SetPositionAndRotation(posicion, rotacion);
        go.transform.localScale = escala;

        Renderer r = go.GetComponent<Renderer>();
        if (material == null)
        {
            Shader shader = Shader.Find("HDRP/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            material = new Material(shader);
        }
        var bloque = new MaterialPropertyBlock();
        bloque.SetColor("_UnlitColor", color);
        bloque.SetColor("_BaseColor", color);
        bloque.SetColor("_Color", color);
        r.SetPropertyBlock(bloque);
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        dibujos.Add(go);
    }

    private void Limpiar()
    {
        foreach (GameObject go in dibujos)
            if (go != null) Destroy(go);
        dibujos.Clear();
        hayDibujo = false;
    }

    void OnDisable() => Limpiar();
}
