using UnityEngine;
using UnityEngine.InputSystem;

// Estructura del personaje: lee el teclado y elige la animación de estado.
// Flecha derecha o D: Correr mirando a la derecha. Flecha izquierda o A: Correr mirando
// a la izquierda. Sin dirección (o las dos a la vez): Reposo, sin cambiar hacia dónde mira.
// El desplazamiento, la física y el salto se agregan en las entregas siguientes.
[RequireComponent(typeof(Animator))]
public class RecolectorControl : MonoBehaviour
{
    static readonly int Corriendo = Animator.StringToHash("corriendo");

    Animator animator;

    // -1 izquierda, 0 sin dirección, 1 derecha; como Input.GetAxisRaw("Horizontal").
    public float Horizontal { get; private set; }

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        Horizontal = LeerHorizontal();

        animator.SetBool(Corriendo, Horizontal != 0f);

        // La escala negativa en x voltea el sprite y, más adelante, al punto de disparo.
        if (Horizontal < 0f)
        {
            transform.localScale = new Vector3(-1f, 1f, 1f);
        }
        else if (Horizontal > 0f)
        {
            transform.localScale = new Vector3(1f, 1f, 1f);
        }
    }

    static float LeerHorizontal()
    {
        var teclado = Keyboard.current;
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
