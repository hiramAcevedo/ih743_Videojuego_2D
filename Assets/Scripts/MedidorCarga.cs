using UnityEngine;

// La barra de fuerza de la bolsa, encima de la cabeza del recolector. Mientras se sostiene la tecla se llena de
// izquierda a derecha en tiempoLlenado segundos y la fuerza del lanzamiento crece con ella:
//   tramo normal (gris)   la fuerza va de fuerzaMinima a 1 según lo lleno;
//   franja verde          el punto dulce: soltar ahí es un tiro perfecto, con la fuerza multiplicada por bonoPerfecto;
//   tramo rojo, al final  la bolsa ya está demasiado apretada: soltar ahí la rompe en la mano y cae a los pies (un
//                         lanzamiento inútil), y quedarse en rojo más de toleranciaRojo segundos la revienta encima del
//                         recolector, que pierde un corazón.
// El riesgo es el juego: entre más se carga más lejos llega, pero la franja verde dura poco más de un sexto de segundo.
// La barra se dibuja con un solo sprite de un píxel (escalado), en la cuadrícula de 48 píxeles por unidad del personaje.
// Es hija del recolector; como él se voltea con la escala en x, la barra se contra-voltea para no llenarse al revés.
public class MedidorCarga : MonoBehaviour
{
    public enum Resultado { Normal, Perfecto, Inutil }

    [Header("Tiempos")]
    [Tooltip("Segundos de vacío a lleno.")]
    [SerializeField] float tiempoLlenado = 1.1f;
    [Tooltip("Dónde empieza la franja verde, de 0 a 1.")]
    [SerializeField] float inicioVerde = 0.8f;
    [Tooltip("Dónde empieza el tramo rojo, de 0 a 1 (y termina la verde).")]
    [SerializeField] float inicioRojo = 0.95f;
    [Tooltip("Segundos que aguanta la bolsa en rojo antes de reventar.")]
    [SerializeField] float toleranciaRojo = 0.45f;

    [Header("Fuerza")]
    [Tooltip("Fuerza de un toque rápido: la bolsa sale con esta fracción de su rapidez.")]
    [SerializeField] float fuerzaMinima = 0.35f;
    [Tooltip("Multiplicador de un tiro perfecto.")]
    [SerializeField] float bonoPerfecto = 1.25f;

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
    static readonly Color Fondo = new Color32(70, 66, 74, 255);
    static readonly Color FondoVerde = new Color32(38, 92, 52, 255);
    static readonly Color FondoRojo = new Color32(110, 34, 38, 255);
    static readonly Color Llenado = new Color32(255, 226, 140, 255);
    static readonly Color LlenadoVerde = new Color32(120, 230, 110, 255);
    static readonly Color LlenadoRojo = new Color32(240, 70, 60, 255);
    static readonly Color Marca = new Color32(250, 250, 245, 255);

    SpriteRenderer relleno;
    float tiempo;

    public bool Activo { get; private set; }
    // Qué tan llena está, de 0 a 1.
    public float Carga { get; private set; }
    // Segundos que lleva en el tramo rojo.
    public float TiempoEnRojo { get; private set; }
    public bool EnVerde => Carga >= inicioVerde && Carga < inicioRojo;
    public bool EnRojo => Carga >= inicioRojo;
    public float TiempoLlenado => tiempoLlenado;
    public float InicioVerde => inicioVerde;
    public float InicioRojo => inicioRojo;
    public float ToleranciaRojo => toleranciaRojo;
    public float FuerzaMinima => fuerzaMinima;
    public float BonoPerfecto => bonoPerfecto;

    void Awake()
    {
        transform.localPosition = new Vector3(0f, altura, 0f);
        Construir();
        Mostrar(false);
    }

    void LateUpdate()
    {
        // El padre se voltea con la escala en x; la barra no.
        float signo = transform.parent != null && transform.parent.lossyScale.x < 0f ? -1f : 1f;
        transform.localScale = new Vector3(signo, 1f, 1f);
        if (!Activo || relleno == null)
        {
            return;
        }

        int px = Mathf.RoundToInt(Carga * anchoPx);
        relleno.transform.localScale = new Vector3(px, altoPx, 1f);
        if (EnRojo)
        {
            // En rojo parpadea: blanco y rojo a 12 veces por segundo.
            relleno.color = Mathf.FloorToInt(TiempoEnRojo * 12f) % 2 == 0 ? LlenadoRojo : Marca;
        }
        else
        {
            relleno.color = EnVerde ? LlenadoVerde : Llenado;
        }
    }

    public void Empezar()
    {
        Activo = true;
        tiempo = 0f;
        Carga = 0f;
        TiempoEnRojo = 0f;
        Mostrar(true);
    }

    // Avanza la carga; devuelve true si en este cuadro la bolsa reventó (ya no está activa).
    public bool Avanzar(float dt)
    {
        if (!Activo)
        {
            return false;
        }

        tiempo += dt;
        Carga = Mathf.Clamp01(tiempo / tiempoLlenado);
        if (EnRojo)
        {
            TiempoEnRojo += dt;
            if (TiempoEnRojo > toleranciaRojo)
            {
                Cancelar();
                return true;
            }
        }
        return false;
    }

    // Termina la carga y dice cómo sale la bolsa: con qué fracción de su rapidez y si fue perfecto o inútil.
    public Resultado Soltar(out float fuerza)
    {
        Resultado r;
        if (EnRojo)
        {
            r = Resultado.Inutil;
            fuerza = 0f;
        }
        else if (EnVerde)
        {
            r = Resultado.Perfecto;
            fuerza = bonoPerfecto;
        }
        else
        {
            r = Resultado.Normal;
            fuerza = Mathf.Lerp(fuerzaMinima, 1f, Carga / inicioVerde);
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

    // El marco, el fondo con sus franjas, dos marcas en los bordes de la franja verde y el relleno.
    void Construir()
    {
        if (pixel == null)
        {
            return;
        }

        float x0 = -anchoPx * 0.5f * Px;
        int verde0 = Mathf.RoundToInt(inicioVerde * anchoPx), rojo0 = Mathf.RoundToInt(inicioRojo * anchoPx);
        Rect("Marco", x0 - Px, 0f, anchoPx + 2, altoPx + 2, Marco, 0);
        Rect("Fondo", x0, 0f, anchoPx, altoPx, Fondo, 1);
        Rect("FondoVerde", x0 + verde0 * Px, 0f, rojo0 - verde0, altoPx, FondoVerde, 2);
        Rect("FondoRojo", x0 + rojo0 * Px, 0f, anchoPx - rojo0, altoPx, FondoRojo, 2);
        relleno = Rect("Relleno", x0, 0f, 0, altoPx, Llenado, 3);
        Rect("MarcaVerde", x0 + verde0 * Px, 0f, 1, altoPx + 4, Marca, 4);
        Rect("MarcaRoja", x0 + rojo0 * Px, 0f, 1, altoPx + 4, Marca, 4);
    }

    SpriteRenderer Rect(string nombre, float x, float y, int anchoPixeles, int altoPixeles, Color color, int capa)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(x, y, 0f);
        go.transform.localScale = new Vector3(anchoPixeles, altoPixeles, 1f);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = pixel;
        r.color = color;
        r.sortingOrder = ordenDibujo + capa;
        return r;
    }
}
