using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controlador para la escena de Introducción / Menú Principal.
/// Permite mover la cámara por waypoints al presionar Jugar,
/// cargar la escena de juego en segundo plano,
/// y realizar una transición suave mediante fundido a negro y persistencia entre escenas.
/// </summary>
public class ControladorIntro : MonoBehaviour
{
    [Header("Referencias de la Intro")]
    [Tooltip("La cámara de la escena de introducción.")]
    public Camera camaraIntro;
    
    [Tooltip("Lo que se apaga al arrancar la intro: el Canvas del menú, o el panel/Image de adentro si compartís un solo Canvas Overlay para todo. Es un GameObject a propósito — con el tipo Canvas no se podía apuntar a un hijo, y apagar el Canvas entero se llevaba puesto el resto del HUD.")]
    public GameObject canvasMenu;

    [Tooltip("Puntos por los que pasará la cámara en orden (mínimo 3).")]
    public Transform[] waypoints;

    [Header("Ajustes de Movimiento")]
    [Tooltip("Velocidad de movimiento de la cámara.")]
    public float velocidadMovimiento = 12f;

    [Tooltip("Velocidad de rotación (slerp) de la cámara.")]
    public float velocidadRotacion = 3f;

    [Header("Configuración de Carga")]
    [Tooltip("Nombre exacto de la escena de juego a cargar.")]
    public string escenaJuego = "OutdoorsScene";

    private bool introActiva = false;
    private bool haciendoFundido = false;
    private float alphaFundido = 0f;

    void Awake()
    {
        // Registrar el evento de carga de escena
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        // Limpiar el registro del evento para evitar fugas de memoria
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        // Asegurar que el cursor esté libre al inicio para poder clickear el botón
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (camaraIntro == null)
        {
            camaraIntro = Camera.main;
        }
    }

    void Update()
    {
        // Defensa: mantener el cursor visible y liberado mientras la intro/menú esté corriendo
        if (!introActiva && !haciendoFundido)
        {
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    /// <summary>
    /// Método a enlazar con el OnClick() del botón Jugar en el Canvas.
    /// </summary>
    public void BotonJugarPresionado()
    {
        if (introActiva) return;
        introActiva = true;
        
        // Hacemos que este controlador sea persistente para que el fundido a negro
        // continúe cubriendo la pantalla durante el cambio de escena.
        DontDestroyOnLoad(gameObject);
        
        StartCoroutine(IniciarIntro());
    }

    private IEnumerator IniciarIntro()
    {
        // 1. Ocultar el Canvas del Menú
        if (canvasMenu != null)
        {
            canvasMenu.SetActive(false);
        }

        // 2. Iniciar la carga de la escena de juego en segundo plano (NO aditiva para evitar solapamientos)
        AsyncOperation cargaAsync = SceneManager.LoadSceneAsync(escenaJuego);
        cargaAsync.allowSceneActivation = false;

        // 3. Mover la cámara a través de los waypoints
        int indexWaypoint = 0;

        if (waypoints != null && waypoints.Length > 0)
        {
            while (indexWaypoint < waypoints.Length)
            {
                Vector3 destino = waypoints[indexWaypoint].position;
                Quaternion rotDestino = waypoints[indexWaypoint].rotation;

                // Mover hasta estar muy cerca del waypoint actual
                while (Vector3.Distance(camaraIntro.transform.position, destino) > 0.15f)
                {
                    camaraIntro.transform.position = Vector3.MoveTowards(
                        camaraIntro.transform.position,
                        destino,
                        velocidadMovimiento * Time.deltaTime
                    );
                    camaraIntro.transform.rotation = Quaternion.Slerp(
                        camaraIntro.transform.rotation,
                        rotDestino,
                        velocidadRotacion * Time.deltaTime
                    );
                    yield return null;
                }

                indexWaypoint++;
            }
        }

        // 4. Seguir derecho en la dirección del último waypoint mientras termina de cargar la escena
        Vector3 direccionDerecho = Vector3.forward;
        if (waypoints != null && waypoints.Length > 0)
        {
            direccionDerecho = waypoints[waypoints.Length - 1].forward;
        }
        else
        {
            direccionDerecho = camaraIntro.transform.forward;
        }

        // Esperar a que la escena termine de cargar en segundo plano (llega a 0.9f cuando allowSceneActivation es false)
        while (cargaAsync.progress < 0.9f)
        {
            camaraIntro.transform.position += direccionDerecho * velocidadMovimiento * Time.deltaTime;
            yield return null;
        }

        // 5. La escena está lista para ser activada! Hacer fundido a negro
        haciendoFundido = true;
        yield return StartCoroutine(Fade(1f, 0.8f)); // Fundido de 0.8 segundos

        // 6. Activar la escena cargada (esto destruye la escena del menú y activa la de OutdoorsScene)
        cargaAsync.allowSceneActivation = true;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Si se cargó la escena de juego y este objeto es persistente, iniciamos la finalización de la transición
        if (scene.name == escenaJuego && introActiva)
        {
            StartCoroutine(FinalizarTransicion());
        }
    }

    private IEnumerator FinalizarTransicion()
    {
        // Esperar hasta que el WorldGenerator de la nueva escena esté listo y el jugador tenga suelo
        while (WorldGenerator.Instance == null || !WorldGenerator.Instance.MundoListo)
        {
            yield return null;
        }

        // Esperar un frame adicional para asegurar inicialización de cámaras de juego
        yield return null;

        // Liberar el cursor para que MouseLook del juego tome el control nativo
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Hacer el fundido de negro a transparente para revelar el gameplay
        yield return StartCoroutine(Fade(0f, 0.8f));

        // Destruir este controlador que ya completó su función
        Destroy(gameObject);
    }

    private IEnumerator Fade(float targetAlpha, float duration)
    {
        float startAlpha = alphaFundido;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            alphaFundido = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }
        alphaFundido = targetAlpha;
    }

    void OnGUI()
    {
        // Dibujar un plano negro en pantalla para realizar la transición/fundido
        if (alphaFundido > 0f)
        {
            GUI.color = new Color(0f, 0f, 0f, alphaFundido);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        }
    }
}
