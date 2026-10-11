using UnityEngine;

/// <summary>
/// Bus de "agachá el ambiente": cualquier sistema que esté haciendo un sonido IMPORTANTE (el loop
/// de patrulla de un centinela, el susto de vapor, un dron cerca) pide bajar el ambiente de fondo
/// mientras dure, para no competir con él.
///
/// Uso: el emisor llama a <see cref="Pedir"/> TODOS LOS FRAMES mientras quiera el ducking. No hay
/// que avisar cuándo termina — si deja de pedir, el factor vuelve solo a 1. Eso evita tener que
/// acordarse de "soltar" el ducking al morir, al despawnear o al cambiar de estado, que es
/// exactamente donde se cuelgan estos sistemas.
///
/// Gana el pedido MÁS agresivo del frame: con dos centinelas cerca manda el que quiere más silencio.
///
/// ⚠️ AmbientSounds lo consume en LateUpdate, no en Update. El orden de Update entre scripts es
/// arbitrario, así que leerlo ahí se llevaría los pedidos de la mitad de los emisores según cuál
/// corrió antes. LateUpdate corre después de TODOS los Update, así que siempre ve el frame completo.
/// </summary>
public static class DuckingAmbiente
{
    private static float factorPedido = 1f;

    /// <summary>
    /// Pide que el ambiente baje a este factor (0 = silencio total, 1 = sin cambios).
    /// Llamar cada frame mientras dure el sonido importante.
    /// </summary>
    public static void Pedir(float factor)
    {
        factorPedido = Mathf.Min(factorPedido, Mathf.Clamp01(factor));
    }

    /// <summary>
    /// Devuelve el factor pedido este frame y lo resetea a 1. Lo llama SOLO AmbientSounds.
    /// </summary>
    public static float ConsumirFactor()
    {
        float f = factorPedido;
        factorPedido = 1f;
        return f;
    }
}
