using UnityEngine;

// El swing de la botella: un toque arranca una aguja que recorre la barra de izquierda a derecha y regresa, como el
// péndulo de un golpe de golf; el segundo toque lanza según dónde esté la aguja:
//   centro verde      tiro perfecto: la botella sale con la rapidez multiplicada por bonoPerfecto;
//   amarillo          tiro normal;
//   orillas rojas     se le resbala: la botella cae a los pies y se rompe (un lanzamiento inútil).
// Si la aguja termina la ida y la vuelta sin el segundo toque, también se resbala. La aguja va a velocidad constante,
// así que la franja verde dura lo mismo en la ida que en la vuelta.
public class MedidorSwing : MonoBehaviour
{
    public enum Resultado { Normal, Perfecto, Resbalon }

    [Header("Tiempos y zonas")]
    [Tooltip("Segundos que tarda la aguja en cruzar la barra (la vuelta tarda lo mismo).")]
    [SerializeField] float duracionIda = 0.7f;
    [Tooltip("Media anchura de la franja verde, alrededor del centro, de 0 a 0.5.")]
    [SerializeField] float medioVerde = 0.08f;
    [Tooltip("Media anchura de verde más amarillo, alrededor del centro, de 0 a 0.5.")]
    [SerializeField] float medioAmarillo = 0.28f;
    [Tooltip("Multiplicador de un tiro perfecto.")]
    [SerializeField] float bonoPerfecto = 1.35f;

    [Header("Dibujo")]
    [Tooltip("Sprite de un píxel blanco (48 por unidad, pivote a la izquierda).")]
    [SerializeField] Sprite pixel;
    [SerializeField] int anchoPx = 40;
    [SerializeField] int altoPx = 4;
    [Tooltip("Altura de la barra sobre los pies, en unidades.")]
    [SerializeField] float altura = 2.45f;
    [SerializeField] int ordenDibujo = 120;

    const float Px = 1f / 48f;
    static readonly Color Marco = new Color32(28, 22, 26, 255);
    static readonly Color Rojo = new Color32(176, 52, 52, 255);
    static readonly Color Amarillo = new Color32(232, 196, 72, 255);
    static readonly Color Verde = new Color32(92, 196, 92, 255);
    static readonly Color Aguja = new Color32(250, 250, 245, 255);

    Transform aguja;
    float tiempo;

    public bool Activo { get; private set; }
    // Posición de la aguja en la barra, de 0 (izquierda) a 1 (derecha).
    public float Posicion { get; private set; }
    public float DuracionIda => duracionIda;
    public float MedioVerde => medioVerde;
    public float MedioAmarillo => medioAmarillo;
    public float BonoPerfecto => bonoPerfecto;

    void Awake()
    {
        transform.localPosition = new Vector3(0f, altura, 0f);
        Construir();
        Mostrar(false);
    }

    void LateUpdate()
    {
        float signo = transform.parent != null && transform.parent.lossyScale.x < 0f ? -1f : 1f;
        transform.localScale = new Vector3(signo, 1f, 1f);
        if (Activo && aguja != null)
        {
            int px = Mathf.RoundToInt(Posicion * (anchoPx - 1));
            aguja.localPosition = new Vector3((-anchoPx * 0.5f + px - 1) * Px, 0f, 0f);
        }
    }

    public void Empezar()
    {
        Activo = true;
        tiempo = 0f;
        Posicion = 0f;
        Mostrar(true);
    }

    // Mueve la aguja; devuelve true si terminó la ida y la vuelta sin que se lanzara (la botella se resbala).
    public bool Avanzar(float dt)
    {
        if (!Activo)
        {
            return false;
        }

        tiempo += dt;
        float t = tiempo / duracionIda;
        if (t >= 2f)
        {
            Posicion = 0f;
            return true;
        }
        Posicion = t <= 1f ? t : 2f - t;
        return false;
    }

    // Dónde cayó la aguja: el resultado del lanzamiento y el multiplicador de rapidez.
    public Resultado Resolver(out float multiplicador)
    {
        float d = Mathf.Abs(Posicion - 0.5f);
        Resultado r;
        if (d <= medioVerde)
        {
            r = Resultado.Perfecto;
            multiplicador = bonoPerfecto;
        }
        else if (d <= medioAmarillo)
        {
            r = Resultado.Normal;
            multiplicador = 1f;
        }
        else
        {
            r = Resultado.Resbalon;
            multiplicador = 0f;
        }
        Cancelar();
        return r;
    }

    public void Cancelar()
    {
        Activo = false;
        Mostrar(false);
    }

    void Mostrar(bool visible)
    {
        foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
        {
            r.enabled = visible;
        }
    }

    // Marco, cinco zonas simétricas (rojo, amarillo, verde, amarillo, rojo) y la aguja.
    void Construir()
    {
        if (pixel == null)
        {
            return;
        }

        float x0 = -anchoPx * 0.5f * Px;
        int amarillo0 = Mathf.RoundToInt((0.5f - medioAmarillo) * anchoPx), verde0 = Mathf.RoundToInt((0.5f - medioVerde) * anchoPx);
        int verde1 = anchoPx - verde0, amarillo1 = anchoPx - amarillo0;
        Rect("Marco", x0 - Px, anchoPx + 2, altoPx + 2, Marco, 0);
        Rect("Rojo", x0, anchoPx, altoPx, Rojo, 1);
        Rect("Amarillo", x0 + amarillo0 * Px, amarillo1 - amarillo0, altoPx, Amarillo, 2);
        Rect("Verde", x0 + verde0 * Px, verde1 - verde0, altoPx, Verde, 3);
        var sombra = Rect("Aguja", x0, 3, altoPx + 6, Marco, 4).transform;
        var luz = Rect("Luz", 0f, 1, altoPx + 4, Aguja, 5).transform;
        luz.SetParent(sombra, true);
        luz.localPosition = new Vector3(1f / 3f, 0f, 0f);
        luz.localScale = new Vector3(1f / 3f, (altoPx + 4f) / (altoPx + 6f), 1f);
        aguja = sombra;
    }

    SpriteRenderer Rect(string nombre, float x, int anchoPixeles, int altoPixeles, Color color, int capa)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        go.transform.localScale = new Vector3(anchoPixeles, altoPixeles, 1f);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = pixel;
        r.color = color;
        r.sortingOrder = ordenDibujo + capa;
        return r;
    }
}
