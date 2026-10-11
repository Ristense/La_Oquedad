using UnityEngine;
using TMPro;

/// <summary>
/// Texto unico y global de dialogo (mismo patron que InfoObjetoUI/NotaPanelUI: un solo texto
/// fijo en pantalla, cualquier NPC lo usa, no hay uno por instancia). Es "de un tiro":
/// Mostrar() lo prende con el mensaje y se apaga solo despues de duracionVisible segundos,
/// sin necesidad de mantener ninguna tecla apretada.
///
/// Diegetico: si Mostrar() recibe un ancla, el panel se reposiciona cada frame sobre la
/// proyeccion en pantalla de esa transform + offsetMundo (tipo "cuadrito en el pecho del NPC"),
/// en vez de quedarse fijo en el centro. Sin ancla, se queda donde este puesto en el Canvas.
///
/// Tambien apaga otros elementos del HUD (mira, munion de la tirolina, etc.) mientras se habla,
/// para no competir visualmente con el dialogo, y los reactiva al terminar.
/// </summary>
public class DialogoUI : MonoBehaviour
{
    public static DialogoUI Instance;

    [Tooltip("El TMP_Text que se prende/apaga y muestra el mensaje.")]
    public TMP_Text texto;

    [Tooltip("Segundos que el texto queda visible antes de desaparecer solo.")]
    public float duracionVisible = 4f;

    [Header("Posición diegética (opcional)")]
    [Tooltip("Transform que se mueve para seguir al ancla en pantalla (puede ser el propio panel/fondo " +
             "con el texto adentro como hijo). Si se deja vacío, se usa el RectTransform del propio " +
             "texto. Solo importa si Mostrar() recibe un ancla.")]
    public Transform panel;
    [Tooltip("Offset en metros desde la posición del ancla (ej: altura del pecho del NPC).")]
    public Vector3 offsetMundo = new Vector3(0f, 1.5f, 0f);

    [Header("Ocultar mientras se habla")]
    [Tooltip("Elementos del HUD (mira, munición de la tirolina, etc.) que se apagan al mostrar " +
             "el diálogo y se reactivan al ocultarlo. Solo se tocan los que estaban activos antes.")]
    public GameObject[] ocultarMientrasHabla;

    private float tiempoOcultar = -1f;
    private Transform ancla;
    private Camera cam;
    private bool[] estadoPrevioOcultos;

    /// <summary>El GameObject que en verdad se prende/apaga: el panel (grupo entero: fondo + texto +
    /// lo que le quieras agregar) si hay uno asignado, o el texto solo si no.</summary>
    private GameObject Raiz => panel != null ? panel.gameObject : (texto != null ? texto.gameObject : null);

    void Awake()
    {
        Instance = this;
        if (Raiz != null) Raiz.SetActive(false);
        cam = Camera.main;
    }

    void Update()
    {
        if (tiempoOcultar >= 0f && Time.time >= tiempoOcultar)
        {
            Ocultar();
            return;
        }

        if (ancla != null) ReposicionarSobreAncla();
    }

    /// <summary>Muestra el mensaje fijo en su posición de Canvas de siempre (sin diégesis).</summary>
    public void Mostrar(string mensaje) => Mostrar(mensaje, null);

    /// <summary>Muestra el mensaje. Si se pasa un ancla, el panel sigue su posición en pantalla.</summary>
    public void Mostrar(string mensaje, Transform anclaMundo)
    {
        if (texto != null) texto.text = mensaje;
        if (Raiz != null) Raiz.SetActive(true);
        tiempoOcultar = Time.time + duracionVisible;

        ancla = anclaMundo;
        if (ancla != null)
        {
            if (cam == null) cam = Camera.main;
            Debug.Log($"[DialogoUI] ancla='{ancla.name}' posMundo={ancla.position} offsetMundo={offsetMundo} cam={(cam != null ? cam.name : "NULL")}");
            ReposicionarSobreAncla();
        }

        OcultarHud();
    }

    public void Ocultar()
    {
        if (Raiz != null) Raiz.SetActive(false);
        tiempoOcultar = -1f;
        ancla = null;
        RestaurarHud();
    }

    public bool EstaVisible => Raiz != null && Raiz.activeSelf;

    private void ReposicionarSobreAncla()
    {
        if (cam == null) return;
        Transform destino = panel != null ? panel : (texto != null ? texto.rectTransform : null);
        if (destino == null) return;

        Vector3 pantalla = cam.WorldToScreenPoint(ancla.position + offsetMundo);
        if (pantalla.z < 0f)
        {
            // El ancla quedó detrás de la cámara (te diste vuelta): no tiene sentido proyectarlo.
            return;
        }
        destino.position = pantalla;
    }

    private void OcultarHud()
    {
        if (ocultarMientrasHabla == null) return;
        estadoPrevioOcultos = new bool[ocultarMientrasHabla.Length];
        for (int i = 0; i < ocultarMientrasHabla.Length; i++)
        {
            GameObject go = ocultarMientrasHabla[i];
            if (go == null) continue;
            estadoPrevioOcultos[i] = go.activeSelf;
            go.SetActive(false);
        }
    }

    private void RestaurarHud()
    {
        if (ocultarMientrasHabla == null || estadoPrevioOcultos == null) return;
        for (int i = 0; i < ocultarMientrasHabla.Length; i++)
        {
            GameObject go = ocultarMientrasHabla[i];
            if (go == null) continue;
            go.SetActive(estadoPrevioOcultos[i]);
        }
        estadoPrevioOcultos = null;
    }
}
