using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // Obligatorio para Slider
using System.Collections;
using System.Collections.Generic;
using TMPro; // Obligatorio para los textos de TextMeshPro

public class PlayerData : MonoBehaviour
{
    [Header("Estadísticas de Supervivencia")]
    public float maxWaterSeconds = 600f; // 10 minutes
    public float waterSeconds = 600f; // starts full
    public float maxFoodSeconds = 600f; // 10 minutes
    public float foodSeconds = 600f; // starts full
    public float maxEnergySeconds = 600f; // 10 minutes
    public float energySeconds = 600f; // starts full

    [Header("Estado de Sueño")]
    public float horasDespierto = 0f; // Cuántas horas lleva sin dormir
    public float maxHorasDespierto = 40f; // Máximo de horas despierto (para el slider)

    [Header("Datos de Posición y Entorno")]
    public Vector3 coordenadaMundo;     // Posición real X,Y,Z
    public Vector3Int coordenadaGrilla; // Celda del laberinto
    public Vector3 posicionSpawn;       // Dónde nació
    [Tooltip("Clima actual: lo lee ControladorClima para decidir efectos. Ya NO se muestra en el HUD (se sacó txtClima) pero sigue siendo estado interno.")]
    public string climaActual = "Despejado";

    [Header("Referencias UI (Casco)")]
    public CanvasGroup cascoUI;
    [Tooltip("GameObject contenedor de los textos flotantes del HUD (Agua.UI, Comida.UI, etc). Se desactiva cuando la batería llega a 0.")]
    public GameObject hudContenedor;
    public TMP_Text txtAgua;
    public TMP_Text txtComida;
    public TMP_Text txtSueno;
    public TMP_Text txtEnergia;
    public TMP_Text txtCoordenadas;
    public TMP_Text txtPesoTotal;
    [Tooltip("Slider que representa el agua restante (1 = lleno, 0 = vacío).")]
    public Slider sliderAgua;
    public Slider sliderComida;
    public Slider sliderSueno;
    public Slider sliderEnergia;
    public Slider sliderPeso;

    [Header("Popups de cambio (+Xs / -Xs) — Agua, Comida, Sueño, Batería (Peso NO tiene)")]
    [Tooltip("Popup flotante junto al agua. Se dispara solo con MostrarPopupAgua(), no reacciona al drenaje pasivo.")]
    public TextoFlotanteStat popupAgua;
    public TextoFlotanteStat popupComida;
    public TextoFlotanteStat popupSueno;
    public TextoFlotanteStat popupEnergia;

    [Header("Tiempo y Calendario (estado interno — el HUD ya no lo muestra)")]
    public int anioActual = 2084;
    public int diaDelAnio = 1;
    public float horaActual = 8f; // Empieza a las 8
    [Tooltip("Cuántas horas 'in-game' corresponden a un día completo — se usa para formatear el contador de sueño (ej. '2d 05:30') y para el avance del calendario que se registra en EstadisticasRun.diasSobrevividos.")]
    public int horasPorDia = 40;
    [Tooltip("Minutos de juego que pasan por cada segundo real (afecta al calendario, NO al drenaje de agua/comida/batería que va en tiempo real).")]
    public float multiplicadorTiempo = 60f;

    [Header("Carga Física")]
    public float pesoTotal = 0f; // Recalculado cada frame en Update desde la mochila + item en mano + nota sostenida
    public float maxPesoTotal = 50f; // Peso máximo soportado (para el Slider)
    [Tooltip("Referencia a la mochila del jugador (para sumar el peso de los items guardados). Si se deja vacía, se busca sola con FindFirstObjectByType.")]
    public MochilaGrid mochila;
    [Tooltip("Referencia a la mano del jugador (para sumar el peso del item sostenido). Si se deja vacía, se busca sola.")]
    public ManoJugador mano;
    [Tooltip("Referencia al LectorNotas (para sumar el peso de la nota sostenida). Si se deja vacía, se busca sola.")]
    public LectorNotas lectorNotas;

    [Header("Notas (Lore)")]
    [Tooltip("BD de notas que el jugador leyó/guardó con E. No se persisten entre sesiones (por ahora).")]
    public List<NotaData> notasGuardadas = new List<NotaData>();

    [Header("Optimización de Rendimiento")]
    [Tooltip("Intervalo en segundos entre cada actualización de los textos del HUD.")]
    public float intervaloUpdateUI = 0.2f;
    private float temporizadorUI = 0f;

    [Header("Muerte y Fin del Ciclo Jugable")]
    [Tooltip("Escena que se carga cuando el jugador muere. Debe estar incluida en Build Settings.")]
    public string escenaMenu = "Menu";
    [Tooltip("Segundos a esperar después de morir antes de cargar la escena del menú.")]
    public float delayMuerte = 1.5f;
    [Tooltip("Canvas que muestra los stats de la run. Persiste el cambio de escena al menú. Se cierra con Escape.")]
    public MuerteUI muerteUI;
    public bool Muerto => muerto;
    private bool muerto = false;
    private int diaInicial = -1;        // Para calcular días sobrevividos
    private float pesoMaximoCargado = 0f; // Para mostrar en game over

    void Start()
    {
        diaInicial = diaDelAnio;

        // Auto-cachear refs si no fueron asignadas en el inspector
        if (mochila == null)      mochila      = FindFirstObjectByType<MochilaGrid>();
        if (mano == null)         mano         = FindFirstObjectByType<ManoJugador>();
        if (lectorNotas == null)  lectorNotas  = FindFirstObjectByType<LectorNotas>();
    }

    /// <summary>
    /// Recalcula el peso total desde 3 fuentes: items guardados en la mochila + item sostenido
    /// en la mano + nota sostenida. Fuente única de la verdad → ningún otro script debe tocar
    /// pesoTotal directamente (si lo hicieran, se pisaría con el próximo recálculo).
    /// </summary>
    private void RecalcularPesoTotal()
    {
        float total = 0f;
        if (mochila != null) total += mochila.PesoTotalItemsGuardados();
        if (mano != null && mano.ItemEnMano != null && mano.ItemEnMano.data != null)
            total += mano.ItemEnMano.data.peso;
        if (lectorNotas != null && lectorNotas.SostieneNota && lectorNotas.NotaSostenida != null)
            total += lectorNotas.NotaSostenida.peso;
        pesoTotal = total;
    }

    void Update()
    {
        // Recalcular la carga real (mochila + mano + nota) ANTES de trackear el máximo
        RecalcularPesoTotal();

        // Stats de run: trackear el peso máximo cargado
        if (pesoTotal > pesoMaximoCargado) pesoMaximoCargado = pesoTotal;

        // 1. Guardar la coordenada exacta todo el tiempo (posición mundo) y la celda derivada de ésta.
        // La celda se calcula acá (no se espera a que WorldGenerator nos avise) para que el HUD muestre
        // siempre la celda REAL del mundo, incluso en el frame del spawn donde el jugador aún no cruzó
        // ninguna celda. Se delega en WorldGenerator.MundoACelda para que la grilla sea la MISMA que
        // usa el generador (local a su transform); el fallback solo aplica si todavía no hay Instance.
        coordenadaMundo = transform.position;
        if (WorldGenerator.Instance != null)
        {
            coordenadaGrilla = WorldGenerator.Instance.MundoACelda(transform.position);
        }
        else
        {
            coordenadaGrilla = new Vector3Int(
                Mathf.FloorToInt(transform.position.x / 20f),
                Mathf.FloorToInt(transform.position.y / 20f),
                Mathf.FloorToInt(transform.position.z / 20f)
            );
        }

        // 2. Drenaje de estadísticas (todas a 1 segundo por segundo real = 10 min cada una)
        if (waterSeconds > 0) waterSeconds -= Time.deltaTime;
        if (foodSeconds > 0) foodSeconds -= Time.deltaTime;
        if (energySeconds > 0) energySeconds -= Time.deltaTime;

        // 3. Evitar que bajen de 0 o suban de max
        waterSeconds = Mathf.Clamp(waterSeconds, 0, maxWaterSeconds);
        foodSeconds = Mathf.Clamp(foodSeconds, 0, maxFoodSeconds);
        energySeconds = Mathf.Clamp(energySeconds, 0, maxEnergySeconds);

        // 5. Tiempo: calendario in-game (afecta horaActual/diaDelAnio para EstadisticasRun.diasSobrevividos
        // y para el ControladorClima) + horas despierto (única que se muestra en el HUD).
        float minutosAIncrementar = Time.deltaTime * multiplicadorTiempo;
        horaActual += minutosAIncrementar / 60f;
        horasDespierto += Time.deltaTime / 60f;

        if (horaActual >= horasPorDia)
        {
            horaActual -= horasPorDia;
            diaDelAnio++;
            if (diaDelAnio > 365) { diaDelAnio = 1; anioActual++; }
        }

        // 4. Pintar datos en el Canvas de forma optimizada
        temporizadorUI += Time.deltaTime;
        if (temporizadorUI >= intervaloUpdateUI)
        {
            ActualizarUI();
            temporizadorUI = 0f;
        }

        // El glitch corre frame a frame en estado crítico para la fluidez visual
        if (energySeconds > 0f && energySeconds <= maxEnergySeconds * 0.15f)
        {
            ActualizarGlitchBateria();
        }
    }

    // --- Popups de cambio puntual (+Xs / -Xs). Llamar solo ante eventos explícitos ---
    // (consumir un item, gastar batería en la tirolina, etc.), NUNCA desde el drenaje pasivo por frame.
    public void MostrarPopupAgua(float delta) { if (popupAgua != null) popupAgua.Mostrar(delta); }
    public void MostrarPopupComida(float delta) { if (popupComida != null) popupComida.Mostrar(delta); }
    public void MostrarPopupSueno(float delta) { if (popupSueno != null) popupSueno.Mostrar(delta); }
    public void MostrarPopupEnergia(float delta) { if (popupEnergia != null) popupEnergia.Mostrar(delta); }

    public void ActualizarCoordenadaGrilla(Vector3Int nuevaCelda)
    {
        coordenadaGrilla = nuevaCelda;
    }

    public void SetearSpawn(Vector3 spawnReal)
    {
        posicionSpawn = spawnReal;
        // Debug.Log("Punto de Spawn Guardado en PlayerData: " + posicionSpawn);
    }

    /// <summary>
    /// Guarda una nota en la BD del jugador. Evita duplicados (misma instancia de NotaData).
    /// </summary>
    public void GuardarNota(NotaData nota)
    {
        if (nota == null) return;
        if (notasGuardadas.Contains(nota))
        {
            // Debug.Log($"[PlayerData] Nota '{nota.titulo}' ya estaba guardada — ignorada.");
            return;
        }
        notasGuardadas.Add(nota);
        // Debug.Log($"[PlayerData] Nota guardada: '{nota.titulo}' por {nota.autor}. Total: {notasGuardadas.Count}");
    }

    /// <summary>
    /// Mata al jugador con una causa. Idempotente: si ya está muerto no hace nada.
    /// Desactiva PlayerMovement/MouseLook para que el cadáver no responda al input,
    /// muestra la causa en el HUD si hay UI asignada, espera `delayMuerte` segundos
    /// y carga la escena del menú.
    /// </summary>
    public void Morir(string causa)
    {
        if (muerto) return;
        muerto = true;

        // Debug.Log($"[PlayerData] Jugador muerto. Causa: {causa}");

        // Registrar stats de la run para el Game Over screen y agregar las notas leídas a la biblioteca
        int dias = (diaInicial >= 0) ? Mathf.Max(0, diaDelAnio - diaInicial) : 0;
        EstadisticasRun.RegistrarMuerte(dias, notasGuardadas.Count, pesoMaximoCargado, causa, notasGuardadas);

        // Apagar input del jugador (locomoción + cámara) para que el cuerpo quede inerte
        PlayerMovement pm = GetComponent<PlayerMovement>();
        if (pm != null) pm.enabled = false;
        MouseLook ml = GetComponentInChildren<MouseLook>();
        if (ml != null) ml.canLook = false;

        // Liberar cursor para que la transición al menú no sea molesta
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Activar el canvas de muerte: pinta los stats y se hace DontDestroyOnLoad para sobrevivir el cambio de escena
        if (muerteUI != null) muerteUI.Activar();

        StartCoroutine(RutinaMuerte());
    }

    private IEnumerator RutinaMuerte()
    {
        yield return new WaitForSecondsRealtime(delayMuerte);

        if (string.IsNullOrEmpty(escenaMenu))
        {
            Debug.LogError("[PlayerData] No se puede cargar la escena del menú: `escenaMenu` está vacío.");
            yield break;
        }

        Time.timeScale = 1f; // Por si quedó pausado
        SceneManager.LoadScene(escenaMenu);
    }

    private void ActualizarUI()
    {
        // Formatea segundos en minutos:segundos
        if (txtAgua != null) txtAgua.text = FormatTime(waterSeconds);
        if (sliderAgua != null) sliderAgua.value = maxWaterSeconds > 0f ? waterSeconds / maxWaterSeconds : 0f;
        
        if (txtComida != null) txtComida.text = FormatTime(foodSeconds);
        if (sliderComida != null) sliderComida.value = maxFoodSeconds > 0f ? foodSeconds / maxFoodSeconds : 0f;
        
        if (txtEnergia != null) txtEnergia.text = FormatTime(energySeconds);
        if (sliderEnergia != null) sliderEnergia.value = maxEnergySeconds > 0f ? energySeconds / maxEnergySeconds : 0f;

        if (txtSueno != null)
        {
            int d = Mathf.FloorToInt(horasDespierto / horasPorDia);
            int h = Mathf.FloorToInt(horasDespierto % horasPorDia);
            int m = Mathf.FloorToInt((horasDespierto - Mathf.Floor(horasDespierto)) * 60f);

            if (d > 0)
                txtSueno.text = $"{d}d {h:D2}:{m:D2}";
            else
                txtSueno.text = $"{h:D2}:{m:D2}";
        }
        if (sliderSueno != null)
        {
            sliderSueno.value = maxHorasDespierto > 0f ? Mathf.Clamp01(1f - (horasDespierto / maxHorasDespierto)) : 0f;
        }
        if (txtPesoTotal != null) txtPesoTotal.text = $"{pesoTotal:F1} kg";
        if (sliderPeso != null)
        {
            sliderPeso.value = maxPesoTotal > 0f ? Mathf.Clamp01(pesoTotal / maxPesoTotal) : 0f;
        }

        if (txtCoordenadas != null)
        {
            // Mostramos en qué celda matemática estamos Y la posición real
            txtCoordenadas.text = $"Celda: X:{coordenadaGrilla.x} Y:{coordenadaGrilla.y} Z:{coordenadaGrilla.z}\n" +
                                  $"Mundo: {coordenadaMundo.x:F1}, {coordenadaMundo.y:F1}, {coordenadaMundo.z:F1}";
        }

        // LÓGICA DE APAGADO DEL CASCO (Opacidad normal o apagado)
        if (cascoUI != null)
        {
            if (energySeconds > 15f)
            {
                // De 100 a 15 de batería, se va oscureciendo suavemente (de 1.0 a 0.2 de opacidad)
                // Convertimos a porcentaje basado en maxEnergySeconds
                float percent = (energySeconds - 15f) / (maxEnergySeconds - 15f);
                cascoUI.alpha = Mathf.Lerp(0.2f, 1f, percent);
            }
            else if (energySeconds <= 0f)
            {
                // 0 batería = Apagado Total
                cascoUI.alpha = 0f;
            }
        }

        // HUD (textos flotantes): se desactiva con batería en 0, así también el GrainUI sabe que no hay UI.
        // En rango de glitch (<15% batería), no tocamos su estado acá — lo maneja ActualizarGlitchBateria
        // que corre cada frame con un parpadeo random.
        if (hudContenedor != null)
        {
            bool enGlitch = energySeconds > 0f && energySeconds <= maxEnergySeconds * 0.15f;
            if (!enGlitch)
            {
                bool deberiaEstarActivo = energySeconds > 0f;
                if (hudContenedor.activeSelf != deberiaEstarActivo)
                    hudContenedor.SetActive(deberiaEstarActivo);
            }
        }
    }

    // Helper to convert seconds into a mm:ss display string
    private string FormatTime(float seconds)
    {
        // Clamp to avoid negative values
        seconds = Mathf.Max(0f, seconds);
        int minutes = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        return $"{minutes:D0}:{secs:D2}";
    }
    // Simula el glitch de batería cuando está críticamente baja
    private void ActualizarGlitchBateria()
    {
        // CanvasGroup (compatibilidad con HUD basado en Canvas)
        if (cascoUI != null)
        {
            cascoUI.alpha = Random.value > 0.4f ? 0.3f : 0.05f;
        }

        // Parpadeo por SetActive — funciona con TMP 3D u otros elementos que no sean UI
        if (hudContenedor != null)
        {
            bool activo = Random.value > 0.4f; // ~60% prendido, ~40% apagado por frame
            if (hudContenedor.activeSelf != activo)
                hudContenedor.SetActive(activo);
        }
    }
}

