using UnityEngine;
using UnityEngine.InputSystem;

// Estructura del personaje: lee el teclado, elige la animación de estado, salta y lanza.
// Flecha derecha o D: Correr mirando a la derecha. Flecha izquierda o A: Correr mirando
// a la izquierda. Sin dirección (o las dos a la vez): Reposo, sin cambiar hacia dónde mira.
// W o flecha arriba: salta con los pies en el suelo y, ya en el aire, una vez más: el doble salto. Al volver a
// tocar el suelo se recarga. El segundo salto parte de velocidad vertical cero, así que sube lo mismo que el primero.
// K sostenida: aparece la mira y oscila delante del recolector; al soltar K sale la bolsa hacia la mira.
// Barra espaciadora: sale la botella, recta y horizontal, hacia donde mira.
// La física es la de Unity 2D: un Rigidbody2D dinámico con gravedad y la rotación congelada, y una
// CapsuleCollider2D del tamaño del cuerpo con la base en los pies, que se apoya en el collider del suelo.
// Todavía no se desplaza a los lados: las teclas de dirección sólo animan y voltean.
[RequireComponent(typeof(Animator), typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
public class RecolectorControl : MonoBehaviour
{
    static readonly int Corriendo = Animator.StringToHash("corriendo");
    // Más rápido que esto hacia arriba ya va despegando, aunque el rayo todavía alcance el suelo.
    const float VelocidadDespegue = 0.5f;

    [Header("Salto")]
    [Tooltip("Impulso hacia arriba al saltar. Con masa 1 es la velocidad de salida en unidades por segundo.")]
    [SerializeField] float fuerzaSalto = 5f;
    [Tooltip("Saltos que se pueden dar en el aire antes de volver a tocar el suelo. Con 1 es el doble salto; con 0, sólo el salto desde el suelo.")]
    [SerializeField] int saltosEnElAire = 1;
    [Tooltip("Capas que cuentan como suelo para el rayo: la del Tilemap Grid > Suelo.")]
    [SerializeField] LayerMask capaSuelo;
    [Tooltip("Cuánto llega el rayo por debajo de los pies, en unidades.")]
    [SerializeField] float margenSuelo = 0.1f;

    [Header("Lanzar")]
    [Tooltip("Prefab de la bolsa de basura: sale hacia la mira, con gravedad y rebotes.")]
    [SerializeField] GameObject bolsaPrefab;
    [Tooltip("Prefab de la botella: sale recta y rápida hacia donde mira el recolector.")]
    [SerializeField] GameObject botellaPrefab;
    [Tooltip("Hijo del recolector a la altura de la mano. Como cuelga de él, se voltea con la escala en x.")]
    [SerializeField] Transform puntoDisparo;
    [Tooltip("La mira oscilante de la bolsa, hija del recolector.")]
    [SerializeField] Mira mira;

    Animator animator;
    Rigidbody2D cuerpo;
    CapsuleCollider2D capsula;
    // Saltos dados en el aire desde la última vez que tocó el suelo.
    int saltosAereos;

    // -1 izquierda, 0 sin dirección, 1 derecha; como Input.GetAxisRaw("Horizontal").
    public float Horizontal { get; private set; }
    // Ángulo de la mira en el último lanzamiento de bolsa (grados sobre la horizontal, hacia donde mira).
    public float UltimoAngulo { get; private set; }
    // Si en este cuadro tiene los pies en el suelo: el rayo lo toca y no va despegando.
    public bool EnSuelo { get; private set; }
    // Saltos dados desde que empezó la escena.
    public int Saltos { get; private set; }

    void Start()
    {
        animator = GetComponent<Animator>();
        cuerpo = GetComponent<Rigidbody2D>();
        capsula = GetComponent<CapsuleCollider2D>();
    }

    void Update()
    {
        var teclado = Keyboard.current;
        Horizontal = LeerHorizontal(teclado);

        animator.SetBool(Corriendo, Horizontal != 0f);

        // La escala negativa en x voltea el sprite y, con él, al punto de lanzamiento y a la mira.
        if (Horizontal < 0f)
        {
            transform.localScale = new Vector3(-1f, 1f, 1f);
        }
        else if (Horizontal > 0f)
        {
            transform.localScale = new Vector3(1f, 1f, 1f);
        }

        EnSuelo = TocaSuelo();
        if (EnSuelo)
        {
            saltosAereos = 0;
        }

        if (teclado == null)
        {
            return;
        }

        // El salto: W o la flecha arriba, en el cuadro en que se presiona. Con los pies en el suelo, el salto normal;
        // en el aire, sólo si le queda un salto extra (el doble salto).
        if (teclado.wKey.wasPressedThisFrame || teclado.upArrowKey.wasPressedThisFrame)
        {
            if (EnSuelo)
            {
                Saltar();
            }
            else if (saltosAereos < saltosEnElAire)
            {
                saltosAereos++;
                Saltar();
            }
        }

        // Una botella por pulsación: el evento de la tecla, no mientras se mantiene apretada.
        if (teclado.spaceKey.wasPressedThisFrame)
        {
            Lanzar(botellaPrefab, new Vector2(Sentido(), 0f));
        }

        // La bolsa: apuntar mientras K está apretada y lanzar al soltarla, una por lanzamiento.
        if (mira != null)
        {
            if (teclado.kKey.wasPressedThisFrame)
            {
                mira.Mostrar();
            }
            else if (teclado.kKey.wasReleasedThisFrame && mira.Visible)
            {
                UltimoAngulo = mira.Angulo;
                Lanzar(bolsaPrefab, mira.Direccion);
                mira.Ocultar();
            }
        }
    }

    // Como en el tutorial: un rayo hacia abajo; si toca el suelo, se puede saltar. Sale del centro de la
    // cápsula y llega margenSuelo por debajo de los pies. Sólo ve la capa del suelo, así que no se detecta
    // a sí mismo ni a la bolsa o la botella (el tutorial lo resuelve apagando Queries Start In Colliders).
    // Después del impulso, mientras la física no da su siguiente paso, el rayo todavía alcanza el suelo: la
    // velocidad hacia arriba dice que ya va en el aire. Debug.DrawRay lo dibuja en la vista de escena.
    bool TocaSuelo()
    {
        Bounds caja = capsula.bounds;
        Vector2 origen = caja.center;
        float distancia = caja.extents.y + margenSuelo;
        bool toca = Physics2D.Raycast(origen, Vector2.down, distancia, capaSuelo).collider != null
            && cuerpo.linearVelocity.y <= VelocidadDespegue;
        Debug.DrawRay(origen, Vector2.down * distancia, toca ? Color.green : Color.red);
        return toca;
    }

    // AddForce hacia arriba, como en el tutorial, aplicada como impulso: la velocidad de salida es
    // fuerzaSalto entre la masa y no depende del paso de la física. Se parte de velocidad vertical cero
    // para que todos los saltos suban lo mismo, aunque la tecla llegue en el último instante de una caída.
    void Saltar()
    {
        cuerpo.linearVelocity = new Vector2(cuerpo.linearVelocity.x, 0f);
        cuerpo.AddForce(Vector2.up * fuerzaSalto, ForceMode2D.Impulse);
        Saltos++;
    }

    int Sentido() => transform.localScale.x >= 0f ? 1 : -1;

    // Como en el tutorial, la dirección depende del signo de la escala en x y el proyectil nace en el
    // punto de lanzamiento, no en el centro del personaje. Cada prefab trae su propia física (Proyectil).
    void Lanzar(GameObject prefab, Vector2 direccion)
    {
        if (prefab == null || puntoDisparo == null)
        {
            return;
        }

        GameObject copia = Instantiate(prefab, puntoDisparo.position, Quaternion.identity);
        var colliderCopia = copia.GetComponent<Collider2D>();

        // No choca con el recolector (su cápsula) ni con los otros proyectiles.
        foreach (var propio in GetComponentsInChildren<Collider2D>())
        {
            Physics2D.IgnoreCollision(colliderCopia, propio);
        }
        foreach (var otro in FindObjectsByType<Proyectil>())
        {
            if (otro.gameObject != copia)
            {
                Physics2D.IgnoreCollision(colliderCopia, otro.GetComponent<Collider2D>());
            }
        }

        copia.GetComponent<Proyectil>().Lanzar(direccion, Sentido());
    }

    static float LeerHorizontal(Keyboard teclado)
    {
        if (teclado == null)
        {
            return 0f;
        }

        float valor = 0f;
        if (teclado.leftArrowKey.isPressed || teclado.aKey.isPressed)
        {
            valor -= 1f;
        }
        if (teclado.rightArrowKey.isPressed || teclado.dKey.isPressed)
        {
            valor += 1f;
        }
        return valor;
    }
}
