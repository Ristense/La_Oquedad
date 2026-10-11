using UnityEngine;
using UnityEditor;

public class PasarelaEditorHelper : MonoBehaviour
{
    [MenuItem("Herramientas/Pasarela/Generar Puntos Escalada")]
    public static void GenerarPuntosEscalada()
    {
        // Obtener el GameObject seleccionado (prefab de pasarela)
        GameObject selectedObject = Selection.activeGameObject;
        
        if (selectedObject == null)
        {
            EditorUtility.DisplayDialog("Error", "Selecciona un prefab de pasarela primero.", "OK");
            return;
        }

        // Verificar si tiene el componente Escalera (opcional, pero recomendado)
        Escalera escalera = selectedObject.GetComponent<Escalera>();

        // Crear dos puntos en los bordes del prefab (asumir que es un objeto de ~20x20 cm)
        // Punto 1: Borde izquierdo (-X)
        GameObject puntoIzq = new GameObject("Punto_Escalada_Izquierda");
        puntoIzq.transform.SetParent(selectedObject.transform);
        puntoIzq.transform.localPosition = new Vector3(-0.5f, 0, 0);
        puntoIzq.tag = "Escalada";
        
        // Punto 2: Borde derecho (+X)
        GameObject puntoDer = new GameObject("Punto_Escalada_Derecha");
        puntoDer.transform.SetParent(selectedObject.transform);
        puntoDer.transform.localPosition = new Vector3(0.5f, 0, 0);
        puntoDer.tag = "Escalada";

        // Si tienes el componente Escalera, asignarlos automáticamente
        if (escalera != null)
        {
            // No necesitamos hacer nada más, el script buscará los hijos con Tag "Escalada"
            EditorUtility.DisplayDialog("Éxito", "Puntos de escalada creados. Asegúrate de que 'Base' y 'Top' estén configurados en el componente Escalera.", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Éxito", "Puntos de escalada creados en:\n- Punto_Escalada_Izquierda\n- Punto_Escalada_Derecha\n\nAsegúrate de añadir el componente Escalera al prefab.", "OK");
        }

        EditorUtility.SetDirty(selectedObject);
    }

    [MenuItem("Herramientas/Pasarela/Limpiar Puntos Escalada")]
    public static void LimpiarPuntosEscalada()
    {
        GameObject selectedObject = Selection.activeGameObject;
        
        if (selectedObject == null)
        {
            EditorUtility.DisplayDialog("Error", "Selecciona un prefab de pasarela primero.", "OK");
            return;
        }

        // Buscar y eliminar todos los hijos con Tag "Escalada"
        Transform[] allChildren = selectedObject.GetComponentsInChildren<Transform>();
        int count = 0;

        foreach (Transform child in allChildren)
        {
            if (child != selectedObject.transform && child.CompareTag("Escalada"))
            {
                DestroyImmediate(child.gameObject);
                count++;
            }
        }

        EditorUtility.DisplayDialog("Éxito", $"Se eliminaron {count} puntos de escalada.", "OK");
        EditorUtility.SetDirty(selectedObject);
    }
}
