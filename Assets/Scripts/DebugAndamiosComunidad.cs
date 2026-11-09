using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SOLO DEBUG. Dibuja en el Game los puntos de andamio (WorldGenerator.PuntosAndamioComunidad)
/// de la comunidad donde está el jugador: una esfera pegada a la pared y un palito que apunta hacia
/// adentro de la sala. Color por piso de la grilla. Al cambiar de comunidad se redibuja; fuera de
/// una comunidad no se ve nada. Lo agrega solo el WorldGenerator; se apaga con su toggle
/// dibujarPuntosAndamio.
/// </summary>
public class DebugAndamiosComunidad : MonoBehaviour
{
    private static readonly Color[] ColoresPorPiso =
    {
        Color.white, Color.white, new Color(1f, 0.85f, 0.1f), new Color(0.2f, 1f, 0.4f), new Color(1f, 0.3f, 0.9f)
    };

    private WorldGenerator wg;
    private readonly List<GameObject> marcas = new List<GameObject>();
    private Vector3Int macroDibujado;
    private bool hayDibujo;
    private Material material;

    void Awake() => wg = GetComponent<WorldGenerator>();

    void Update()
    {
        Camera cam = Camera.main;
        if (wg == null || cam == null || !wg.dibujarPuntosAndamio) { Limpiar(); return; }

        Vector3Int macro = wg.MundoAMacroChunk(cam.transform.position);
        if (hayDibujo && macro == macroDibujado) return;

        Limpiar();
        macroDibujado = macro;
        hayDibujo = true;

        List<WorldGenerator.PuntoAndamio> puntos = wg.PuntosAndamioComunidad(macro);
        foreach (WorldGenerator.PuntoAndamio p in puntos)
        {
            int ly = ((p.celda.y % wg.macroSize) + wg.macroSize) % wg.macroSize;
            Color color = ColoresPorPiso[Mathf.Clamp(ly, 0, ColoresPorPiso.Length - 1)];

            Vector3 pos = p.posicion + p.normal * 1.5f + Vector3.up * 1f;
            Crear(PrimitiveType.Sphere, pos, Vector3.one * 3f, Quaternion.identity, color, $"Andamio piso {ly} · {p.celda}");
            Crear(PrimitiveType.Cube, pos + p.normal * 3f, new Vector3(0.4f, 0.4f, 6f),
                  Quaternion.LookRotation(p.normal, Vector3.up), color, "normal");
        }
        Debug.Log($"[Andamios] comunidad {macro}: {puntos.Count} puntos");
    }

    private void Crear(PrimitiveType tipo, Vector3 pos, Vector3 escala, Quaternion rot, Color color, string nombre)
    {
        GameObject go = GameObject.CreatePrimitive(tipo);
        Destroy(go.GetComponent<Collider>());
        go.name = nombre;
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = escala;

        Renderer r = go.GetComponent<Renderer>();
        if (material == null)
        {
            Shader sh = Shader.Find("HDRP/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            material = new Material(sh);
        }
        var bloque = new MaterialPropertyBlock();
        bloque.SetColor("_UnlitColor", color);
        bloque.SetColor("_BaseColor", color);
        bloque.SetColor("_Color", color);
        r.SetPropertyBlock(bloque);
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        marcas.Add(go);
    }

    private void Limpiar()
    {
        foreach (GameObject go in marcas) if (go != null) Destroy(go);
        marcas.Clear();
        hayDibujo = false;
    }

    void OnDisable() => Limpiar();
}
