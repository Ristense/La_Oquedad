using TMPro;
using UnityEngine;

/// <summary>
/// Marca flotando sobre la cabeza de un NPC que tiene una misión: "!" cuando la tiene para dar,
/// "?" mientras está en curso, y nada cuando no tiene o ya se completó. Siempre mira a la cámara y
/// sube y baja un poco para que se note de lejos.
///
/// Si no se le asigna un prefab propio, arma solo un texto TextMeshPro con la fuente por defecto.
/// </summary>
public class IndicadorMision : MonoBehaviour
{
    public enum Estado
    {
        Ninguna,
        Disponible,
        EnCurso
    }

    [Tooltip("Prefab opcional para la marca (ícono, modelo). Vacío = un \"!\"/\"?\" de texto.")]
    public GameObject prefabMarca;
    public Color colorDisponible = new Color(1f, 0.85f, 0.1f);
    public Color colorEnCurso = new Color(0.6f, 0.85f, 1f);
    [Tooltip("Metros por encima de la cabeza (el punto más alto de los renderers del NPC).")]
    public float alturaSobreCabeza = 0.5f;
    [Tooltip("Tamaño del texto en metros, aprox.")]
    public float tamano = 0.8f;
    public float amplitudFlote = 0.12f;
    public float velocidadFlote = 2f;

    public Estado EstadoActual { get; private set; } = Estado.Ninguna;

    private Transform marca;
    private TextMeshPro texto;
    private float alturaCabezaLocal;

    // Se calcula al crear la marca y no en Awake: el WorldGenerator instancia a los NPC apagados,
    // y en un objeto inactivo Awake no corre hasta que se prende.
    private void CalcularAlturaCabeza()
    {
        Renderer[] rends = GetComponentsInChildren<Renderer>(true);
        float alto = 1.8f;
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            alto = b.max.y - transform.position.y;
        }
        alturaCabezaLocal = alto + alturaSobreCabeza;
    }

    public void SetEstado(Estado estado)
    {
        EstadoActual = estado;
        if (estado == Estado.Ninguna)
        {
            if (marca != null) marca.gameObject.SetActive(false);
            return;
        }

        if (marca == null) CrearMarca();
        marca.gameObject.SetActive(true);

        if (texto != null)
        {
            texto.text = estado == Estado.Disponible ? "!" : "?";
            texto.color = estado == Estado.Disponible ? colorDisponible : colorEnCurso;
        }
    }

    private void CrearMarca()
    {
        CalcularAlturaCabeza();
        if (prefabMarca != null)
        {
            marca = Instantiate(prefabMarca, transform).transform;
        }
        else
        {
            var go = new GameObject("Indicador Mision");
            go.transform.SetParent(transform, false);
            texto = go.AddComponent<TextMeshPro>();
            texto.alignment = TextAlignmentOptions.Center;
            texto.fontStyle = FontStyles.Bold;
            texto.fontSize = tamano * 10f;
            texto.rectTransform.sizeDelta = new Vector2(2f, 2f);
            marca = go.transform;
        }
        marca.localPosition = Vector3.up * alturaCabezaLocal;
    }

    void LateUpdate()
    {
        if (marca == null || !marca.gameObject.activeSelf) return;

        marca.localPosition = Vector3.up * (alturaCabezaLocal + Mathf.Sin(Time.time * velocidadFlote) * amplitudFlote);

        Camera cam = Camera.main;
        if (cam != null)
            marca.rotation = Quaternion.LookRotation(marca.position - cam.transform.position, Vector3.up);
    }
}
