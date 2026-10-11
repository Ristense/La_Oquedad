using UnityEngine;

/// <summary>
/// Herramienta de diagnóstico: apuntá a la cosa sospechosa y apretá F9. Loguea QUÉ estás
/// mirando (nombre, jerarquía completa, distancia) y cuántos triángulos/renderers hay en
/// su raíz — para cazar ese objeto misterioso que congela el juego al mirarlo.
/// Ponelo en cualquier GameObject de la escena (la cámara misma sirve). Borralo cuando
/// termine la cacería.
/// </summary>
public class DebugQueEstoyMirando : MonoBehaviour
{
    public KeyCode tecla = KeyCode.F9;
    [Tooltip("Alcance del rayo (m). Largo a propósito: la cosa está 'lejísimo'.")]
    public float alcance = 10000f;

    void Update()
    {
        if (!Input.GetKeyDown(tecla)) return;

        Camera cam = Camera.main;
        if (cam == null) { Debug.LogWarning("[QueMiro] No hay Camera.main"); return; }

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, alcance, ~0, QueryTriggerInteraction.Ignore))
        {
            Debug.Log("[QueMiro] El rayo no pegó en nada (¿el objeto no tiene collider? Probá acercarte o usá el Frame Debugger).");
            return;
        }

        // Jerarquía completa: de la raíz al objeto pegado
        string ruta = hit.collider.name;
        Transform t = hit.collider.transform.parent;
        while (t != null) { ruta = t.name + "/" + ruta; t = t.parent; }

        // Contar triángulos y renderers bajo la RAÍZ del objeto golpeado
        Transform raiz = hit.collider.transform.root;
        long tris = 0; int renderers = 0; int luces = 0;
        foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>(true))
            if (mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
        renderers = raiz.GetComponentsInChildren<Renderer>(true).Length;
        luces = raiz.GetComponentsInChildren<Light>(true).Length;

        Debug.Log($"[QueMiro] '{ruta}' · dist {hit.distance:F0}m · raíz '{raiz.name}': {tris:N0} tris, {renderers} renderers, {luces} luces");
    }
}
