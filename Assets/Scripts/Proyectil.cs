using UnityEngine;

// La base común de los proyectiles del juego: lo que lanza el recolector es una copia de un prefab que
// lleva este script, un Rigidbody2D y un collider. Cada prefab trae su configuración (rapidez, escala de
// gravedad, rebotes, vida, giro), así que otro objeto que vuele es sólo un prefab nuevo con otros números:
//   Bolsa:   rapidez 7, gravedad 1, hasta 2 rebotes, giro 200. Sale en la dirección de la mira y dibuja un arco.
//   Botella: rapidez 14, gravedad 0, giro 540. Sale recta y horizontal, a rapidez constante, y se rompe en vidrios al
//            chocar o al llegar a su alcance (7.5 unidades), con estela y destello.
// Quien lanza fija la dirección con Lanzar; la física 2D hace el resto. El proyectil desaparece al salir
// del encuadre de la cámara, al cumplir su vida, al pasarse de rebotes (un prefab con cero rebotes desaparece
// al tocar algo) o si, ya sin rebotes por dar, se queda quieto en el suelo.
// Desde la 3.6 el lanzamiento trae un multiplicador de rapidez (la fuerza de la carga) y puede ser perfecto, con una
// estela dorada. Si se resbala, el objeto cae a los pies y al primer contacto se rompe en pedazos.
// Desde la 3.7 la botella sale en ráfaga (Disparar) con la hora exacta de su cita en la cadencia, como la bala del
// tutorial (25:22 a 34:07): sin gravedad (27:06), con su velocidad puesta otra vez en cada paso de la física (27:22) y
// con un final propio, que en el tutorial es el evento al terminar la animación de la bala (32:17) y aquí es romperse al
// llegar a su alcance. En el camino deja una estela de copias que se desvanecen.
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

    [Header("Efectos de acción")]
    [Tooltip("Distancia desde la mano a la que se rompe solo, en unidades; con 0 no tiene alcance (sólo la vida y la vista).")]
    [SerializeField] float alcance = 0f;
    [Tooltip("Si se rompe en pedazos al primer contacto, en lugar de rebotar o desaparecer.")]
    [SerializeField] bool rompeAlChocar = false;
    [Tooltip("Si vuelve a poner su velocidad de salida en cada paso de la física, como la bala del tutorial (27:22).")]
    [SerializeField] bool velocidadConstante = false;
    [Tooltip("Cada cuántas unidades de recorrido deja una copia en la estela; con 0 no deja estela.")]
    [SerializeField] float pasoEstela = 0f;
    [Tooltip("Segundos que tarda en desvanecerse cada copia de la estela.")]
    [SerializeField] float vidaEstela = 0.1f;
    [Tooltip("Color y opacidad de la copia recién puesta; las demás se desvanecen desde ahí.")]
    [SerializeField] Color colorEstela = new Color(1f, 1f, 1f, 0.5f);
    [Tooltip("Pedazos que suelta al romperse, a qué rapidez salen y de cuántos píxeles de lado.")]
    [SerializeField] int cantidadPedazos = 7;
    [SerializeField] float rapidezPedazos = 3.2f;
    [SerializeField] float tamanoPedazos = 2f;
    [Tooltip("Fracción de su velocidad que conservan los pedazos cuando se rompe solo, al llegar a su alcance.")]
    [SerializeField] float arrastrePedazos = 0f;
    [Tooltip("Pedazos de brillo, con su color, que se suman a los otros (los reflejos del vidrio).")]
    [SerializeField] int brillos = 0;
    [SerializeField] Color colorBrillo = Color.white;
    [Tooltip("Destello donde se rompe, su escala y cuánto dura; sin sprite no hay destello.")]
    [SerializeField] Sprite destello;
    [SerializeField] float escalaDestello = 1.5f;
    [SerializeField] float vidaDestello = 0.08f;

    Rigidbody2D cuerpo;
    SpriteRenderer dibujo;
    float ultimoContacto = -1f, quietoDesde = -1f;
    // Las copias de la estela ya puestas: la siguiente va a (puestas + 1) pasos de la mano.
    int copiasPuestas;
    bool roto;

    // Cuántos proyectiles se han roto desde que empezó el juego y dónde fue la última rotura.
    public static int Rotos { get; private set; }
    public static Vector3 UltimaRotura { get; private set; }

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
    public float Alcance => alcance;
    public float PasoEstela => pasoEstela;
    public float VidaEstela => vidaEstela;
    public bool RompeAlChocar => rompeAlChocar;
    public bool VelocidadConstante => velocidadConstante;
    public int PedazosAlRomperse => cantidadPedazos + brillos;
    public float EscalaDestello => escalaDestello;
    // La fuerza con que salió (fracción de su rapidez), si fue un tiro perfecto y si se le resbaló al recolector.
    public float Multiplicador { get; private set; } = 1f;
    public bool Perfecto { get; private set; }
    public bool Resbalo { get; private set; }
    public Sprite PedazoSprite => pedazo;
    // En la ráfaga: la hora de su cita (segundos del reloj del juego) y la dirección; fuera de la ráfaga la cita no existe.
    public double Cita { get; private set; } = double.NaN;
    public Vector2 Direccion { get; private set; }
    public bool Roto => roto;

    // Cuánto lleva volando, en el estado de la física de este momento, un proyectil que debió salir en cita. La física va
    // en Time.fixedTime y, con interpolación, el cuerpo se dibuja un paso de la física atrás de ella: para que se vea en la
    // mano a la hora de la cita y a rapidez·t de ella después, el estado lleva un paso más.
    public static double TiempoDeVuelo(double cita) => Time.fixedTimeAsDouble + Time.fixedDeltaTime - cita;

    void Awake()
    {
        cuerpo = GetComponent<Rigidbody2D>();
        dibujo = GetComponent<SpriteRenderer>();
        cuerpo.gravityScale = escalaGravedad;
        Origen = transform.position;
    }

    void Start()
    {
        Destroy(gameObject, vida);
    }

    // Dirección de salida (se normaliza) y sentido del giro (1 derecha, -1 izquierda).
    public void Lanzar(Vector2 direccion, int sentido) => Lanzar(direccion, sentido, 1f, false);

    // Con la fuerza de la carga: la rapidez se multiplica por multiplicador; un tiro perfecto deja estela.
    public void Lanzar(Vector2 direccion, int sentido, float multiplicador, bool perfecto)
    {
        Sentido = sentido;
        Multiplicador = multiplicador;
        Perfecto = perfecto;
        Direccion = direccion.normalized;
        VelocidadInicial = Direccion * rapidez * multiplicador;
        cuerpo.linearVelocity = VelocidadInicial;
        cuerpo.angularVelocity = -sentido * giro * Mathf.Max(0.5f, multiplicador);
        if (perfecto)
        {
            Estela();
        }
    }

    // Una botella de la ráfaga: sale a su rapidez, con la hora exacta que le tocaba en la cadencia. Quien la lanza ya la
    // puso donde iría si hubiera salido en esa cita (TiempoDeVuelo), así que todas van a la misma distancia una de otra
    // aunque el cuadro llegue un poco tarde. Origen es la mano: de ahí se cuentan el alcance y la estela.
    public void Disparar(Vector2 direccion, int sentido, Vector3 mano, double cita)
    {
        Lanzar(direccion, sentido, 1f, false);
        Origen = mano;
        Cita = cita;
        copiasPuestas = 0;
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

    // Se rompe donde está: pedazos en abanico, los brillos y el destello, y desaparece.
    public void Romper() => Romper(transform.position, Vector2.zero);

    // Al romperse en el aire (por su alcance) los vidrios siguen un poco hacia donde iba; contra algo, caen ahí.
    void Romper(Vector3 punto, Vector2 empuje)
    {
        if (roto)
        {
            return;
        }
        roto = true;
        Rotos++;
        UltimaRotura = punto;
        int orden = dibujo != null ? dibujo.sortingOrder + 1 : 10;
        Pedazo.Soltar(pedazo, punto, colorPedazos, cantidadPedazos, rapidezPedazos, Sentido, orden, tamanoPedazos, empuje);
        if (brillos > 0)
        {
            Pedazo.Soltar(pedazo, punto, colorBrillo, brillos, rapidezPedazos * 1.25f, Sentido, orden + 1, tamanoPedazos - 1f, empuje);
        }
        if (destello != null && dibujo != null)
        {
            Desvanecer.Crear(destello, punto, Quaternion.identity, escalaDestello, Color.white, vidaDestello, 0f,
                dibujo.sortingLayerID, orden + 2, null, dibujo.sharedMaterial);
        }
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

    // Lo que lleva recorrido desde la mano: en la ráfaga, rapidez por el tiempo desde su cita (la trayectoria es una
    // recta a rapidez constante); fuera de ella, la distancia a la mano.
    public float Recorrido()
    {
        if (!double.IsNaN(Cita))
        {
            return VelocidadInicial.magnitude * (float)(Time.timeAsDouble - Cita);
        }
        return Vector2.Distance(transform.position, Origen);
    }

    // Como en el tutorial (27:22), la velocidad se pone en cada paso de la física: nada la frena ni la desvía.
    void FixedUpdate()
    {
        if (velocidadConstante && !Resbalo && !roto)
        {
            cuerpo.linearVelocity = VelocidadInicial;
        }
    }

    void Update()
    {
        // Al llegar a su alcance se rompe, justo a esa distancia de la mano.
        if (alcance > 0f && !Resbalo && Recorrido() >= alcance)
        {
            Romper(double.IsNaN(Cita) ? transform.position : Origen + (Vector3)(Direccion * alcance), VelocidadInicial * arrastrePedazos);
            return;
        }

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

    // La estela de la ráfaga: una copia del sprite cada pasoEstela unidades de la recta, quieta donde la botella pasó, con el
    // giro que llevaba ahí y la edad que le toca (lo que tardó la botella en llegar de ese punto a donde va), así que las
    // copias quedan a la misma distancia una de otra a cualquier velocidad del juego. Cada una se desvanece en vidaEstela.
    void LateUpdate()
    {
        if (pasoEstela <= 0f || double.IsNaN(Cita) || dibujo == null || roto)
        {
            return;
        }

        float recorrido = Recorrido();
        float rapidezSalida = VelocidadInicial.magnitude;
        while ((copiasPuestas + 1) * pasoEstela <= recorrido)
        {
            copiasPuestas++;
            float distancia = copiasPuestas * pasoEstela;
            float edad = (recorrido - distancia) / rapidezSalida;
            if (edad >= vidaEstela)
            {
                continue;
            }
            Vector3 punto = Origen + (Vector3)(Direccion * distancia);
            var giroAtras = Quaternion.Euler(0f, 0f, -cuerpo.angularVelocity * edad);
            Desvanecer.Crear(dibujo.sprite, punto, giroAtras * transform.rotation, 1f, colorEstela, vidaEstela, edad,
                dibujo.sortingLayerID, dibujo.sortingOrder - 1, null, dibujo.sharedMaterial, dibujo.flipX);
        }
    }

    // Cada contacto nuevo (con el suelo, una pared o lo que sea) cuenta como rebote; el material físico
    // del collider decide cuánto rebota. Los contactos seguidos en menos de 0.1 s son el mismo rebote.
    // Si se rompe al chocar, el primer contacto lo hace vidrios.
    void OnCollisionEnter2D(Collision2D colision)
    {
        if (Time.time - ultimoContacto < 0.1f)
        {
            return;
        }
        ultimoContacto = Time.time;
        Rebotes++;
        if (Resbalo || rompeAlChocar)
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
