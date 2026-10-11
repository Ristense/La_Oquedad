using UnityEngine;

/// <summary>Los tres recursos que una vivienda puede almacenar. Todos medidos en SEGUNDOS.</summary>
public enum RecursoVivienda { Agua, Comida, Energia }

/// <summary>
/// Reservas de una vivienda, en SEGUNDOS — la misma unidad que las stats del jugador
/// (PlayerData.waterSeconds / foodSeconds / energySeconds). Se leen como "cuánto tiempo le
/// queda a esta vivienda de este recurso antes de quedarse sin nada".
/// </summary>
[System.Serializable]
public class StatsVivienda
{
    public float agua;
    public float comida;
    public float energia;

    /// <summary>
    /// Momento de la partida (en segundos, el reloj de RegistroViviendas) al que corresponden los
    /// valores de arriba. Es lo que permite drenar SIN simular: al leer, se descuenta lo que pasó
    /// desde acá hasta ahora. Una vivienda nunca tocada tiene 0 y se le descuenta la partida entera.
    /// </summary>
    public float tiempoUltimaActualizacion;

    public float Obtener(RecursoVivienda recurso)
    {
        switch (recurso)
        {
            case RecursoVivienda.Agua: return agua;
            case RecursoVivienda.Comida: return comida;
            default: return energia;
        }
    }

    public void Asignar(RecursoVivienda recurso, float segundos)
    {
        segundos = Mathf.Max(0f, segundos);
        switch (recurso)
        {
            case RecursoVivienda.Agua: agua = segundos; break;
            case RecursoVivienda.Comida: comida = segundos; break;
            default: energia = segundos; break;
        }
    }

    public StatsVivienda Clonar() => new StatsVivienda
    {
        agua = agua,
        comida = comida,
        energia = energia,
        tiempoUltimaActualizacion = tiempoUltimaActualizacion
    };
}
