using UnityEngine;

// La mira para lanzar la bolsa, como en Yoshi's Island: mientras se sostiene la tecla de apuntar aparece
// delante del recolector y oscila sola en un arco alrededor del punto de lanzamiento, subiendo y bajando
// sin parar; al soltar, la bolsa sale hacia donde esté la mira en ese momento. Es hija del recolector, así
// que cuando él se voltea con la escala en x la mira pasa al otro lado sola.
[RequireComponent(typeof(SpriteRenderer))]
public class Mira : MonoBehaviour
{
    [Tooltip("El punto de lanzamiento (PuntoDisparo): el centro del arco.")]
    [SerializeField] Transform pivote;
    [Tooltip("Ángulo más bajo del arco, en grados sobre la horizontal.")]
    [SerializeField] float anguloMinimo = -10f;
    [Tooltip("Ángulo más alto del arco, en grados sobre la horizontal.")]
    [SerializeField] float anguloMaximo = 70f;
    [Tooltip("Segundos que tarda un ciclo completo: subir y volver a bajar.")]
    [SerializeField] float periodo = 1.2f;
    [Tooltip("Distancia de la mira al punto de lanzamiento, en unidades.")]
    [SerializeField] float radio = 2.5f;

    SpriteRenderer dibujo;
    float tiempo;

    public bool Visible => dibujo != null && dibujo.enabled;
    // Ángulo actual, en grados, en el espacio del recolector (0 es hacia donde mira).
    public float Angulo { get; private set; }
    public float AnguloMinimo => anguloMinimo;
    public float AnguloMaximo => anguloMaximo;
    public float Periodo => periodo;
    // Dirección de lanzamiento en el mundo: del punto de lanzamiento a la mira.
    public Vector2 Direccion => ((Vector2)(transform.position - pivote.position)).normalized;

    void Awake()
    {
        dibujo = GetComponent<SpriteRenderer>();
        dibujo.enabled = false;
        Colocar(anguloMinimo);
    }

    public void Mostrar()
    {
        tiempo = 0f;
        Colocar(anguloMinimo);
        dibujo.enabled = true;
    }

    public void Ocultar()
    {
        dibujo.enabled = false;
    }

    void Update()
    {
        if (!Visible)
        {
            return;
        }

        tiempo += Time.deltaTime;
        // De 0 a 1 y de vuelta a 0 en cada periodo, sin saltos en los extremos.
        float fase = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * tiempo / periodo);
        Colocar(Mathf.Lerp(anguloMinimo, anguloMaximo, fase));
    }

    void Colocar(float angulo)
    {
        Angulo = angulo;
        float radianes = angulo * Mathf.Deg2Rad;
        transform.localPosition = pivote.localPosition + new Vector3(Mathf.Cos(radianes), Mathf.Sin(radianes), 0f) * radio;
    }
}
