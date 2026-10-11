using UnityEngine;
using UnityEngine.SceneManagement; // Necesario para cargar escenas

public class MenuPausa : MonoBehaviour
{
    [Header("UI del Menú")]
    [Tooltip("Arrastra aquí tu Canvas del Menú (o el panel principal)")]
    public GameObject canvasMenu;
    [Tooltip("GameObjects que se ocultan mientras el menú de pausa está abierto (ej. HUD del casco) y se vuelven a mostrar al reanudar.")]
    public GameObject[] ocultarAlPausar;

    [Header("Configuración")]
    public string nombreEscenaMenuPrincipal = "MenuPrincipal"; // Cambia esto por el nombre de tu escena inicial

    private bool juegoPausado = false;

    void Start()
    {
        // Asegurarnos de que el menú empiece oculto y el tiempo normal
        if (canvasMenu != null)
        {
            canvasMenu.SetActive(false);
        }
        Time.timeScale = 1f;
    }

    void Update()
    {
        // Tecla F5 para Guardado Rápido en cualquier momento
        if (Input.GetKeyDown(KeyCode.F5))
        {
            GuardarPartida();
        }

        // Tecla F6 para borrar la partida guardada (debug)
        if (Input.GetKeyDown(KeyCode.F6))
        {
            BorrarGuardado();
        }

        // Tecla F7 para volcar el estado de la red de viviendas a la consola (debug)
        if (Input.GetKeyDown(KeyCode.F7))
        {
            if (WorldGenerator.Instance != null)
            {
                WorldGenerator.Instance.DebugImprimirRedViviendas();
            }
        }

        // Detectar si presionamos la tecla Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Debug.Log($"[MenuPausa Debug] ESC presionado. juegoPausado actual = {juegoPausado} | canvasMenu is null? {canvasMenu == null}");
            if (juegoPausado)
            {
                ReanudarJuego();
            }
            else
            {
                PausarJuego();
            }
        }
    }

    public void BotonGuardarPartida()
    {
        GuardarPartida();
    }

    public void GuardarPartida()
    {
        if (WorldGenerator.Instance != null)
        {
            WorldGenerator.Instance.GuardarPartidaActual();
        }
    }

    public void BotonBorrarGuardado()
    {
        BorrarGuardado();
    }

    public void BorrarGuardado()
    {
        SistemaGuardado.BorrarGuardado();
        Debug.Log("[MenuPausa] Partida guardada borrada.");
    }

    public void PausarJuego()
    {
        canvasMenu.SetActive(true); // Mostrar menú
        OcultarAlPausar();          // Ocultar HUD/otros mientras el menú esté abierto
        Time.timeScale = 0f;        // Congelar el tiempo
        juegoPausado = true;

        // Liberar el ratón para poder hacer clic en los botones
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ReanudarJuego()
    {
        canvasMenu.SetActive(false); // Ocultar menú
        RestaurarAlReanudar();       // Restaurar HUD/otros tal como estaban
        Time.timeScale = 1f;         // Descongelar el tiempo
        juegoPausado = false;

        // Bloquear el ratón de nuevo (útil si tu juego es en primera persona)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Cómo estaba cada objeto de ocultarAlPausar justo antes de pausar.
    private bool[] estadoPrevioOcultos;

    /// <summary>
    /// Apaga los objetos de `ocultarAlPausar` al pausar, guardando cómo estaba cada uno.
    ///
    /// ⚠️ Antes hacía `SetActive(visibles)` a todos por igual, y eso los ENCENDÍA al reanudar
    /// aunque estuvieran apagados de antes. Se notaba con el cuadro de diálogo del NPC: normalmente
    /// está apagado, reanudar lo prendía, y como DialogoUI ya había consumido su temporizador no
    /// tenía nada que vencer — el cuadro quedaba en pantalla para siempre.
    /// </summary>
    private void OcultarAlPausar()
    {
        if (ocultarAlPausar == null) return;
        estadoPrevioOcultos = new bool[ocultarAlPausar.Length];
        for (int i = 0; i < ocultarAlPausar.Length; i++)
        {
            GameObject go = ocultarAlPausar[i];
            if (go == null) continue;
            estadoPrevioOcultos[i] = go.activeSelf;
            go.SetActive(false);
        }
    }

    /// <summary>Repone lo que cada objeto TENÍA, no los prende a todos.</summary>
    private void RestaurarAlReanudar()
    {
        if (ocultarAlPausar == null || estadoPrevioOcultos == null) return;
        for (int i = 0; i < ocultarAlPausar.Length && i < estadoPrevioOcultos.Length; i++)
        {
            GameObject go = ocultarAlPausar[i];
            if (go != null) go.SetActive(estadoPrevioOcultos[i]);
        }
        estadoPrevioOcultos = null;
    }

    // --- ESTAS FUNCIONES SON PARA TUS BOTONES ---

    // Asigna esta función al OnClick de tu botón "Continuar" o "Reanudar"
    public void BotonContinuar()
    {
        ReanudarJuego();
    }

    public void BotonReanudar()
    {
        ReanudarJuego();
    }

    // Asigna esto al botón "Opciones" (de momento no hace nada)
    public void BotonOpciones()
    {
        // Debug.Log("[Menú Pausa] Botón Opciones presionado (sin acción de momento).");
    }

    // Asigna esta función al OnClick de tu botón "Volver al Menú"
    public void IrAlMenuPrincipal()
    {
        // SIEMPRE devuelve el tiempo a 1 antes de cambiar de escena, si no, el menú principal quedará congelado
        Time.timeScale = 1f; 
        SceneManager.LoadScene(nombreEscenaMenuPrincipal);
    }

    public void BotonVolverAlMenu()
    {
        IrAlMenuPrincipal();
    }

    // Asigna esta función al OnClick de tu botón "Salir" (de momento no hace nada)
    public void SalirDelJuego()
    {
        // Debug.Log("[Menú Pausa] Botón Salir presionado (sin acción de momento).");
    }

    public void BotonSalir()
    {
        SalirDelJuego();
    }
}
