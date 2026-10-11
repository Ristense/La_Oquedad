using UnityEngine;

public class SistemaRapel : MonoBehaviour
{
    [Header("Configuración de Cuerda")]
    public float velocidadBajada = 4f;
    public float longitudMaximaCuerda = 100f; // Cuántos metros puedes bajar antes de quedarte corto
    
    [Header("Estado (No tocar)")]
    public bool estaEnRapel = false;
    public PilarRapel pilarActual;

    private CharacterController cc; 
    private Rigidbody rb;
    private PlayerMovement playerMovement;

    void Start()
    {
        // Guardamos referencias a las físicas del jugador si es que existen
        cc = GetComponent<CharacterController>();
        rb = GetComponent<Rigidbody>();
        playerMovement = GetComponent<PlayerMovement>();
    }

    void Update()
    {
        // 1. Detección de Enganche (Si NO estamos en rapel)
        if (!estaEnRapel || pilarActual == null)
        {
            Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, 5f))
            {
                PilarRapel pilar = hit.collider.GetComponentInParent<PilarRapel>();
                // Solo interactuar si el anclaje tiene la cuerda desplegada (LineRenderer activo)
                if (pilar != null && pilar.GetComponent<LineRenderer>() != null && pilar.GetComponent<LineRenderer>().enabled)
                {
                    // Mostrar UI Flotante
                    if (TooltipManager.Instance != null)
                    {
                        TooltipManager.Instance.MostrarTooltip(pilar.gameObject, "[E] Cuerda");
                    }

                    if (Input.GetKeyDown(KeyCode.E))
                    {
                        if (TooltipManager.Instance != null) TooltipManager.Instance.OcultarTooltip();
                        // Debug.Log($"[Rapel] Miraste la cuerda/anclaje. Enganchando a altura: {hit.point.y}");
                        Engancharse(pilar, hit.point.y);
                    }
                }
            }
            return; // No ejecutar lógica de colgado
        }

        // --- SISTEMA DE SOLTARSE O SUBIR A LA CORNISA ---
        // Chequeamos si el jugador está en el tope de la cuerda (a 1.5 metros del anclaje)
        bool enLaCima = (pilarActual.puntoEnganche.position.y - transform.position.y) <= 1.5f;

        // Clic Derecho para subir a la cornisa
        if (Input.GetMouseButtonDown(1) && enLaCima && pilarActual.puntoLlegada != null)
        {
            Vector3 destinoSeguro = pilarActual.puntoLlegada.position; // Guardar antes de soltar
            Desengancharse();
            
            if (cc != null) cc.enabled = false;
            transform.position = destinoSeguro;
            if (cc != null) cc.enabled = true;
            
            return;
        }

        // Si presionas ESPACIO, te sueltas inmediatamente dejándote caer
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Desengancharse();
            return;
        }
        // ------------------------------------

        // W para subir (positivo), S para bajar (negativo)
        float inputY = Input.GetAxis("Vertical"); 
        
        // Calculamos cuánto te mueves este frame
        float movimientoY = inputY * velocidadBajada * Time.deltaTime;

        // Nueva posición inicial calculada
        Vector3 nuevaPosicion = transform.position;
        nuevaPosicion.y += movimientoY;

        // LÍMITE SUPERIOR: No puedes subir más arriba de donde está clavado el pilar
        if (nuevaPosicion.y > pilarActual.puntoEnganche.position.y)
        {
            nuevaPosicion.y = pilarActual.puntoEnganche.position.y;
        }

        // LÍMITE INFERIOR: Se acaba la cuerda
        float distanciaCuerda = pilarActual.puntoEnganche.position.y - nuevaPosicion.y;
        if (distanciaCuerda > longitudMaximaCuerda)
        {
            nuevaPosicion.y = pilarActual.puntoEnganche.position.y - longitudMaximaCuerda;
        }

        // MODO TIESO: Te alineamos perfectamente a la misma coordenada X y Z del pilar
        // (Esto evita que atravieses paredes. Más adelante aquí meteremos la fórmula de péndulo/balanceo)
        nuevaPosicion.x = pilarActual.puntoEnganche.position.x;
        nuevaPosicion.z = pilarActual.puntoEnganche.position.z;

        // Aplicamos el movimiento apagando las físicas normales para que no interfieran
        if (cc != null)
        {
            cc.enabled = false;
            transform.position = nuevaPosicion;
            cc.enabled = true;
        }
        else if (rb != null)
        {
            rb.MovePosition(nuevaPosicion);
            if (!rb.isKinematic) rb.linearVelocity = Vector3.zero; 
        }
        else
        {
            transform.position = nuevaPosicion;
        }

        // Estirar la cuerda visual (LineRenderer) hasta la cintura del jugador
        pilarActual.ActualizarCuerdaVisual(transform.position);
    }

    public void Engancharse(PilarRapel pilar, float alturaDeEnganche)
    {
        estaEnRapel = true;
        pilarActual = pilar;
        
        if (playerMovement != null) playerMovement.movementLocked = true;
        
        // Nos colocamos a la altura donde el jugador miró la cuerda (con un ligero ajuste visual)
        Vector3 posInicio = pilar.puntoEnganche.position;
        posInicio.y = alturaDeEnganche - 1f; 
        
        if (cc != null) cc.enabled = false;
        transform.position = posInicio;
        if (cc != null) cc.enabled = true;

        // Si usas Rigidbody, apagamos la gravedad mientras cuelgas
        if (rb != null) rb.isKinematic = true; 
        
        // Debug.Log($"[Rapel] ¡Enganchado exitosamente! Altura de tu personaje ahora es: {posInicio.y}");
    }

    public void Desengancharse()
    {
        estaEnRapel = false;
        // Reactivar la gravedad si tienes Rigidbody
        if (rb != null) rb.isKinematic = false; 

        if (playerMovement != null)
        {
            playerMovement.movementLocked = false;
            playerMovement.BloquearSpaceHastaSoltar();
        }
        
        // Ya no ocultamos la cuerda aquí, se queda colgando para siempre
        pilarActual = null;
        // Debug.Log("[Rapel] Te soltaste de la cuerda (Gravedad reactivada).");
    }
}
