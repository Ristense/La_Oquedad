using System;
using UnityEngine;

/// <summary>
/// El "pincho" físico que dispara la tirolina. Vuela con Rigidbody (le afecta la gravedad de verdad,
/// cae en arco) y se clava en lo primero sólido que choque — no hay raycast mágico, si apuntas mal
/// se clava donde caiga o se destruye solo si no pega nada (tiempoVidaMax).
///
/// Lo crea e instancia <see cref="SistemaTirolina"/> vía <see cref="Lanzar"/>.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ProyectilTirolina : MonoBehaviour
{
    [Tooltip("Tiempo de vida máximo (segundos) antes de autodestruirse si no choca contra nada.")]
    public float tiempoVidaMax = 6f;

    private Rigidbody rb;
    private bool clavado = false;
    private Action<Vector3> alImpactar;
    private Action alFallar;

    /// <param name="velocidadInicial">Velocidad de salida (dirección × velocidad), la gravedad hace el resto.</param>
    /// <param name="ignorar">Collider del jugador que dispara, para no chocar contra sí mismo.</param>
    /// <param name="callbackImpacto">Se llama con el punto exacto donde se clavó.</param>
    /// <param name="callbackFallo">Se llama si se autodestruye sin haber chocado nada (para no dejar el arma trabada).</param>
    public void Lanzar(Vector3 velocidadInicial, Collider ignorar, Action<Vector3> callbackImpacto, Action callbackFallo)
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        // Evita que atraviese paredes delgadas al ir rápido, y que se vea entrecortado.
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.linearVelocity = velocidadInicial;

        alImpactar = callbackImpacto;
        alFallar = callbackFallo;

        Collider miCollider = GetComponent<Collider>();
        if (ignorar != null && miCollider != null)
            Physics.IgnoreCollision(miCollider, ignorar, true);

        Destroy(gameObject, tiempoVidaMax);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (clavado) return;
        clavado = true;

        // Si este proyectil nunca pasó por Lanzar() (ej. quedó una copia suelta en la escena para
        // pruebas, en vez de haber sido instanciado por SistemaTirolina), rb nunca se asignó.
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb == null) return;

        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Vector3 punto = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;

        // Se queda pegado a la superficie (así sigue clavado si esa geometría se mueve).
        transform.SetParent(collision.transform, worldPositionStays: true);

        alImpactar?.Invoke(punto);
    }

    void OnDestroy()
    {
        // Si se destruyó por tiempoVidaMax sin haberse clavado en nada, avisamos que "falló"
        // para que SistemaTirolina no se quede pensando que hay un disparo en vuelo para siempre.
        if (!clavado) alFallar?.Invoke();
    }
}
