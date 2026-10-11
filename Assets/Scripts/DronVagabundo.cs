using UnityEngine;

/// <summary>
/// Dron que vaga por el exterior (Abismo / tipo 7) por su cuenta — NO está atado al jugador.
/// Cada dron elige waypoints en una esfera alrededor de su PROPIA posición, evita
/// estructuras (linecast), y ocasionalmente se acerca a una pared para "trabajar"
/// (queda quieto unos segundos con un efecto opcional encendido).
/// </summary>
public class DronVagabundo : MonoBehaviour
{
    [Header("Movimiento")]
    [Tooltip("Velocidad mínima del dron (se elige aleatoria entre min y max al spawnear). u/s")]
    public float velocidadMin = 5f;
    [Tooltip("Velocidad máxima del dron. u/s")]
    public float velocidadMax = 12f;
    [Tooltip("Aceleración hacia el waypoint (u/s²).")]
    public float aceleracion = 3f;
    [Tooltip("Distancia al waypoint para considerarlo alcanzado.")]
    public float distanciaLlegada = 3f;

    [Header("Waypoint (esfera alrededor del propio dron)")]
    [Tooltip("Distancia máxima del waypoint respecto de la posición actual del dron.")]
    public float radioVagar = 40f;
    [Tooltip("Rango Y permitido relativo a la posición actual.")]
    public float rangoY = 20f;
    [Tooltip("Cuántos intentos hace por waypoint antes de rendirse y quedarse esperando.")]
    public int intentosWaypoint = 12;
    [Tooltip("Capas que cuentan como obstáculo (paredes, pisos, techos).")]
    public LayerMask capasObstaculo = ~0;

    [Header("Estado Trabajar (se acerca a una pared)")]
    [Tooltip("Probabilidad (0-1) de que al llegar a un waypoint, el dron busque una pared cercana y se acerque a 'trabajar'.")]
    [Range(0f, 1f)]
    public float chanceTrabajar = 0.25f;
    public float tiempoTrabajarMin = 4f;
    public float tiempoTrabajarMax = 12f;
    [Tooltip("Distancia desde la pared a la que se posiciona el dron mientras 'trabaja'.")]
    public float distanciaPared = 2f;
    [Tooltip("GameObject opcional que se activa mientras trabaja (ej. partícula de chispas como hijo).")]
    public GameObject efectoTrabajando;

    [Header("Audio")]
    [Tooltip("AudioSource del zumbido constante (loop). Volumen/distancias se configuran en el AudioSource.")]
    public AudioSource audioZumbido;
    [Tooltip("AudioSource del sonido de trabajo. Volumen/distancias se configuran en el AudioSource. Solo suena en estado Trabajar.")]
    public AudioSource audioTrabajar;


    private enum Estado { Vagar, Trabajar }
    private Estado estado = Estado.Vagar;
    private Vector3 waypoint;
    private Vector3 velocidad;
    private float velocidadElegida;
    private float tiempoTrabajarRestante;
    private Vector3 paredNormal = Vector3.up;

    void Start()
    {
        velocidadElegida = Random.Range(velocidadMin, velocidadMax);
        if (efectoTrabajando != null) efectoTrabajando.SetActive(false);

        // Audio: solo forzamos spatialBlend=1 y loop (esencial para 3D ambiental).
        if (audioZumbido != null)
        {
            audioZumbido.spatialBlend = 1f;
            audioZumbido.loop = true;
            audioZumbido.mute = false;
            audioZumbido.rolloffMode = AudioRolloffMode.Linear;
            audioZumbido.minDistance = 1f;
            audioZumbido.maxDistance = 15f;
            if (!audioZumbido.isPlaying) audioZumbido.Play();
        }

        if (audioTrabajar != null)
        {
            audioTrabajar.spatialBlend = 1f;
            audioTrabajar.loop = true;
            audioTrabajar.rolloffMode = AudioRolloffMode.Linear;
            audioTrabajar.minDistance = 1f;
            audioTrabajar.maxDistance = 15f;
            audioTrabajar.Stop();
        }

        ElegirNuevoWaypoint();
    }

    void Update()
    {
        if (estado == Estado.Trabajar)
        {
            tiempoTrabajarRestante -= Time.deltaTime;

            // Mientras trabaja, orientación hacia la pared
            if (paredNormal.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(-paredNormal, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 2f * Time.deltaTime);
            }

            if (tiempoTrabajarRestante <= 0f)
            {
                if (efectoTrabajando != null) efectoTrabajando.SetActive(false);
                // Audio: parar el de trabajo, retomar el zumbido
                if (audioTrabajar != null) audioTrabajar.Stop();
                if (audioZumbido != null) { audioZumbido.mute = false; if (!audioZumbido.isPlaying) audioZumbido.Play(); }
                estado = Estado.Vagar;
                ElegirNuevoWaypoint();
            }
            return;
        }

        // VAGAR
        Vector3 deseado = waypoint - transform.position;
        float dist = deseado.magnitude;
        Vector3 dirDeseada = (dist > 0.01f) ? deseado / dist : Vector3.zero;
        Vector3 velocidadObjetivo = dirDeseada * velocidadElegida;
        velocidad = Vector3.MoveTowards(velocidad, velocidadObjetivo, aceleracion * Time.deltaTime);
        transform.position += velocidad * Time.deltaTime;

        if (velocidad.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(velocidad.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 4f * Time.deltaTime);
        }

        if (dist < distanciaLlegada)
        {
            // ¿Llegó a un punto de trabajo? Entonces entra en estado Trabajar
            if (modoTrabajarPendiente)
            {
                modoTrabajarPendiente = false;
                estado = Estado.Trabajar;
                tiempoTrabajarRestante = Random.Range(tiempoTrabajarMin, tiempoTrabajarMax);
                velocidad = Vector3.zero;
                if (efectoTrabajando != null) efectoTrabajando.SetActive(true);
                // Audio: silenciar zumbido y arrancar el de trabajo
                if (audioZumbido != null) audioZumbido.mute = true;
                if (audioTrabajar != null && !audioTrabajar.isPlaying) audioTrabajar.Play();
            }
            else
            {
                ElegirNuevoWaypoint();
            }
        }
    }

    private bool modoTrabajarPendiente = false;

    private void ElegirNuevoWaypoint()
    {
        // Tirada para ver si este nuevo punto es de "trabajo" (cerca de pared) o de vagar
        if (Random.value <= chanceTrabajar && BuscarWaypointJuntoAPared(out Vector3 wpTrabajo, out Vector3 normal))
        {
            waypoint = wpTrabajo;
            paredNormal = normal;
            modoTrabajarPendiente = true;
            return;
        }

        for (int i = 0; i < intentosWaypoint; i++)
        {
            Vector3 candidato = PuntoRandomEnEsferaPropia();
            if (!EsExterior(candidato)) continue;
            if (Physics.Linecast(transform.position, candidato, capasObstaculo, QueryTriggerInteraction.Ignore)) continue;

            waypoint = candidato;
            modoTrabajarPendiente = false;
            return;
        }

        // Sin waypoint válido: quedarse esperando un frame
        waypoint = transform.position + transform.forward * 1f;
        modoTrabajarPendiente = false;
    }

    private bool BuscarWaypointJuntoAPared(out Vector3 wp, out Vector3 normal)
    {
        // Lanza varios rays en direcciones horizontales aleatorias. Si alguno golpea pared, usa ese.
        for (int i = 0; i < intentosWaypoint; i++)
        {
            Vector2 h = Random.insideUnitCircle.normalized;
            Vector3 dir = new Vector3(h.x, Random.Range(-0.2f, 0.2f), h.y).normalized;
            if (Physics.Raycast(transform.position, dir, out RaycastHit hit, radioVagar, capasObstaculo, QueryTriggerInteraction.Ignore))
            {
                wp = hit.point + hit.normal * distanciaPared;
                normal = hit.normal;
                return true;
            }
        }
        wp = Vector3.zero; normal = Vector3.up;
        return false;
    }

    private Vector3 PuntoRandomEnEsferaPropia()
    {
        Vector2 h = Random.insideUnitCircle.normalized;
        float r = Random.Range(radioVagar * 0.3f, radioVagar);
        float y = Random.Range(-rangoY, rangoY);
        return transform.position + new Vector3(h.x * r, y, h.y * r);
    }

    private bool EsExterior(Vector3 pos)
    {
        // Exterior = sin techo encima en los próximos 10m y sin paredes inmediatas
        if (Physics.CheckSphere(pos, 2f, capasObstaculo, QueryTriggerInteraction.Ignore)) return false;
        if (Physics.Raycast(pos, Vector3.up, 10f, capasObstaculo, QueryTriggerInteraction.Ignore)) return false;
        return true;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = (estado == Estado.Trabajar) ? Color.yellow : Color.cyan;
        Gizmos.DrawSphere(waypoint, 0.6f);
        Gizmos.DrawLine(transform.position, waypoint);
    }
}
