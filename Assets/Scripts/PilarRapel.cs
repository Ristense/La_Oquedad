using UnityEngine;

// Esto obligará a Unity a añadir un LineRenderer automáticamente al pilar
[RequireComponent(typeof(LineRenderer))]
public class PilarRapel : MonoBehaviour
{
    [Header("Configuración del Pilar")]
    // Objeto vacío de donde saldrá físicamente la cuerda. Lo ideal es que esté un poco asomado al vacío.
    public Transform puntoEnganche; 
    // Objeto vacío donde aparecerá el jugador al llegar a la cima
    public Transform puntoLlegada;
    public float longitudCuerda = 100f; // Longitud máxima que colgará la cuerda

    [Header("Efectos Visuales")]
    [Tooltip("Sistema de partículas para la previsualización (asignar en el inspector).")]
    public ParticleSystem particulasPreview;

    [Header("Cuerda (LineRenderer)")]
    [Tooltip("Material de la cuerda. En HDRP DEBE ser HDRP/Unlit (o similar), si no la cuerda sale invisible/magenta. Si se deja vacío, se crea uno HDRP/Unlit automáticamente.")]
    public Material materialCuerda;
    [Tooltip("Ancho de la cuerda en metros.")]
    public float anchoCuerda = 0.05f;
    [Tooltip("Color/emisión de la cuerda (HDR para que brille con el Bloom).")]
    [ColorUsage(true, true)] public Color colorCuerda = Color.white;

    private LineRenderer lineRenderer;
    [HideInInspector] public Vector3 puntoFinalCuerda;
    private SistemaRapel jugadorRapel;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 2;

        // Material: sin uno HDRP-compatible, el LineRenderer no se ve. Se asigna el del inspector
        // o, como fallback, se crea uno HDRP/Unlit en runtime.
        if (materialCuerda != null)
        {
            lineRenderer.material = materialCuerda;
        }
        else if (lineRenderer.sharedMaterial == null)
        {
            Shader s = Shader.Find("HDRP/Unlit");
            if (s != null) lineRenderer.material = new Material(s);
        }

        lineRenderer.startWidth = anchoCuerda;
        lineRenderer.endWidth = anchoCuerda;
        lineRenderer.startColor = colorCuerda;
        lineRenderer.endColor = colorCuerda;
        lineRenderer.numCapVertices = 2; // puntas redondeadas, se ve más como cuerda

        // Si el pilar NO es un objeto recogible, desplegamos la cuerda de inicio
        if (GetComponent<InventoryItem>() == null)
        {
            DesplegarCuerda();
            SetParticulasActivas(false);
        }
        else
        {
            MostrarCuerda(false);
            SetParticulasActivas(false);
        }

        // Busca automáticamente al jugador por su Tag "Player"
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            jugadorRapel = playerObj.GetComponent<SistemaRapel>();
        }
    }

    public void DesplegarCuerda()
    {
        if (lineRenderer == null || puntoEnganche == null) return;

        lineRenderer.enabled = true;
        
        // Raycast hacia abajo para ver si toca suelo
        puntoFinalCuerda = puntoEnganche.position - new Vector3(0, longitudCuerda, 0);
        if (Physics.Raycast(puntoEnganche.position, Vector3.down, out RaycastHit hit, longitudCuerda))
        {
            puntoFinalCuerda = hit.point;
        }

        // Asignar posiciones al LineRenderer
        lineRenderer.useWorldSpace = true;
        lineRenderer.SetPosition(0, puntoEnganche.position);
        lineRenderer.SetPosition(1, puntoFinalCuerda);


        // Crear un collider para que puedas hacerle clic a cualquier parte de la cuerda
        Transform objCollider = transform.Find("ColliderCuerdaFisica");
        if (objCollider == null)
        {
            GameObject go = new GameObject("ColliderCuerdaFisica");
            go.transform.SetParent(transform);
            objCollider = go.transform;
            objCollider.gameObject.AddComponent<BoxCollider>();
        }
        
        // Forzar posición y rotación limpia (global)
        objCollider.position = puntoEnganche.position;
        objCollider.rotation = Quaternion.identity; 
        objCollider.gameObject.SetActive(true);
        
        BoxCollider colCuerda = objCollider.GetComponent<BoxCollider>();
        float distancia = puntoEnganche.position.y - puntoFinalCuerda.y;
        float extraArriba = 0.5f; // Medio metro extra hacia arriba para que no quede corto
        float distanciaTotal = distancia + extraArriba;
        
        colCuerda.center = new Vector3(0, (extraArriba - distancia) / 2f, 0); 
        colCuerda.size = new Vector3(1f, distanciaTotal, 1f); // Grosor de 1 metro
        
        // Debug.Log($"[Rapel] Cuerda desplegada físicamente. Altura inicial: {puntoEnganche.position.y} | Fondo: {puntoFinalCuerda.y} | Largo Total: {distancia}m");
    }

    // Dibujar el collider en la vista de Escena (Scene View) para debugeo
    void OnDrawGizmos()
    {
        Transform objCollider = transform.Find("ColliderCuerdaFisica");
        if (objCollider != null && objCollider.gameObject.activeInHierarchy)
        {
            BoxCollider col = objCollider.GetComponent<BoxCollider>();
            if (col != null)
            {
                Gizmos.color = new Color(1f, 0f, 1f, 0.5f); // Color Magenta semitransparente
                Gizmos.matrix = objCollider.localToWorldMatrix;
                Gizmos.DrawWireCube(col.center, col.size);
            }
        }
    }

    public void MostrarCuerda(bool mostrar)
    {
        if (lineRenderer != null) lineRenderer.enabled = mostrar;
        Transform objCollider = transform.Find("ColliderCuerdaFisica");
        if (objCollider != null)
        {
            objCollider.gameObject.SetActive(mostrar);
        }
    }

    public void ActualizarCuerdaVisual(Vector3 posicionJugador)
    {
        if (!lineRenderer.enabled) return;

        // Visualmente, la cuerda siempre va del anclaje al fondo.
        // Pero si quieres que "termine" en el jugador, descomenta esto:
        // lineRenderer.SetPosition(1, posicionJugador);
    }

    public void SetParticulasActivas(bool activas)
    {
        if (particulasPreview != null)
        {
            if (activas)
            {
                particulasPreview.gameObject.SetActive(true);
                particulasPreview.Play();
            }
            else
            {
                particulasPreview.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particulasPreview.gameObject.SetActive(false);
            }
        }
        else
        {
            // Fallback: si no se asignó nada, busca en los hijos automáticamente
            ParticleSystem[] particulas = GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in particulas)
            {
                if (activas)
                {
                    ps.gameObject.SetActive(true);
                    ps.Play();
                }
                else
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.gameObject.SetActive(false);
                }
            }
        }
    }
}
