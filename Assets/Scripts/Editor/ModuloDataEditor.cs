using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor visual para la FORMA de un ModuloData.
/// Dibuja una grilla de cuadritos clicables: verde = celda ocupada, gris = vacía.
/// Click para alternar. La celda (0,0) se marca como origen.
/// </summary>
[CustomEditor(typeof(ModuloData))]
public class ModuloDataEditor : Editor
{
    private int rango = 4; // dibuja una grilla de (rango x rango) alrededor del origen

    public override void OnInspectorGUI()
    {
        // Campos normales (nombre, peso, prefabVisual, y la lista forma por si se edita a mano)
        DrawDefaultInspector();

        ModuloData modulo = (ModuloData)target;
        if (modulo.forma == null) modulo.forma = new List<Vector2Int>();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Editor de Forma", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Click en los cuadros para alternar celdas. Verde = ocupada. La celda con borde es el origen (0,0).", MessageType.Info);

        rango = EditorGUILayout.IntSlider("Rango de la grilla", rango, 2, 8);

        HashSet<Vector2Int> ocupadas = new HashSet<Vector2Int>(modulo.forma);

        float tam = 26f;
        // Dibujamos de arriba (y alto) hacia abajo para que coincida visualmente con el juego
        for (int y = rango; y >= -rango; y--)
        {
            EditorGUILayout.BeginHorizontal();
            for (int x = -rango; x <= rango; x++)
            {
                Vector2Int celda = new Vector2Int(x, y);
                bool ocupada = ocupadas.Contains(celda);
                bool esOrigen = (x == 0 && y == 0);

                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = ocupada
                    ? new Color(0.4f, 0.9f, 0.45f)
                    : new Color(0.3f, 0.3f, 0.3f);

                string etiqueta = esOrigen ? "◎" : "";
                if (GUILayout.Button(etiqueta, GUILayout.Width(tam), GUILayout.Height(tam)))
                {
                    Undo.RecordObject(modulo, "Editar forma del módulo");
                    if (ocupada) modulo.forma.Remove(celda);
                    else modulo.forma.Add(celda);
                    EditorUtility.SetDirty(modulo);
                }

                GUI.backgroundColor = prev;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField($"Celdas en la forma: {modulo.forma.Count}");

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Limpiar"))
        {
            Undo.RecordObject(modulo, "Limpiar forma");
            modulo.forma.Clear();
            EditorUtility.SetDirty(modulo);
        }
        if (GUILayout.Button("Reset a 1 celda"))
        {
            Undo.RecordObject(modulo, "Reset forma");
            modulo.forma.Clear();
            modulo.forma.Add(Vector2Int.zero);
            EditorUtility.SetDirty(modulo);
        }
        EditorGUILayout.EndHorizontal();
    }
}
