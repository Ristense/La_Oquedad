using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mientras estás manejando la tablet con el mouse (ver <see cref="ModoTablet"/>), el punto de mira
/// se despega del centro de la pantalla y sigue al cursor — es el cursor de la tablet. Al soltar,
/// vuelve exactamente a donde estaba. Es SOLO visual: no clickea ni raycastea nada, de eso se sigue
/// encargando el EventSystem con la posición real del mouse (que es la misma, así que lo que ves
/// bajo la mira es lo que vas a clickear).
///
/// Va en cualquier objeto siempre activo (el Canvas del HUD sirve). El cursor del sistema se apaga
/// desde TabletaMouse (`ocultarCursorDelSistema`), si no verías los dos a la vez.
/// </summary>
public class MiraSigueMouse : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("El RectTransform del punto de mira (Punto_de_mira). Tiene que ser el objeto que se mueve, no su padre.")]
    public RectTransform mira;
    [Tooltip("Canvas donde vive la mira. Si lo dejás vacío se busca hacia arriba desde la mira. Hace falta para saber si el canvas es Overlay (sin cámara) o Screen Space - Camera / World Space.")]
    public Canvas canvas;

    private bool siguiendo;
    private Vector3 posOriginal;

    // Graphics de la mira a los que hay que apagarles el Raycast Target mientras hace de cursor.
    private Graphic[] graficos;
    private bool[] raycastOriginal;

    void Awake()
    {
        if (mira != null && canvas == null) canvas = mira.GetComponentInParent<Canvas>();
    }

    void Update()
    {
        if (mira == null) return;

        bool debeSeguir = ModoTablet.UsandoMouse;

        if (debeSeguir && !siguiendo) Enganchar();
        else if (!debeSeguir && siguiendo) Soltar();

        if (siguiendo) Seguir();
    }

    void OnDisable()
    {
        if (siguiendo) Soltar();
    }

    private void Enganchar()
    {
        siguiendo = true;
        posOriginal = mira.localPosition;

        // ⚠️ Sin esto la tablet deja de ser clickeable. La mira queda justo DEBAJO del cursor, y su
        // canvas (Overlay) está por encima del canvas World Space de la tablet, así que se come
        // todos los clicks antes de que lleguen a los botones. Clavada en el centro no se notaba
        // porque solo tapaba ese punto.
        graficos = mira.GetComponentsInChildren<Graphic>(true);
        raycastOriginal = new bool[graficos.Length];
        for (int i = 0; i < graficos.Length; i++)
        {
            raycastOriginal[i] = graficos[i].raycastTarget;
            graficos[i].raycastTarget = false;
        }
    }

    private void Soltar()
    {
        siguiendo = false;
        mira.localPosition = posOriginal;

        if (graficos != null)
        {
            for (int i = 0; i < graficos.Length; i++)
                if (graficos[i] != null) graficos[i].raycastTarget = raycastOriginal[i];
            graficos = null;
        }
    }

    private void Seguir()
    {
        RectTransform padre = mira.parent as RectTransform;
        if (padre == null) return;

        // En Screen Space - Overlay la cámara del canvas es null Y HAY que pasarla como null:
        // pasarle una cámara real ahí devuelve coordenadas equivocadas.
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvas.worldCamera
            : null;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(padre, Input.mousePosition, cam, out Vector2 local))
        {
            // localPosition y no anchoredPosition: el resultado ya viene en el espacio local del
            // padre, así que funciona igual con la mira anclada al centro, a una esquina o estirada.
            // Con anchoredPosition habría que restar el offset del anchor a mano.
            mira.localPosition = new Vector3(local.x, local.y, mira.localPosition.z);
        }
    }
}
