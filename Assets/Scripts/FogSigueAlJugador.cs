using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Hace que el rango vertical del Fog (Base Height / Maximum Height) del Volume de HDRP
/// siga al jugador, para que nunca quede fuera de rango al volar/escalar muy alto o bajo
/// en un mundo que no tiene límite vertical fijo. Poné este script en el mismo GameObject
/// que el componente `Volume` (ej. "Sky and Fog Volume").
/// </summary>
[RequireComponent(typeof(Volume))]
public class FogSigueAlJugador : MonoBehaviour
{
    [Tooltip("Jugador a seguir. Si está vacío, se busca por tag 'Player'.")]
    public Transform jugador;

    [Tooltip("Cuánto por debajo del jugador arranca la niebla (Base Height). MANTENER GRANDE (miles de unidades) — un rango angosto genera una línea horizontal fea justo al horizonte (artefacto del Height Fog de HDRP: un rayo mirando al horizonte viaja mucho más lejos DENTRO de la franja que uno mirando para arriba/abajo). La sensación de 'niebla que tapa' se controla con Fog Attenuation Distance en el Fog del profile, NO con este rango.")]
    public float rangoAbajo = 3000f;
    [Tooltip("Cuánto por arriba del jugador termina la niebla (Maximum Height). Ver nota de rangoAbajo — mismo criterio.")]
    public float rangoArriba = 3000f;

    [Tooltip("Solo actualiza si el jugador se movió más de esto en Y desde la última vez (evita tocar el profile todo el tiempo por nada).")]
    public float umbralMovimiento = 5f;

    private Volume volume;
    private Fog fog;
    private float ultimaY;
    private bool inicializado;

    void Awake()
    {
        volume = GetComponent<Volume>();
    }

    void Start()
    {
        if (jugador == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) jugador = p.transform;
        }

        // El profile compartido (asset) NO se toca directo: se clona una instancia propia
        // en runtime (patrón estándar de HDRP para modificar un Volume sin pisar el asset).
        if (volume.profile != null)
        {
            volume.profile = Instantiate(volume.profile);
        }

        if (volume.profile == null || !volume.profile.TryGet(out fog))
        {
            Debug.LogWarning("[FogSigueAlJugador] El Volume no tiene un override de Fog en su profile.");
            enabled = false;
        }
    }

    void Update()
    {
        if (jugador == null || fog == null) return;

        float y = jugador.position.y;
        if (inicializado && Mathf.Abs(y - ultimaY) < umbralMovimiento) return;

        fog.baseHeight.value = y - rangoAbajo;
        fog.maximumHeight.value = y + rangoArriba;

        ultimaY = y;
        inicializado = true;
    }
}
