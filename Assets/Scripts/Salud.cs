using UnityEngine;

// Los corazones del recolector. Un golpe (por ahora, la bolsa que le revienta en la mano) le quita uno, lo empuja hacia
// atrás, lo deja aturdido un momento (no camina ni lanza) y lo hace parpadear en rojo mientras es invulnerable. Sin
// corazones se desmaya un segundo y se levanta con los tres de nuevo, en el mismo lugar. Los corazones se dibujan en la
// esquina superior izquierda de la vista, como hijos de la cámara, con dos sprites de 9x8 píxeles: lleno y vacío.
[RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
public class Salud : MonoBehaviour
{
    [SerializeField] int maximo = 3;
    [Tooltip("Segundos sin poder moverse ni lanzar después de un golpe.")]
    [SerializeField] float aturdimiento = 0.4f;
    [Tooltip("Segundos sin recibir otro golpe, parpadeando.")]
    [SerializeField] float invulnerable = 1.2f;
    [Tooltip("Segundos de desmayo al quedarse sin corazones.")]
    [SerializeField] float desmayo = 1f;
    [Tooltip("Velocidad del empujón: x hacia atrás, y hacia arriba, en unidades por segundo.")]
    [SerializeField] Vector2 empuje = new Vector2(3.5f, 3.5f);
    [Tooltip("Lo que parpadea en rojo: las piernas (el SpriteRenderer del recolector) y el torso. Vacío, sólo el del recolector.")]
    [SerializeField] SpriteRenderer[] partes;

    [Header("Corazones en pantalla")]
    [SerializeField] Sprite corazonLleno;
    [SerializeField] Sprite corazonVacio;
    [Tooltip("Separación del borde de la vista, en unidades.")]
    [SerializeField] float margen = 0.35f;
    [SerializeField] float escala = 2f;
    [SerializeField] int ordenDibujo = 200;

    static readonly Color Tinte = new Color(1f, 0.45f, 0.45f);

    Rigidbody2D cuerpo;
    SpriteRenderer dibujo;
    SpriteRenderer[] corazones;
    Camera camara;
    float finAturdido = -1f, finInvulnerable = -1f;

    public int Maximo => maximo;
    public int Vidas { get; private set; }
    public int Golpes { get; private set; }
    public int Desmayos { get; private set; }
    public bool Aturdido => Time.time < finAturdido;
    public bool Invulnerable => Time.time < finInvulnerable;

    void Awake()
    {
        cuerpo = GetComponent<Rigidbody2D>();
        dibujo = GetComponent<SpriteRenderer>();
        Vidas = maximo;
    }

    void Start()
    {
        CrearCorazones();
        Pintar();
    }

    // Un golpe que lo empuja hacia el lado sentidoEmpuje (1 derecha, -1 izquierda). Si es invulnerable no le pasa nada.
    public bool Herir(int sentidoEmpuje)
    {
        if (Invulnerable)
        {
            return false;
        }

        Vidas--;
        Golpes++;
        finAturdido = Time.time + aturdimiento;
        finInvulnerable = Time.time + invulnerable;
        cuerpo.linearVelocity = new Vector2(sentidoEmpuje * empuje.x, empuje.y);
        if (Vidas <= 0)
        {
            // Se desmaya y se levanta con todos los corazones.
            Desmayos++;
            Vidas = maximo;
            finAturdido = Time.time + desmayo;
            finInvulnerable = Time.time + desmayo + invulnerable;
        }
        Pintar();
        return true;
    }

    void Update()
    {
        var color = Invulnerable && Mathf.FloorToInt(Time.time * 12f) % 2 == 0 ? Tinte : Color.white;
        if (partes == null || partes.Length == 0)
        {
            dibujo.color = color;
            return;
        }
        foreach (var parte in partes)
        {
            if (parte != null)
            {
                parte.color = color;
            }
        }
    }

    void CrearCorazones()
    {
        var cam = Camera.main;
        if (cam == null || corazonLleno == null)
        {
            return;
        }

        camara = cam;
        var contenedor = new GameObject("Corazones").transform;
        contenedor.SetParent(cam.transform, false);
        corazones = new SpriteRenderer[maximo];
        for (int i = 0; i < maximo; i++)
        {
            var go = new GameObject("Corazon " + (i + 1));
            go.transform.SetParent(contenedor, false);
            go.transform.localScale = new Vector3(escala, escala, 1f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sortingOrder = ordenDibujo;
            corazones[i] = r;
        }
        Colocar();
    }

    // En cada cuadro, por si cambia el tamaño de la vista: la esquina superior izquierda de lo que ve la cámara.
    void LateUpdate()
    {
        Colocar();
    }

    void Colocar()
    {
        if (corazones == null || camara == null)
        {
            return;
        }
        float alto = camara.orthographicSize, ancho = alto * camara.aspect;
        float paso = (corazonLleno.rect.width + 2f) / corazonLleno.pixelsPerUnit * escala;
        for (int i = 0; i < corazones.Length; i++)
        {
            corazones[i].transform.localPosition = new Vector3(-ancho + margen + i * paso, alto - margen, -camara.transform.position.z);
        }
    }

    void Pintar()
    {
        if (corazones == null)
        {
            return;
        }
        for (int i = 0; i < corazones.Length; i++)
        {
            corazones[i].sprite = i < Vidas ? corazonLleno : corazonVacio;
        }
    }
}
