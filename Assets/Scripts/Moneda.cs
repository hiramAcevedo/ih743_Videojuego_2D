using System;
using UnityEngine;

// Una propina: la moneda de peso que el recolector junta en su ruta (la del arte de la 3.1). Gira con cuatro cuadros y
// flota arriba y abajo. Cuando la toca el recolector (su cápsula; las botellas y la bolsa la atraviesan) se la
// lleva: avisa a GestorNiveles con su número dentro de la ruta, deja un destello y desaparece. Ese número es el que se
// guarda en la base para que, al volver a abrir el juego, no aparezca otra vez.
[RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D))]
public class Moneda : MonoBehaviour
{
    [Tooltip("Los cuadros del giro, en orden.")]
    [SerializeField] Sprite[] cuadros = new Sprite[0];
    [SerializeField] float cuadrosPorSegundo = 10f;
    [Tooltip("Cuánto sube y baja al flotar, en unidades.")]
    [SerializeField] float flotacion = 0.08f;
    [Tooltip("Segundos de una subida y bajada completa.")]
    [SerializeField] float periodo = 1.4f;
    [Tooltip("El destello que queda donde estaba al tomarla.")]
    [SerializeField] Sprite destello;
    [SerializeField] float escalaDestello = 2f;
    [SerializeField] float vidaDestello = 0.15f;

    SpriteRenderer dibujo;
    Vector3 reposo;
    float fase;
    Action<Moneda> alTomar;

    public int Indice { get; private set; }
    public bool Tomada { get; private set; }
    public Vector3 Reposo => reposo;

    // indice: su número dentro de la ruta. Las vecinas no giran ni flotan al mismo tiempo.
    public void Preparar(int indice, Action<Moneda> avisar)
    {
        Indice = indice;
        alTomar = avisar;
        reposo = transform.position;
        fase = indice * 0.17f;
    }

    void Awake()
    {
        dibujo = GetComponent<SpriteRenderer>();
        reposo = transform.position;
    }

    void Update()
    {
        float t = Time.time + fase;
        if (cuadros.Length > 0)
        {
            dibujo.sprite = cuadros[Mathf.FloorToInt(t * cuadrosPorSegundo) % cuadros.Length];
        }
        transform.position = reposo + Vector3.up * (Mathf.Sin(t * 2f * Mathf.PI / periodo) * flotacion);
    }

    void OnTriggerEnter2D(Collider2D otro)
    {
        if (Tomada)
        {
            return;
        }
        var cuerpo = otro.attachedRigidbody;
        if (cuerpo == null || cuerpo.GetComponent<RecolectorControl>() == null)
        {
            return;
        }
        Tomada = true;
        Desvanecer.Crear(destello, transform.position, Quaternion.identity, escalaDestello, Color.white, vidaDestello, 0f,
            dibujo.sortingLayerID, dibujo.sortingOrder + 1);
        alTomar?.Invoke(this);
        Destroy(gameObject);
    }
}
