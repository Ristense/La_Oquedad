using UnityEngine;

/// <summary>
/// NPC que vive en el centro de una vivienda y da la "mision" de buscar la vivienda vecina.
/// Interaccion diegetica: mirarlo + tecla E, mismo patron que PuertaInteractiva.
///
/// ⚠️ ESCUCHAR = ACEPTAR. La mision NO se toma al apretar E, sino al dejar que el NPC TERMINE
/// de hablar sin alejarte. Si te vas antes, simplemente no lo escuchaste: se corta el dialogo y
/// no queda nada aceptado. No hay popup de si/no — alejarse ES el rechazo.
///
/// ⚠️ El NPC DEDUCE solo en que vivienda vive (por su posicion en el mundo) y le PIDE el dialogo
/// al WorldGenerator. No depende de que nadie le inyecte nada al instanciarlo, asi funciona
/// igual embebido en el prefab de la sala (Centro.Vivienda.prefab) o spawneado por el generador.
/// </summary>
[RequireComponent(typeof(Collider))]
public class NPCHabitante : MonoBehaviour
{
    [Tooltip("Distancia maxima desde la camara para poder empezar a hablarle.")]
    public float alcanceInteraccion = 4f;

    [Tooltip("Distancia a la que se considera que te ALEJASTE mientras te habla (corta el dialogo y no se acepta). Un poco mas holgada que el alcance de interaccion, para que un pasito al costado no cancele.")]
    public float alcanceEscucha = 6f;

    [Tooltip("Dibujar las esferas de alcance en la vista Scene aunque el NPC no este seleccionado. Solo afecta al editor, no al juego.")]
    public bool dibujarAlcancesSiempre = false;

    [Header("Apariencia (opcional)")]
    [Tooltip("Donde se instancia el modelo del habitante. Vacio = como hijo directo de este objeto.")]
    public Transform puntoModelo;
    [Tooltip("Punto (altura del pecho) donde aparece el cuadrito de dialogo mientras habla. " +
             "Vacio = usa este mismo transform (el offsetMundo de DialogoUI hace de altura).")]
    public Transform puntoDialogo;
    [Tooltip("Modelo generico que trae el prefab (el maniqui de prueba). Se APAGA si el habitante de esta vivienda tiene su propio prefabModelo.")]
    public GameObject modeloPlaceholder;

    [Header("Variedad de posicion (opcional)")]
    [Tooltip("Empties dentro del prefab de la sala donde este NPC PODRIA pararse. Si hay 2 o mas, " +
             "elige uno determinista por semilla+vivienda (misma semilla = mismo puesto siempre). " +
             "Vacio = se queda exactamente donde lo dejaste en el prefab.")]
    public Transform[] puestosPosibles;

    [Tooltip("Solo lectura / debug: macro chunk de la vivienda donde vive, deducido de su posicion.")]
    public Vector3Int macroChunkVivienda;

    private bool macroResuelto = false;
    private bool puestoElegido = false;

    // >= 0 mientras te esta contando la mision. Al llegar a ese instante sin haberte alejado,
    // recien ahi se acepta.
    private float tiempoFinEscucha = -1f;

    void Update()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        ResolverMacroChunk();

        float dist = Vector3.Distance(transform.position, cam.transform.position);

        // Va ANTES del early return por distancia: justamente necesitamos detectar cuando el
        // jugador se aleja mientras el NPC habla.
        ActualizarEscucha(dist);

        if (dist > alcanceInteraccion) return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, alcanceInteraccion))
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                bool hablando = tiempoFinEscucha >= 0f;

                // Mientras esta contando la mision no pisamos con el prompt de "[E] Hablar",
                // si no se ve como un parpadeo entre los dos.
                if (!hablando && TooltipManager.Instance != null)
                {
                    TooltipManager.Instance.MostrarTooltip(gameObject, "[E] Hablar");
                }

                if (Input.GetKeyDown(KeyCode.E))
                {
                    Hablar();
                }
            }
        }
    }

    /// <summary>
    /// Corre mientras el NPC esta hablando: si te alejas, corta y no se acepta nada; si te
    /// quedas hasta el final, recien ahi se toma la mision.
    /// </summary>
    private void ActualizarEscucha(float dist)
    {
        if (tiempoFinEscucha < 0f) return;

        if (dist > alcanceEscucha)
        {
            // Te fuiste antes de que terminara: no lo escuchaste, no hay mision.
            tiempoFinEscucha = -1f;
            if (DialogoUI.Instance != null) DialogoUI.Instance.Ocultar();
            return;
        }

        if (Time.time >= tiempoFinEscucha)
        {
            tiempoFinEscucha = -1f;
            if (WorldGenerator.Instance != null)
            {
                WorldGenerator.Instance.OnHablarConVivienda(macroChunkVivienda);
            }
        }
    }

    private void ResolverMacroChunk()
    {
        if (macroResuelto) return;
        if (WorldGenerator.Instance == null) return;

        macroChunkVivienda = WorldGenerator.Instance.MundoAMacroChunk(transform.position);
        macroResuelto = true;

        ElegirPuesto();
        AplicarModeloHabitante();
    }

    /// <summary>
    /// Instancia el modelo propio del habitante que le toco a esta vivienda (si tiene uno) y
    /// apaga el modelo generico del prefab. Asi el mismo prefab de NPC sirve para todos: la
    /// logica es compartida y lo unico que cambia es la malla.
    /// </summary>
    private void AplicarModeloHabitante()
    {
        if (!WorldGenerator.Instance.TryObtenerDatosVivienda(macroChunkVivienda, out DatosVivienda datos)) return;
        if (datos.habitante == null || datos.habitante.prefabModelo == null) return;

        if (modeloPlaceholder != null) modeloPlaceholder.SetActive(false);

        Transform padre = puntoModelo != null ? puntoModelo : transform;
        Instantiate(datos.habitante.prefabModelo, padre.position, padre.rotation, padre);
    }

    /// <summary>
    /// Mueve al NPC a uno de los puestos posibles, elegido determinista por semilla+vivienda:
    /// la misma vivienda en la misma semilla siempre lo pone en el mismo lugar, pero cada
    /// vivienda del mundo lo tiene en un puesto distinto.
    /// Como los puestos son hijos del prefab de la sala, acompañan cualquier rotacion que
    /// tenga ese prefab — no hay que compensar nada a mano.
    /// </summary>
    private void ElegirPuesto()
    {
        if (puestoElegido) return;
        puestoElegido = true;

        if (puestosPosibles == null || puestosPosibles.Length == 0) return;

        int hash = WorldGenerator.Instance.semillaDelMundo
                 + macroChunkVivienda.x * 31517
                 + macroChunkVivienda.y * 60413
                 + macroChunkVivienda.z * 15991;

        // (h % n + n) % n en vez de Mathf.Abs: Abs(int.MinValue) desborda.
        int n = puestosPosibles.Length;
        int idx = (hash % n + n) % n;

        Transform destino = puestosPosibles[idx];
        if (destino == null) return;

        transform.position = destino.position;
        transform.rotation = destino.rotation;
    }

    /// <summary>
    /// Empieza a contar la mision. NO acepta nada todavia — eso lo decide ActualizarEscucha
    /// segun si el jugador se queda hasta el final o se va.
    /// </summary>
    public void Hablar()
    {
        ResolverMacroChunk();

        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.OcultarTooltip();
        }

        string texto = null;
        if (WorldGenerator.Instance != null)
        {
            WorldGenerator.Instance.TryObtenerPistaVivienda(macroChunkVivienda, out texto);
        }
        // Último recurso. Desde que TryObtenerPistaVivienda responde también a las viviendas SIN
        // objetivo, acá solo se llega si el WorldGenerator no existe o si esta vivienda ni siquiera
        // está en registroViviendas — o sea, si el NPC está parado donde no hay vivienda. Se avisa
        // por consola porque si volvés a ver los puntos en pantalla, ESTA es la causa, y sin el log
        // se confunde con el caso de "no tiene a quién buscar", que ya está resuelto.
        if (string.IsNullOrWhiteSpace(texto))
        {
            texto = "...";
            Debug.LogWarning($"[NPCHabitante] '{name}' no obtuvo diálogo para el macro chunk {macroChunkVivienda}. " +
                             "O no hay WorldGenerator, o ahí no hay ninguna vivienda registrada (¿el NPC quedó fuera de una sala tipo 1?).");
        }

        if (DialogoUI.Instance != null)
        {
            Transform ancla = puntoDialogo != null ? puntoDialogo : transform;
            DialogoUI.Instance.Mostrar(texto, ancla);
            tiempoFinEscucha = Time.time + DialogoUI.Instance.duracionVisible;
        }
        else
        {
            Debug.Log($"[NPC] {texto}");
            tiempoFinEscucha = Time.time; // sin UI, se acepta en el proximo frame
        }
    }

    // ---- Gizmos (solo editor, no se ven en el juego) ----

    private void OnDrawGizmos()
    {
        if (dibujarAlcancesSiempre) DibujarAlcances();
    }

    private void OnDrawGizmosSelected()
    {
        if (!dibujarAlcancesSiempre) DibujarAlcances();
    }

    private void DibujarAlcances()
    {
        // Amarillo: desde donde podes empezar a hablarle (aparece el "[E] Hablar").
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, alcanceInteraccion);

        // Cian: hasta donde te podes alejar mientras habla sin que se corte la mision.
        // Si se pinta ROJO es porque quedo mas chica que la de interaccion — configuracion
        // invalida: podrias empezar a hablarle desde un lugar donde ya se considera que te
        // fuiste, y la mision se cancelaria sola al instante.
        bool escuchaInvalida = alcanceEscucha < alcanceInteraccion;
        Gizmos.color = escuchaInvalida
            ? new Color(1f, 0.2f, 0.2f, 0.9f)
            : new Color(0.3f, 0.9f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, alcanceEscucha);
    }
}
