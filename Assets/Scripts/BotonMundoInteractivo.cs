using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Permite interactuar con un objeto en el mundo (ej. botones 3D o botones con BoxCollider en un Canvas World Space)
/// al apuntar directamente con la mira y presionar la tecla [E].
/// </summary>
[RequireComponent(typeof(Collider))]
public class BotonMundoInteractivo : MonoBehaviour
{
    [Header("Ajustes de Interacción")]
    [Tooltip("Mensaje que se mostrará en pantalla (ej: '[Click] Activar Sistema').")]
    public string mensajeTooltip = "[Click] Pulsar Botón";
    [Tooltip("Distancia máxima desde la cámara para poder interactuar.")]
    public float alcanceInteraccion = 3.5f;

    [Header("Eventos")]
    [Tooltip("Evento que se ejecutará al hacer Click Izquierdo mientras se mira el botón.")]
    public UnityEvent alPresionar;

    private Collider miCollider;
    private BoxCollider miBoxCollider;
    private RectTransform miRectTransform;

    private void Awake()
    {
        miCollider = GetComponent<Collider>();
        miBoxCollider = GetComponent<BoxCollider>();
        miRectTransform = GetComponent<RectTransform>();
    }

    private void Start()
    {
        AutoAjustarCollider();
    }

    private void Reset()
    {
        AutoAjustarCollider();
    }

    /// <summary>
    /// Ajusta automáticamente el BoxCollider para que coincida con las dimensiones del RectTransform.
    /// </summary>
    [ContextMenu("Ajustar Collider a RectTransform")]
    public void AutoAjustarCollider()
    {
        if (miBoxCollider == null) miBoxCollider = GetComponent<BoxCollider>();
        if (miRectTransform == null) miRectTransform = GetComponent<RectTransform>();

        if (miRectTransform != null && miBoxCollider != null)
        {
            // Ajustar el tamaño del collider al tamaño del botón en el Canvas
            miBoxCollider.size = new Vector3(miRectTransform.rect.width, miRectTransform.rect.height, 0.2f);
            
            // Corregir el centro en base al pivote del RectTransform
            miBoxCollider.center = new Vector3(
                (0.5f - miRectTransform.pivot.x) * miRectTransform.rect.width,
                (0.5f - miRectTransform.pivot.y) * miRectTransform.rect.height,
                0f
            );
        }
    }

    private void Update()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        // Comprobación rápida de distancia antes de hacer el Raycast
        float distancia = Vector3.Distance(transform.position, cam.transform.position);
        if (distancia > alcanceInteraccion) return;

        // Lanzar un rayo desde el centro de la cámara hacia adelante
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        
        // Usamos QueryTriggerInteraction.Collide para obligar a que choque con colliders Trigger 
        // sin importar la configuración global del proyecto en Physics Settings.
        if (Physics.Raycast(ray, out RaycastHit hit, alcanceInteraccion, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            // Verificar si el rayo impactó este collider específico o a uno de sus hijos
            if (hit.collider == miCollider || hit.transform.IsChildOf(transform))
            {
                // Mostrar tooltip de interacción diegética
                if (TooltipManager.Instance != null)
                {
                    TooltipManager.Instance.MostrarTooltip(gameObject, mensajeTooltip);
                }

                // Detectar pulsación de la tecla de interacción (Click Izquierdo)
                if (Input.GetMouseButtonDown(0))
                {
                    Presionar();
                }
            }
        }
    }

    /// <summary>
    /// Invoca el evento asociado al botón. Puede llamarse también por código.
    /// </summary>
    public void Presionar()
    {
        if (alPresionar != null)
        {
            alPresionar.Invoke();
        }
    }
}
