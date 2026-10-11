using UnityEngine;

/// <summary>
/// Activa/desactiva un GameObject (típicamente un Volume con Film Grain) según si
/// ALGUNA de las UIs en `uisAVigilar` está activa. Si hay UI abierta → Grain ON.
/// Si no hay nada → Grain OFF.
/// </summary>
public class GrainUI : MonoBehaviour
{
    [Tooltip("GameObject que se activa cuando hay alguna UI abierta (asignale el Grain Volume).")]
    public GameObject grainVolume;

    [Tooltip("Lista de GameObjects de UI que vigilamos. Si CUALQUIERA está activo, encendemos el grain.")]
    public GameObject[] uisAVigilar;

    void Update()
    {
        if (grainVolume == null || uisAVigilar == null) return;

        bool algunaAbierta = false;
        for (int i = 0; i < uisAVigilar.Length; i++)
        {
            if (uisAVigilar[i] != null && uisAVigilar[i].activeInHierarchy)
            {
                algunaAbierta = true;
                break;
            }
        }

        if (grainVolume.activeSelf != algunaAbierta)
            grainVolume.SetActive(algunaAbierta);
    }
}
