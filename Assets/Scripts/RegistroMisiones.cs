using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Misiones de cofre aceptadas, completadas y el karma del jugador. Se pueden tener varias a la vez;
/// `Seleccionada` es la que se muestra como actual (la va a elegir la tablet).
///
/// Cada misión se identifica por el habitante que la dio: su vivienda (macro chunk) + su índice en
/// la casa. Así sobrevive a que el NPC se descargue y se vuelva a generar al volver.
/// </summary>
public static class RegistroMisiones
{
    public enum Estado
    {
        Aceptada,
        Completada
    }

    public class Mision
    {
        public MisionData data;
        public Vector3Int macroVivienda;
        public int indiceHabitante;
        public Vector3Int celdaCofre;
        public GameObject cofre;
        public Estado estado;
        /// <summary>Cuántos se entregaron de cada objeto pedido (por su ItemData).</summary>
        public readonly Dictionary<ItemData, int> entregados = new Dictionary<ItemData, int>();

        /// <summary>Cuántos faltan de este objeto (0 si no lo pide o ya está completo).</summary>
        public int Faltan(ItemData item)
        {
            int pedidos = 0;
            foreach (MisionData.ObjetoPedido p in data.Pedidos())
                if (MisionData.DataDe(p.prefab) == item) pedidos += p.cantidad;
            entregados.TryGetValue(item, out int dados);
            return Mathf.Max(0, pedidos - dados);
        }

        /// <summary>¿Ya se entregó todo lo pedido?</summary>
        public bool TodoEntregado()
        {
            foreach (MisionData.ObjetoPedido p in data.Pedidos())
                if (Faltan(MisionData.DataDe(p.prefab)) > 0) return false;
            return true;
        }

        /// <summary>"1 Agua, 2 Comida": lo que todavía falta, para el diálogo.</summary>
        public string DescribirFaltantes()
        {
            var partes = new List<string>();
            var vistos = new HashSet<ItemData>();
            foreach (MisionData.ObjetoPedido p in data.Pedidos())
            {
                ItemData d = MisionData.DataDe(p.prefab);
                if (d == null || !vistos.Add(d)) continue;
                int f = Faltan(d);
                if (f > 0) partes.Add($"{f} {d.nombreArticulo}");
            }
            return string.Join(", ", partes);
        }
    }

    /// <summary>Suma una entrega de este objeto. Devuelve false si la misión no lo pide o ya está completo.</summary>
    public static bool RegistrarEntrega(Mision m, ItemData item)
    {
        if (m == null || item == null || m.estado != Estado.Aceptada || m.Faltan(item) <= 0) return false;
        m.entregados.TryGetValue(item, out int dados);
        m.entregados[item] = dados + 1;
        OnCambio?.Invoke();
        return true;
    }

    private static readonly List<Mision> misiones = new List<Mision>();

    public static IReadOnlyList<Mision> Todas => misiones;
    public static Mision Seleccionada { get; private set; }
    public static int Karma { get; private set; }

    /// <summary>Se dispara al aceptar, completar o cambiar la seleccionada (para la UI).</summary>
    public static event System.Action OnCambio;

    // Con Enter Play Mode sin recarga de dominio lo estático sobrevive entre Plays: se vacía a mano.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar()
    {
        misiones.Clear();
        Seleccionada = null;
        Karma = 0;
        OnCambio = null;
    }

    public static Mision Buscar(Vector3Int macroVivienda, int indiceHabitante)
    {
        foreach (Mision m in misiones)
            if (m.macroVivienda == macroVivienda && m.indiceHabitante == indiceHabitante) return m;
        return null;
    }

    public static Mision Aceptar(MisionData data, Vector3Int macroVivienda, int indiceHabitante, Vector3Int celdaCofre, GameObject cofre)
    {
        var m = new Mision
        {
            data = data,
            macroVivienda = macroVivienda,
            indiceHabitante = indiceHabitante,
            celdaCofre = celdaCofre,
            cofre = cofre,
            estado = Estado.Aceptada,
        };
        misiones.Add(m);
        Seleccionada = m;
        OnCambio?.Invoke();
        return m;
    }

    public static void Completar(Mision m, bool entregada)
    {
        if (m == null || m.estado == Estado.Completada) return;
        m.estado = Estado.Completada;
        Karma += entregada ? m.data.karmaEntregar : m.data.karmaQuedarse;
        if (Seleccionada == m) Seleccionada = PrimeraEnCurso();
        OnCambio?.Invoke();
    }

    public static void Seleccionar(Mision m)
    {
        if (m != null && m.estado != Estado.Aceptada) return;
        Seleccionada = m;
        OnCambio?.Invoke();
    }

    private static Mision PrimeraEnCurso()
    {
        foreach (Mision m in misiones)
            if (m.estado == Estado.Aceptada) return m;
        return null;
    }

    /// <summary>Mundo nuevo: las misiones del mundo anterior ya no existen. El karma se queda.</summary>
    public static void LimpiarMisiones()
    {
        foreach (Mision m in misiones)
            if (m.cofre != null) Object.Destroy(m.cofre);
        misiones.Clear();
        Seleccionada = null;
        OnCambio?.Invoke();
    }
}
