using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cada cierto tiempo random, busca la tubería real (PuntoTuberia) más cercana al jugador dentro
/// de un radio y se teletransporta ahí para disparar un chorro de vapor + sonido — el "susto" de
/// una tubería que suena de golpe. Mismo patrón de timer que SistemaDesprendimientos: espera
/// random, tira los dados, reprograma siempre (se dispare o no), para no sentirse un metrónomo.
///
/// Es UN SOLO emisor compartido que viaja de tubería en tubería, no un componente por tubería —
/// así el costo es constante sin importar cuántas tuberías tenga el mundo.
/// </summary>
public class SustoVapor : MonoBehaviour
{
    [Header("Referencias")]
    public Transform jugador;

    [Header("Frecuencia")]
    [Tooltip("Segundos mínimos entre intentos de susto.")]
    public float intervaloMin = 40f;
    [Tooltip("Segundos máximos entre intentos de susto.")]
    public float intervaloMax = 120f;
    [Tooltip("Gracia (s) desde que el MUNDO queda listo hasta el primer intento — para que no te reciba un susto en el primer paso de la partida.")]
    public float esperaInicialMin = 60f;
    public float esperaInicialMax = 180f;
    [Range(0f, 1f)]
    [Tooltip("Probabilidad de que, al vencer el temporizador, el susto REALMENTE dispare. Abajo de 1 el ritmo deja de sentirse regular — a veces pasan dos seguidos, a veces un silencio largo.")]
    public float probabilidadSusto = 0.5f;

    [Header("Búsqueda")]
    [Tooltip("Radio (m) alrededor del jugador donde se busca una tubería real. Sin chequeo de paredes/línea de visión: si hay una tubería registrada dentro del radio, es candidata.")]
    public float radioBusqueda = 15f;

    [Header("Efecto")]
    [Tooltip("Particle System del vapor. Se llama Play() al disparar — además de sus propias ráfagas periódicas si las tiene, esto fuerza una AHORA en la tubería elegida.")]
    public ParticleSystem particulas;
    public AudioSource fuenteSonido;
    [Tooltip("Se elige uno al azar en cada susto, mismo criterio que el resto del proyecto (pasos, tirolina).")]
    public AudioClip[] sonidosSusto;
    [Range(0f, 1f)] public float volumenSonido = 0.85f;
    [Tooltip("Cuánto dura la partícula si por lo que sea no hay sonido (array vacío, clip null, Fuente Sonido sin asignar). Evita que quede sonando/corriendo para siempre por falta de sonido.")]
    public float duracionSustoSinSonido = 2f;

    [Header("Debug")]
    public bool logDebug = false;
    [Tooltip("Tecla para forzar un susto YA, sin esperar el timer ni tirar la probabilidad — para probar sin quedarte 40-120s esperando. Se salta ambos rolls, pero sigue necesitando una tubería registrada dentro del radio (si no hay, el log de siempre te va a avisar que no encontró nada).")]
    public KeyCode teclaTest = KeyCode.Alpha3;

    private float temporizador;
    private float proximoEvento;
    private bool mundoEstabaListo;

    void Start()
    {
        BuscarReferencias();
    }

    private void BuscarReferencias()
    {
        if (jugador == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) jugador = p.transform;
        }
        if (fuenteSonido == null) fuenteSonido = GetComponent<AudioSource>();
        if (fuenteSonido == null)
        {
            fuenteSonido = gameObject.AddComponent<AudioSource>();
            fuenteSonido.playOnAwake = false;
            fuenteSonido.spatialBlend = 1f; // 3D: al teletransportarse, el sonido tiene que venir de donde está la tubería
        }
    }

    void Update()
    {
        if (jugador == null || WorldGenerator.Instance == null)
        {
            BuscarReferencias();
            return;
        }

        // Trigger manual: se salta el timer Y la probabilidad, va derecho a Disparar(). Reinicia
        // el reloj normal para que no se sumen un susto forzado y uno natural pegados uno al otro.
        if (Input.GetKeyDown(teclaTest))
        {
            Debug.Log("[SustoVapor] Trigger manual (tecla de test) — forzando susto.");
            temporizador = 0f;
            proximoEvento = Random.Range(intervaloMin, intervaloMax);
            Disparar();
        }

        // Mismo criterio que SistemaDesprendimientos: el reloj no corre hasta que el mundo está
        // listo, y se reinicia en la transición — así el rato de menú/intro no cuenta como espera
        // y no te recibe un susto en el primer segundo de partida.
        bool listo = WorldGenerator.Instance.MundoListo;
        if (listo != mundoEstabaListo)
        {
            mundoEstabaListo = listo;
            if (listo)
            {
                temporizador = 0f;
                proximoEvento = Random.Range(esperaInicialMin, esperaInicialMax);
                if (logDebug) Debug.Log($"[SustoVapor] Mundo listo — primer intento en {proximoEvento:F0}s.");
            }
        }
        if (!listo) return;

        temporizador += Time.deltaTime;
        if (temporizador < proximoEvento) return;

        // Reprogramar SIEMPRE, dispare o no: un intento fallido consume su turno igual, si no el
        // ritmo deja de ser random y se vuelve predecible.
        temporizador = 0f;
        proximoEvento = Random.Range(intervaloMin, intervaloMax);

        if (Random.value > probabilidadSusto)
        {
            if (logDebug) Debug.Log("[SustoVapor] Turno salteado por probabilidad.");
            return;
        }

        Disparar();
    }

    private void Disparar()
    {
        PuntoTuberia elegido = BuscarTuberiaCercana();
        if (elegido == null)
        {
            if (logDebug) Debug.Log("[SustoVapor] No hay ninguna tubería registrada dentro del radio — se salta.");
            return;
        }

        // RESET forzado antes de mover: si el susto anterior quedó pegado (Looping, o el corte
        // automático nunca se agendó por lo que sea), esto lo apaga y limpia YA, para que nunca
        // arrastre partículas viejas a la posición nueva. StopEmittingAndClear en vez de Stop()
        // normal: acá sí queremos un corte de golpe, no un apagado gradual, porque estamos por
        // reposicionar — dejar partículas vivas "terminando" en el lugar viejo se ve mal.
        if (particulas != null) particulas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        CancelInvoke(nameof(DetenerParticulas));

        transform.position = elegido.transform.position;
        transform.rotation = elegido.transform.rotation;

        if (particulas != null) particulas.Play();

        // La partícula dura lo mismo que el sonido elegido. ⚠️ El corte SIEMPRE se agenda, con o
        // sin sonido — antes solo se agendaba adentro del "if (clip != null)", así que un hueco
        // vacío en el array (o fuenteSonido sin asignar) dejaba la partícula sonando para siempre,
        // porque el CancelInvoke de arriba cancelaba el corte anterior sin que nada lo reemplazara.
        float duracion = duracionSustoSinSonido;
        if (fuenteSonido != null && sonidosSusto != null && sonidosSusto.Length > 0)
        {
            AudioClip clip = sonidosSusto[Random.Range(0, sonidosSusto.Length)];
            if (clip != null)
            {
                fuenteSonido.PlayOneShot(clip, volumenSonido);
                duracion = clip.length;
            }
        }
        if (particulas != null) Invoke(nameof(DetenerParticulas), duracion);

        // SIEMPRE visible (no atado a logDebug): es el evento que importa confirmar al probar por
        // primera vez, sin depender de acordarse de tildar el checkbox antes de entrar a Play.
        Debug.Log($"[SustoVapor] ¡SUSTO! Disparado en tubería {elegido.name} · pos {elegido.transform.position}"
            + $" · partículas={(particulas != null ? "OK" : "FALTA ASIGNAR")}"
            + $" · sonido={(fuenteSonido != null && sonidosSusto != null && sonidosSusto.Length > 0 ? "OK" : "FALTA ASIGNAR")}");
    }

    /// <summary>
    /// Corta la partícula al terminar el sonido. `Stop()` (sin StopEmittingAndClear) deja
    /// terminar naturalmente a las partículas que ya volaron — un corte de golpe se vería más
    /// artificial que dejarlas apagarse solas los últimos instantes.
    /// </summary>
    private void DetenerParticulas()
    {
        if (particulas != null) particulas.Stop();
    }

    // Reusada en cada búsqueda para no generar basura de GC en cada intento del timer.
    private readonly List<PuntoTuberia> candidatasEnRadio = new List<PuntoTuberia>();

    /// <summary>
    /// Una PuntoTuberia AL AZAR entre TODAS las que están dentro de radioBusqueda (no siempre la
    /// más cercana — con "siempre la más cercana", parado en el mismo lugar te toca la misma
    /// tubería cada vez, se siente repetitivo/predecible). Distancia SOLO HORIZONTAL (ignora Y) —
    /// las tuberías corren cerca del techo, así que estar parado justo debajo puede significar
    /// 15+ metros de distancia vertical con cellSize=20.
    /// </summary>
    private PuntoTuberia BuscarTuberiaCercana()
    {
        candidatasEnRadio.Clear();
        float radioSqr = radioBusqueda * radioBusqueda;
        Vector2 posJugadorXZ = new Vector2(jugador.position.x, jugador.position.z);

        foreach (PuntoTuberia punto in PuntoTuberia.Todos)
        {
            if (punto == null) continue;
            Vector2 posPuntoXZ = new Vector2(punto.transform.position.x, punto.transform.position.z);
            float d = (posPuntoXZ - posJugadorXZ).sqrMagnitude;
            if (d < radioSqr) candidatasEnRadio.Add(punto);
        }

        if (candidatasEnRadio.Count == 0) return null;
        return candidatasEnRadio[Random.Range(0, candidatasEnRadio.Count)];
    }
}
