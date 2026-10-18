using UnityEngine;

// La base común de los proyectiles del juego: lo que lanza el recolector es una copia de un prefab que
// lleva este script, un Rigidbody2D y un collider. Cada prefab trae su configuración (rapidez, escala de
// gravedad, rebotes, vida, giro), así que otro objeto que vuele es sólo un prefab nuevo con otros números:
//   Bolsa:   rapidez 7, gravedad 1, hasta 2 rebotes, giro 200. Sale en la dirección de la mira y dibuja un arco.
//   Botella: rapidez 14, gravedad 0, sin rebotes, giro 540. Sale recta y horizontal.
// Quien lanza fija la dirección con Lanzar; la física 2D hace el resto. El proyectil desaparece al salir
// del encuadre de la cámara, al cumplir su vida, al pasarse de rebotes (un prefab con cero rebotes desaparece
// al tocar algo) o si, ya sin rebotes por dar, se queda quieto en el suelo.
// Desde la 3.6 el lanzamiento trae un multiplicador de rapidez (la fuerza de la carga o del swing) y puede ser
// perfecto, con una estela dorada. Si se resbala, el objeto cae a los pies y al primer contacto se rompe en pedazos.
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
    [Tooltip("Sprite de un píxel para los pedazos cuando se rompe.")]
    [SerializeField] Sprite pedazo;
    [Tooltip("Color de los pedazos.")]
    [SerializeField] Color colorPedazos = new Color32(60, 66, 72, 255);

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
    // La fuerza con que salió (fracción de su rapidez), si fue un tiro perfecto y si se le resbaló al recolector.
    public float Multiplicador { get; private set; } = 1f;
    public bool Perfecto { get; private set; }
    public bool Resbalo { get; private set; }
    public Sprite PedazoSprite => pedazo;

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
    public void Lanzar(Vector2 direccion, int sentido) => Lanzar(direccion, sentido, 1f, false);

    // Con la fuerza de la carga o del swing: la rapidez se multiplica por multiplicador; un tiro perfecto deja estela.
    public void Lanzar(Vector2 direccion, int sentido, float multiplicador, bool perfecto)
    {
        Sentido = sentido;
        Multiplicador = multiplicador;
        Perfecto = perfecto;
        VelocidadInicial = direccion.normalized * rapidez * multiplicador;
        cuerpo.linearVelocity = VelocidadInicial;
        cuerpo.angularVelocity = -sentido * giro * Mathf.Max(0.5f, multiplicador);
        if (perfecto)
        {
            Estela();
        }
    }

    // Se le resbaló: cae casi a plomo, un poco hacia adelante, y se rompe al primer contacto.
    public void Resbalar(int sentido)
    {
        Sentido = sentido;
        Resbalo = true;
        Multiplicador = 0f;
        rebotesMaximos = 0;
        cuerpo.gravityScale = 1f;
        VelocidadInicial = new Vector2(sentido * 0.8f, 0.6f);
        cuerpo.linearVelocity = VelocidadInicial;
        cuerpo.angularVelocity = -sentido * 120f;
    }

    // Se rompe donde está: pedazos en abanico y desaparece.
    public void Romper()
    {
        var r = GetComponent<SpriteRenderer>();
        Pedazo.Soltar(pedazo, transform.position, colorPedazos, 7, 3.2f, Sentido, r != null ? r.sortingOrder + 1 : 10);
        Destroy(gameObject);
    }

    void Estela()
    {
        var estela = gameObject.AddComponent<TrailRenderer>();
        estela.time = 0.25f;
        estela.startWidth = 0.16f;
        estela.endWidth = 0f;
        estela.minVertexDistance = 0.05f;
        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
        estela.material = new Material(shader);
        var dibujo = GetComponent<SpriteRenderer>();
        if (dibujo != null)
        {
            estela.sortingOrder = dibujo.sortingOrder - 1;
        }
        var degradado = new Gradient();
        degradado.SetKeys(
            new[] { new GradientColorKey(new Color32(255, 214, 92, 255), 0f), new GradientColorKey(new Color32(255, 150, 40, 255), 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        estela.colorGradient = degradado;
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
        if (Resbalo)
        {
            Romper();
        }
        else if (Rebotes > rebotesMaximos)
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
