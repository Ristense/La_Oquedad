using System;
using UnityEngine;

/// <summary>
/// Hook estático para que cualquier sistema (ruido, luz, romper algo, objetivo
/// cumplido) pueda forzar una ronda de centinelas sin acoplarse a RondaCentinelas.
/// </summary>
public static class AlertaCentinelas
{
    public static event Action<Vector3> OnAlerta;

    public static void Disparar(Vector3 posicion)
    {
        OnAlerta?.Invoke(posicion);
    }
}
