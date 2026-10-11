using UnityEngine;
using System.IO;

public static class SistemaGuardado
{
    [System.Serializable]
    public class DatosPartida
    {
        public int semilla;
        public float posX;
        public float posY;
        public float posZ;
        public float rotY;
    }

    private static string RutaArchivo => Path.Combine(Application.persistentDataPath, "partida_guardada.json");

    /// <summary>
    /// Guarda la semilla y la transformación del jugador en disco.
    /// </summary>
    public static void Guardar(int semilla, Vector3 pos, float rotY)
    {
        DatosPartida datos = new DatosPartida
        {
            semilla = semilla,
            posX = pos.x,
            posY = pos.y,
            posZ = pos.z,
            rotY = rotY
        };

        string json = JsonUtility.ToJson(datos, true);
        File.WriteAllText(RutaArchivo, json);
        PlayerPrefs.SetInt("HayPartidaGuardada", 1);
        PlayerPrefs.Save();
        Debug.Log($"[SistemaGuardado] Partida guardada en: {RutaArchivo} | Semilla: {semilla} | Pos: {pos}");
    }

    /// <summary>
    /// Carga los datos de la partida guardada si existe.
    /// </summary>
    public static bool Cargar(out int semilla, out Vector3 pos, out float rotY)
    {
        semilla = 0;
        pos = Vector3.zero;
        rotY = 0f;

        if (!ExisteGuardado()) return false;

        try
        {
            string json = File.ReadAllText(RutaArchivo);
            DatosPartida datos = JsonUtility.FromJson<DatosPartida>(json);
            
            semilla = datos.semilla;
            pos = new Vector3(datos.posX, datos.posY, datos.posZ);
            rotY = datos.rotY;
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SistemaGuardado] Error al cargar partida: {e.Message}");
            return false;
        }
    }

    public static bool ExisteGuardado()
    {
        return File.Exists(RutaArchivo);
    }

    public static void BorrarGuardado()
    {
        if (ExisteGuardado())
        {
            File.Delete(RutaArchivo);
        }
        PlayerPrefs.DeleteKey("HayPartidaGuardada");
        PlayerPrefs.Save();
    }
}
