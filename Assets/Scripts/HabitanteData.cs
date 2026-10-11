using UnityEngine;

/// <summary>
/// Identidad de un habitante de vivienda (el NPC). Mismo patron que RoomData: se crean assets
/// en Resources/NPCData/ y el WorldGenerator elige uno por vivienda de forma determinista
/// (hash de semilla + macro chunk), asi cada vivienda tiene su propia gente sin repetir siempre
/// el mismo dialogo.
///
/// El id de la vivienda NO se guarda aca: ya lo es su coordenada de macro chunk (unica en todo
/// el mundo). Este asset es la PLANTILLA compartida; los datos de cada instancia viven en
/// DatosVivienda.
/// </summary>
[CreateAssetMenu(fileName = "NuevoHabitante", menuName = "Mundo/HabitanteData")]
public class HabitanteData : ScriptableObject
{
    [Header("Identificación")]
    public string nombre = "Habitante";
    [Tooltip("Peso relativo de aparicion respecto a los demas habitantes (mismo criterio que RoomData.pesoAparicion): con A=10 y B=1, 1 de cada 11 viviendas tiene a B.")]
    [Min(0f)]
    public float pesoAparicion = 1f;

    [Header("Apariencia")]
    [Tooltip("Modelo de ESTE personaje (solo lo visual: malla/FBX, sin scripts). Lo instancia el NPCHabitante como hijo suyo, asi cada habitante se ve distinto pero la logica de hablar es siempre la misma. Vacio = se queda con el modelo que ya tenga el prefab del NPC.")]
    public GameObject prefabModelo;

    [Header("Diálogo")]
    [Tooltip("Lo que dice ANTES de la pista, para darle personalidad. Se elige una al azar (determinista por vivienda). Vacio = va directo a la pista.")]
    [TextArea(2, 4)]
    public string[] saludos;

    [Tooltip("Como pide que busques la otra vivienda. Usa {DIR} donde quieras que se meta la direccion cardinal (NORTE, SURESTE, etc). Se elige una al azar. Vacio = usa el texto por defecto del generador.")]
    [TextArea(2, 4)]
    public string[] peticiones;

    [Tooltip("Lo que dice la PRIMERA vez que le hablás después de conectar su vivienda con la otra. Se elige una al azar.")]
    [TextArea(2, 4)]
    public string[] agradecimientos;

    [Tooltip("Lo que dice si le volvés a hablar DESPUÉS de esa primera vez (ya te agradeció, ahora es charla de relleno). Vacío = repite el agradecimiento de siempre.")]
    [TextArea(2, 4)]
    public string[] charlaPosMision;

    [Tooltip("Lo que dice cuando NO tiene ninguna vivienda que pedirte buscar — pasa cuando no hay otra vivienda válida en el radio de búsqueda (ej. una isla que salió con el cuartel y una sola casa).\n\nNo es un caso de error: una vivienda realmente aislada es parte de la ficción del juego, y esta frase es su contenido. Vacío = usa el texto por defecto del generador.")]
    [TextArea(2, 4)]
    public string[] frasesSinObjetivo;

    /// <summary>Elige una frase del array de forma determinista segun el hash dado.</summary>
    public static string ElegirFrase(string[] frases, int hash)
    {
        if (frases == null || frases.Length == 0) return null;
        int n = frases.Length;
        // (h % n + n) % n en vez de Mathf.Abs: Abs(int.MinValue) desborda.
        return frases[(hash % n + n) % n];
    }
}
