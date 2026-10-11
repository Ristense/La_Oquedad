using UnityEngine;

// Esto agrega un nuevo botón en el menú de Create de Unity:
[CreateAssetMenu(menuName = "Inventario/Consumible")]
public class ConsumibleData : ItemData // ¡Aquí está la magia! Heredamos de ItemData
{
    [Header("Fraccionamiento (Porciones)")]
    [Tooltip("¿Se consume en porciones (varios clics) o todo de una vez?")]
    public bool esFraccionable = false;
    [Tooltip("Cantidad de porciones/clics en los que se divide el consumible.")]
    [Min(1)]
    public int porcionesMaximas = 1;

    [Header("Propiedades Nutritivas")]
    [Tooltip("Tiempo en SEGUNDOS de comida a restaurar (ej. 300 = 5 minutos)")]
    public float restaurarComida = 20f;
    
    [Tooltip("Tiempo en SEGUNDOS de agua a restaurar (ej. 300 = 5 minutos)")]
    public float restaurarAgua = 10f;
    
    [Tooltip("Tiempo en SEGUNDOS de vigilia a restar. Se convierte a horas al aplicarse a PlayerData.horasDespierto (ej. 120 = 2 minutos = 0.033h).")]

    public float restaurarSueno = 0f;
    
    [Tooltip("Tiempo en SEGUNDOS de batería a restaurar (ej. 120 = 2 minutos)")]
    public float restaurarBateria = 0f;

    // "override" significa que estamos reemplazando (o sobreescribiendo) el método vacío de la base
    public override void Usar(PlayerData player)
    {
        // Aplicamos los efectos al jugador (en segundos)
        player.foodSeconds += restaurarComida;
        player.waterSeconds += restaurarAgua;

        // `horasDespierto` está en horas, así que convertimos los segundos del consumible a horas.
        player.horasDespierto -= restaurarSueno / 3600f;
        if (player.horasDespierto < 0) player.horasDespierto = 0f;

        player.energySeconds += restaurarBateria;

        // Popups flotantes (+Xs) junto a cada stat. Todo lo que restaura un consumible se muestra
        // en positivo, aunque internamente 'restaurarSueno' reste horas de vigilia.
        if (restaurarComida > 0f) player.MostrarPopupComida(restaurarComida);
        if (restaurarAgua > 0f) player.MostrarPopupAgua(restaurarAgua);
        if (restaurarSueno > 0f) player.MostrarPopupSueno(restaurarSueno);
        if (restaurarBateria > 0f) player.MostrarPopupEnergia(restaurarBateria);

        // Debug.Log($"Has consumido: {nombreArticulo}. (+{restaurarComida}s Comida, +{restaurarAgua}s Agua, -{restaurarSueno}s Fatiga, +{restaurarBateria}s Batería)");
    }

    // Agrega las estadísticas que aporta el consumible a la descripción base (nombre + peso).
    public override string DescripcionTooltip()
    {
        string s = base.DescripcionTooltip();
        if (restaurarComida > 0f)  s += $"\nComida  +{FormatoSegundos(restaurarComida)}";
        if (restaurarAgua > 0f)    s += $"\nAgua  +{FormatoSegundos(restaurarAgua)}";
        if (restaurarBateria > 0f) s += $"\nBatería  +{FormatoSegundos(restaurarBateria)}";
        if (restaurarSueno > 0f)   s += $"\nSueño  +{FormatoSegundos(restaurarSueno)}";
        if (esFraccionable && porcionesMaximas > 1) s += $"\nPorciones: {porcionesMaximas}";
        return s;
    }
}
