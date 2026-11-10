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
    [Tooltip("Lo que dice cuando le entregás el ÚLTIMO objeto que faltaba.")]
    [TextArea] public string[] frasesAgradecimiento = { "¡Gracias! No sabés lo que nos sirve esto." };
    [Tooltip("Lo que dice al recibir uno que no es el último. Al final se le agrega lo que todavía falta.")]
    [TextArea] public string[] frasesEntregaParcial = { "¡Bien, uno menos!" };
    [Tooltip("Lo que dice cuando le mostrás lo que pidió y decidís quedártelo.")]
    [TextArea] public string[] frasesRechazo = { "¿En serio? Lo tenés en la mano... Está bien, ya veremos cómo nos arreglamos." };

    [Header("Cofre")]
    [Tooltip("Prefab del cofre. Si trae hijos llamados \"PuntoMision\", el objeto pedido aparece ahí; si no, encima del cofre. Sus PuntoSpawnItem se usan para extras opcionales.")]
    public GameObject prefabCofre;
    [System.Serializable]
    public class ObjetoPedido
    {
        [Tooltip("Prefab del objeto (con InventoryItem y su Data).")]
        public GameObject prefab;
        [Min(1)] public int cantidad = 1;
    }

    [Tooltip("Lo que pide la misión: uno o varios objetos, cada uno con su cantidad. Todos aparecen garantizados en el cofre y se entregan de a uno con [E].")]
    public ObjetoPedido[] objetosPedidos;

    [Header("Viejo (un solo objeto)")]
    [Tooltip("Solo se usa si Objetos Pedidos está vacío. Para misiones nuevas usá la lista de arriba.")]
    public GameObject objetoPedido;
    [Min(1)]
    public int cantidadPedida = 1;

    /// <summary>Lo que pide la misión: la lista si tiene algo, si no el objeto viejo. Sin nulos.</summary>
    public System.Collections.Generic.List<ObjetoPedido> Pedidos()
    {
        var lista = new System.Collections.Generic.List<ObjetoPedido>();
        if (objetosPedidos != null)
            foreach (ObjetoPedido p in objetosPedidos)
                if (p != null && p.prefab != null && p.cantidad > 0) lista.Add(p);
        if (lista.Count == 0 && objetoPedido != null)
            lista.Add(new ObjetoPedido { prefab = objetoPedido, cantidad = Mathf.Max(1, cantidadPedida) });
        return lista;
    }

    /// <summary>ItemData del prefab de un pedido (de su InventoryItem). Null si no tiene.</summary>
    public static ItemData DataDe(GameObject prefab) =>
        prefab != null && prefab.TryGetComponent(out InventoryItem inv) ? inv.data : null;

    [Header("Karma")]
    [Tooltip("Karma que sumás si se lo entregás al NPC.")]
    public int karmaEntregar = 1;
    [Tooltip("Karma que sumás (normalmente negativo) si te lo quedás.")]
    public int karmaQuedarse = -1;
}
