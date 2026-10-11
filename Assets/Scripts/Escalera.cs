using UnityEngine;

using UnityEngine;

// Componente para colocar en el prefab de la escalera.
// NOTA: Este script NO crea colliders automáticamente para dejar el espacio
// intacto en la jerarquía del prefab. Añade manualmente un Collider (isTrigger = true)
// para usar la detección por trigger.
public class Escalera : MonoBehaviour
{
    void Awake()
    {
        // No se crean colliders automáticamente para preservar la jerarquía del prefab.
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogWarning("[Escalera] No hay Collider en la escalera. Añade un Collider (isTrigger=true) al prefab.");
        }
        else if (!col.isTrigger)
        {
            Debug.LogWarning("[Escalera] El Collider existe pero no está como trigger. Ajusta isTrigger=true para detección por trigger.");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Debug.Log($"[Escalera] OnTriggerEnter con '{other.name}' (Tag del objeto: '{other.tag}')");

        SistemaEscalera se = other.GetComponentInParent<SistemaEscalera>();
        if (se != null)
        {
            // Verificamos el tag en el objeto colisionado, en el script o en su GameObject
            if (other.CompareTag("Player") || se.CompareTag("Player") || se.gameObject.CompareTag("Player"))
            {
                Collider playerCollider = se.GetComponent<Collider>();
                Vector3 puntoContacto = playerCollider != null
                    ? GetComponent<Collider>().ClosestPoint(playerCollider.bounds.center)
                    : other.transform.position;

                // Debug.Log($"[Escalera] Enganchando al jugador '{se.name}' en Y={puntoContacto.y}");
                se.EntrarEnEscalera(this, puntoContacto);
            }
            else
            {
                Debug.LogWarning($"[Escalera] Se encontró SistemaEscalera en '{se.name}', pero ni el objeto ni su padre tienen el tag 'Player'.");
            }
        }
        else
        {
            // Debug.Log($"[Escalera] El objeto '{other.name}' no tiene SistemaEscalera ni en él ni en sus padres.");
        }
    }

    void OnTriggerExit(Collider other)
    {
        // Ya no soltamos por salir del trigger: la escalera se abandona solo con Space
        // o con la salida superior. Esto evita que se desancle apenas el jugador llega al base.
    }
}
