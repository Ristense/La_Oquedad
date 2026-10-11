using UnityEngine;
using TMPro;

/// <summary>
/// Panel UI que muestra el contenido de una NotaData mientras el jugador la sostiene
/// y mantiene Click Der. Se asigna al Canvas del jugador (Screen Space - Camera).
/// </summary>
public class NotaPanelUI : MonoBehaviour
{
    [Tooltip("GameObject raíz del panel (se activa/desactiva). Si es null, se usa este GameObject.")]
    public GameObject panelRoot;
    public TMP_Text txtTitulo;
    public TMP_Text txtContenido;
    public TMP_Text txtAutor;

    void Awake()
    {
        if (panelRoot == null) panelRoot = gameObject;
        panelRoot.SetActive(false);
    }

    public void Mostrar(NotaData data)
    {
        if (data == null || panelRoot == null) return;
        if (txtTitulo != null)    txtTitulo.text    = data.titulo;

        string contenidoProcesado = data.contenido;
        if (contenidoProcesado != null && (contenidoProcesado.Contains("{COORDS}") || contenidoProcesado.Contains("{SALA}")))
        {
            PlayerData pd = FindAnyObjectByType<PlayerData>();
            if (pd != null && WorldGenerator.Instance != null)
            {
                // Semilla determinista combinando el título de la nota con la semilla del mundo
                int seed = data.titulo.GetHashCode() + WorldGenerator.Instance.semillaDelMundo;
                if (WorldGenerator.Instance.ObtenerSalaAleatoriaDeterminista(pd.transform.position, 300f, seed, out string nombreSala, out string coords))
                {
                    contenidoProcesado = contenidoProcesado.Replace("{COORDS}", coords);
                    contenidoProcesado = contenidoProcesado.Replace("{SALA}", nombreSala);
                }
            }
        }

        // Misión encadenada: SIEMPRE mostrar la celda destino y la celda actual del jugador.
        // Mostrar() se llama cada frame mientras se lee (Click Der mantenido en LectorNotas),
        // así que la coordenada actual se refresca en vivo mientras el panel está abierto.
        if (data.esMisionEncadenada && WorldGenerator.Instance != null
            && WorldGenerator.Instance.ObtenerDestinoMision(data, out Vector3Int destino))
        {
            PlayerData pdMision = FindAnyObjectByType<PlayerData>();
            string actual = pdMision != null
                ? $"{pdMision.coordenadaGrilla.x}, {pdMision.coordenadaGrilla.y}, {pdMision.coordenadaGrilla.z}"
                : "?, ?, ?";
            contenidoProcesado += $"\n\n>> DESTINO: Celda {destino.x}, {destino.y}, {destino.z}" +
                                  $"\n>> ACTUAL:  Celda {actual}";
        }

        if (txtContenido != null) txtContenido.text = contenidoProcesado;
        if (txtAutor != null)     txtAutor.text     = data.autor;
        panelRoot.SetActive(true);
    }

    public void Ocultar()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }
}
