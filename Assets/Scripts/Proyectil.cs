using UnityEngine;

// La base común de los proyectiles del juego: lo que lanza el recolector es una copia de un prefab que
// lleva este script, un Rigidbody2D y un collider. Cada prefab trae su configuración (rapidez, escala de
// gravedad, rebotes, vida, giro), así que otro objeto que vuele es sólo un prefab nuevo con otros números:
//   Bolsa:   rapidez 7, gravedad 1, hasta 2 rebotes, giro 200. Sale en la dirección de la mira y dibuja un arco.
//   Botella: rapidez 14, gravedad 0, sin rebotes, giro 540. Sale recta y horizontal.
// Quien lanza fija la dirección con Lanzar; la física 2D hace el resto. El proyectil desaparece al salir
// del encuadre de la cámara, al cumplir su vida, al pasarse de rebotes (un prefab con cero rebotes desaparece
// al tocar algo) o si, ya sin rebotes por dar, se queda quieto en el suelo.
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class Proyectil : MonoBehaviour
{
    [Tooltip("Rapidez inicial, en unidades por segundo.")]
    [SerializeField] float rapidez = 7f;
    [Tooltip("Escala de gravedad del cuerpo: 1 cae como una bolsa, 0 vuela recto.")]
    [SerializeField] float escalaGravedad = 1f;
    [Tooltip("Rebotes permitidos; al siguiente contacto desaparece.")]
    [SerializeField] int rebotesMaximos = 2;
    [Tooltip("Segundos de vida como máximo.")]
    [SerializeField] float vida = 6f;
    [Tooltip("Giro en el aire, en grados por segundo, en el sentido del lanzamiento.")]
    [SerializeField] float giro = 200f;
    [Tooltip("Cuánto puede pasarse del borde de la vista (en anchos de pantalla) antes de desaparecer.")]
    [SerializeField] float margenVista = 0.05f;
    [Tooltip("Segundos quieto en el suelo, ya sin rebotes por dar, antes de desaparecer.")]
    [SerializeField] float vidaEnReposo = 0.5f;

    Rigidbody2D cuerpo;
    float ultimoContacto = -1f, quietoDesde = -1f;

    // Dónde nació: el punto de lanzamiento del recolector, no su centro.
    public Vector3 Origen { get; private set; }
    // Con qué velocidad salió (unidades por segundo) y hacia qué lado (1 derecha, -1 izquierda).
    public Vector2 VelocidadInicial { get; private set; }
    public int Sentido { get; private set; }
    public int Rebotes { get; private set; }
    public Vector2 Velocidad => cuerpo.linearVelocity;
    public float Rapidez => rapidez;
    public float EscalaGravedad => escalaGravedad;
    public int RebotesMaximos => rebotesMaximos;
    public float Vida => vida;

    void Awake()
    {
        cuerpo = GetComponent<Rigidbody2D>();
        cuerpo.gravityScale = escalaGravedad;
        Origen = transform.position;
    }

    void Start()
    {
        Destroy(gameObject, vida);
    }

    // Dirección de salida (se normaliza) y sentido del giro (1 derecha, -1 izquierda).
    public void Lanzar(Vector2 direccion, int sentido)
    {
        Sentido = sentido;
        VelocidadInicial = direccion.normalized * rapidez;
        cuerpo.linearVelocity = VelocidadInicial;
        cuerpo.angularVelocity = -sentido * giro;
    }

    void Update()
    {
        if (FueraDeLaVista())
        {
            Destroy(gameObject);
            return;
        }

        // Si ya dio sus rebotes y se queda quieto en el suelo sin volver a despegar, también desaparece.
        bool quieto = Rebotes >= rebotesMaximos && Rebotes > 0 && (cuerpo.IsSleeping() || cuerpo.linearVelocity.magnitude < 0.05f);
        if (!quieto)
        {
            quietoDesde = -1f;
        }
        else if (quietoDesde < 0f)
        {
            quietoDesde = Time.time;
        }
        else if (Time.time - quietoDesde >= vidaEnReposo)
        {
            Destroy(gameObject);
        }
    }

    // Cada contacto nuevo (con el suelo, una pared o lo que sea) cuenta como rebote; el material físico
    // del collider decide cuánto rebota. Los contactos seguidos en menos de 0.1 s son el mismo rebote.
    void OnCollisionEnter2D(Collision2D colision)
    {
        if (Time.time - ultimoContacto < 0.1f)
        {
            return;
        }
        ultimoContacto = Time.time;
        Rebotes++;
        if (Rebotes > rebotesMaximos)
        {
            Destroy(gameObject);
        }
    }

    // Más allá de los bordes de lo que ve la cámara principal, más el margen.
    bool FueraDeLaVista()
    {
        var camara = Camera.main;
        if (camara == null)
        {
            return false;
        }

        Vector3 v = camara.WorldToViewportPoint(transform.position);
        return v.x < -margenVista || v.x > 1f + margenVista || v.y < -margenVista;
    }
}
