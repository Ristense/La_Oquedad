using UnityEngine;

public class TooltipBillboard : MonoBehaviour
{
    private Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;
    }

    void LateUpdate()
    {
        if (mainCam == null) return;
        
        // Asignar directamente la rotación en lugar del forward evita errores matemáticos (NaN)
        // si el Canvas tiene una escala de 0 en el eje Z.
        transform.rotation = mainCam.transform.rotation;
    }
}
