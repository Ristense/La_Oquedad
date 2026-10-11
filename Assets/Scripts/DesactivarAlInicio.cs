using UnityEngine;

/// <summary>
/// Desactiva automáticamente los objetos configurados al iniciar la simulación.
/// </summary>
public class DesactivarAlInicio : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("¿Desactivar también este mismo GameObject al iniciar?")]
    public bool desactivarPropioObjeto = false;

    [Tooltip("Objetos adicionales que se desactivarán al iniciar la simulación.")]
    public GameObject[] objetosADesactivar;

    void Awake()
    {
        // Desactivar los objetos de la lista
        if (objetosADesactivar != null)
        {
            foreach (GameObject obj in objetosADesactivar)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }
        }

        // Desactivar el objeto contenedor del script (evitando auto-desactivación de controladores críticos)
        if (desactivarPropioObjeto)
        {
            bool esControladorCritico = gameObject.name.Contains("Controlador") 
                                       || GetComponent<ControladorClima>() != null 
                                       || GetComponent<AmbientSounds>() != null;

            if (esControladorCritico)
            {
                Debug.LogWarning($"[DesactivarAlInicio] Se canceló la auto-desactivación de '{gameObject.name}' para evitar apagar controladores críticos (Clima, Sonido, etc.).");
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
