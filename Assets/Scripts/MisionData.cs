using UnityEngine;

/// <summary>
/// Una misión de cofre que puede dar un habitante con "!": te pide ir a buscar algo. Al aceptarla
/// aparece el cofre en un pasillo cerca de la vivienda, con el OBJETO PEDIDO garantizado (siempre
/// sale, no depende de probabilidad) más los extras que traiga el prefab del cofre en sus
/// PuntoSpawnItem.
///
/// Los assets van en Resources/Misiones: los habitantes los cargan de ahí y eligen uno por semilla.
/// </summary>
[CreateAssetMenu(fileName = "Mision", menuName = "La Oquedad/Misión de cofre")]
public class MisionData : ScriptableObject
{
    [Header("Identificación")]
    public string titulo = "Misión";
    [Tooltip("Qué tan seguido sale esta misión respecto a las demás (peso relativo).")]
    [Min(0f)]
    public float pesoAparicion = 1f;

    [Header("Diálogo")]
    [Tooltip("Lo que dice el NPC al darte la misión. Se elige una por semilla.")]
    [TextArea] public string[] frasesPedido = { "Necesito que me traigas algo del pasillo de afuera." };
    [Tooltip("Lo que dice si le hablás mientras la misión sigue en curso.")]
    [TextArea] public string[] frasesEnCurso = { "¿Todavía nada? Está cerca, en el pasillo de afuera." };
    [Tooltip("Lo que dice cuando le entregás el objeto.")]
    [TextArea] public string[] frasesAgradecimiento = { "¡Gracias! No sabés lo que nos sirve esto." };

    [Header("Cofre")]
    [Tooltip("Prefab del cofre. Si trae hijos llamados \"PuntoMision\", el objeto pedido aparece ahí; si no, encima del cofre. Sus PuntoSpawnItem se usan para extras opcionales.")]
    public GameObject prefabCofre;
    [Tooltip("Lo que pide la misión. Siempre aparece con el cofre.")]
    public GameObject objetoPedido;
    [Min(1)]
    public int cantidadPedida = 1;

    [Header("Karma")]
    [Tooltip("Karma que sumás si se lo entregás al NPC.")]
    public int karmaEntregar = 1;
    [Tooltip("Karma que sumás (normalmente negativo) si te lo quedás.")]
    public int karmaQuedarse = -1;
}
