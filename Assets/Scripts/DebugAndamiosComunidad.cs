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

    private static readonly Color ColorPasillo = new Color(0.2f, 0.85f, 1f);
    private static readonly Color ColorEscalera = new Color(1f, 0.5f, 0.1f);

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
        int tramos = 0, escaleras = 0;
        if (wg.dibujarCaminosAndamio)
        {
            foreach (List<Vector3Int> camino in wg.CaminosComunidad(macro))
            {
                for (int i = 1; i < camino.Count; i++)
                {
                    Vector3 a = wg.CeldaACentroMundoPiso(camino[i - 1]) + Vector3.up * 1f;
                    Vector3 c = wg.CeldaACentroMundoPiso(camino[i]) + Vector3.up * 1f;
                    bool esEscalera = camino[i].y != camino[i - 1].y;
                    if (esEscalera) escaleras++; else tramos++;

                    Color color = esEscalera ? ColorEscalera : ColorPasillo;
                    Vector3 eje = c - a;
                    Crear(PrimitiveType.Cube, (a + c) * 0.5f, new Vector3(1.2f, 1.2f, eje.magnitude),
                          Quaternion.LookRotation(eje, esEscalera ? Vector3.forward : Vector3.up), color,
                          $"{(esEscalera ? "Escalera" : "Pasillo")} {camino[i - 1]} → {camino[i]}");
                }
                Vector3 fin = wg.CeldaACentroMundoPiso(camino[camino.Count - 1]) + Vector3.up * 1f;
                Crear(PrimitiveType.Cube, fin, Vector3.one * 2.5f, Quaternion.identity, ColorPasillo, $"Fin camino {camino[camino.Count - 1]}");
            }
        }
        Debug.Log($"[Andamios] comunidad {macro}: {puntos.Count} puntos · caminos: {tramos} tramos de pasillo, {escaleras} escaleras");
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
