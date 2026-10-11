using UnityEngine;

/// <summary>
/// Datos de UNA instancia de vivienda en el mundo (no confundir con RoomData, que es la
/// plantilla compartida por tipo de sala). Se registra por coordenada de macro chunk, que
/// ya es un id unico natural (cada macro chunk existe una sola vez en el mundo).
/// </summary>
[System.Serializable]
public class DatosVivienda
{
    public Vector3Int macroChunk;

    // Hacia que otro macro chunk "cree" esta vivienda que puede estar la vecina que busca.
    // No esta garantizado que el jugador la haya encontrado todavia.
    public Vector3Int macroChunkBuscado;

    // Si BuscarViviendaCercana no encontro ninguna, esta vivienda no tiene a quien buscar.
    // Hace falta el flag porque Vector3Int.zero es un macro chunk VALIDO (el del spawn), asi
    // que macroChunkBuscado por si solo no distingue "sin objetivo" de "busca al (0,0,0)".
    public bool tieneObjetivo = false;

    // True apenas el jugador le habla al NPC de ESTA vivienda (leer = aceptar, sin popup).
    // Mientras sea false, esta vivienda todavia no "entrego" su pedido, asi que aunque el
    // jugador llegue a macroChunkBuscado no se completa nada.
    public bool misionAceptada = false;

    // Quien vive aca (nombre + frases). Elegido determinista por semilla+macroChunk al
    // registrarse. Puede ser null si no hay ningun HabitanteData en Resources/Habitantes/.
    public HabitanteData habitante;

    public bool conectada = false;
    public Vector3Int conectadaCon;

    // True apenas el jugador ESCUCHA (no solo lee) el agradecimiento por primera vez tras
    // conectarse. Distingue "primera vez que te agradece" de "ya te agradeció, esto es charla
    // de relleno" en TryObtenerPistaVivienda — sin esto, el NPC repetiría el mismo agradecimiento
    // cada vez que le hablás para siempre.
    public bool yaAgradecio = false;
}
