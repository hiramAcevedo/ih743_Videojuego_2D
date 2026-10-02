using UnityEngine;
using UnityEngine.InputSystem;

// Estructura del personaje: lee el teclado y elige la animación de estado.
// Flecha derecha o D: Correr mirando a la derecha. Flecha izquierda o A: Correr mirando
// a la izquierda. Sin dirección (o las dos a la vez): Reposo, sin cambiar hacia dónde mira.
// K sostenida: aparece la mira y oscila delante del recolector; al soltar K sale la bolsa hacia la mira.
// Barra espaciadora: sale la botella, recta y horizontal, hacia donde mira.
// El desplazamiento, la física y el salto del recolector se agregan en las entregas siguientes.
[RequireComponent(typeof(Animator))]
public class RecolectorControl : MonoBehaviour
{
    static readonly int Corriendo = Animator.StringToHash("corriendo");

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

    // -1 izquierda, 0 sin dirección, 1 derecha; como Input.GetAxisRaw("Horizontal").
    public float Horizontal { get; private set; }
    // Ángulo de la mira en el último lanzamiento de bolsa (grados sobre la horizontal, hacia donde mira).
    public float UltimoAngulo { get; private set; }

    void Start()
    {
        animator = GetComponent<Animator>();
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

        if (teclado == null)
        {
            return;
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

        // No choca con el recolector (cuando tenga colliders, en la 3.3) ni con los otros proyectiles.
        foreach (var propio in GetComponentsInChildren<Collider2D>())
        {
            Physics2D.IgnoreCollision(colliderCopia, propio);
        }
        foreach (var otro in FindObjectsByType<Proyectil>(FindObjectsSortMode.None))
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
