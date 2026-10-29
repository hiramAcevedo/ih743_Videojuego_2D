using UnityEngine;

// El marcador, encima de la calle: arriba a la derecha las propinas (la moneda y el total), arriba al centro el nivel en
// juego, abajo a la izquierda lo que pasa con la base de datos (conectando, guardado, sin conexión) y, al centro, los
// avisos grandes (nivel completo) y el velo de "cargando partida" mientras llega de Firestore. Como los corazones de
// Salud: sprites hijos de la cámara, colocados en cada cuadro contra los bordes de la vista, a 4 píxeles de pantalla por
// píxel de arte; el texto es TextoPixel.
public class Marcador : MonoBehaviour
{
    [Tooltip("La moneda de 9x9 del marcador (HUD/moneda.png).")]
    [SerializeField] Sprite iconoMoneda;
    [Tooltip("Un píxel blanco (HUD/pixel.png) para el velo.")]
    [SerializeField] Sprite pixel;
    [Tooltip("Separación del borde de la vista, en unidades.")]
    [SerializeField] float margen = 0.35f;
    [SerializeField] float escala = 2f;
    [SerializeField] float escalaCargando = 3f;
    [SerializeField] float escalaAviso = 4f;
    [SerializeField] int ordenDibujo = 200;
    [SerializeField] Color colorTexto = new Color32(255, 244, 214, 255);
    [SerializeField] Color colorMonedas = new Color32(255, 214, 92, 255);
    [SerializeField] Color colorBase = new Color32(170, 226, 255, 255);
    [SerializeField] Color colorAlerta = new Color32(255, 150, 120, 255);
    [SerializeField] Color colorVelo = new Color(0.07f, 0.04f, 0.09f, 0.72f);
    [Tooltip("Segundos que el total crece un poco al sumar una propina.")]
    [SerializeField] float duracionSalto = 0.2f;

    Camera camara;
    Transform raiz;
    SpriteRenderer moneda, velo;
    TextoPixel monedas, nivel, estado, aviso, cargando;
    float finEstado = -1f, finAviso = -1f, finSalto = -1f;
    int total = -1;

    public Sprite Pixel => pixel;
    public Color ColorBase => colorBase;
    public Color ColorAlerta => colorAlerta;
    public string TextoMonedas => monedas != null ? monedas.Texto : "";
    public string TextoNivel => nivel != null ? nivel.Texto : "";
    public string TextoEstado => Visible(estado) ? estado.Texto : "";
    public string TextoAviso => Visible(aviso) ? aviso.Texto : "";
    public string TextoCargando => Visible(cargando) ? cargando.Texto : "";

    void Awake()
    {
        camara = Camera.main;
        if (camara == null)
        {
            return;
        }
        raiz = new GameObject("Marcador").transform;
        raiz.SetParent(camara.transform, false);
        moneda = Imagen("Moneda", iconoMoneda, ordenDibujo);
        monedas = Texto("Propinas", colorMonedas, TextoPixel.Alineacion.Derecha, ordenDibujo, escala);
        nivel = Texto("Nivel", colorTexto, TextoPixel.Alineacion.Centro, ordenDibujo, escala);
        estado = Texto("Base de datos", colorBase, TextoPixel.Alineacion.Izquierda, ordenDibujo, escala);
        velo = Imagen("Velo", pixel, ordenDibujo + 10);
        velo.color = colorVelo;
        velo.gameObject.SetActive(false);
        cargando = Texto("Cargando", colorTexto, TextoPixel.Alineacion.Centro, ordenDibujo + 11, escalaCargando);
        cargando.gameObject.SetActive(false);
        aviso = Texto("Aviso", colorMonedas, TextoPixel.Alineacion.Centro, ordenDibujo + 12, escalaAviso);
        aviso.gameObject.SetActive(false);
        Monedas(0);
    }

    public void Monedas(int nuevoTotal)
    {
        if (monedas == null)
        {
            return;
        }
        if (total >= 0 && nuevoTotal > total)
        {
            finSalto = Time.time + duracionSalto;
        }
        total = nuevoTotal;
        monedas.Texto = nuevoTotal.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public void Nivel(string nombre)
    {
        if (nivel != null)
        {
            nivel.Texto = nombre;
        }
    }

    // segundos <= 0: se queda hasta el siguiente.
    public void Estado(string texto, Color color, float segundos)
    {
        if (estado == null)
        {
            return;
        }
        estado.Color = color;
        estado.Texto = texto;
        estado.gameObject.SetActive(!string.IsNullOrEmpty(texto));
        finEstado = segundos > 0f ? Time.time + segundos : -1f;
    }

    public void Aviso(string texto, float segundos)
    {
        if (aviso == null)
        {
            return;
        }
        aviso.Texto = texto;
        aviso.gameObject.SetActive(!string.IsNullOrEmpty(texto));
        finAviso = segundos > 0f ? Time.time + segundos : -1f;
    }

    // Con texto, el velo y el aviso de carga encima de todo; con null, se quitan.
    public void Cargando(string texto)
    {
        if (cargando == null)
        {
            return;
        }
        bool hay = !string.IsNullOrEmpty(texto);
        cargando.Texto = texto ?? "";
        cargando.gameObject.SetActive(hay);
        velo.gameObject.SetActive(hay);
    }

    // En cada cuadro, por si cambia el tamaño de la vista.
    void LateUpdate()
    {
        if (raiz == null)
        {
            return;
        }
        if (finEstado > 0f && Time.time >= finEstado)
        {
            estado.gameObject.SetActive(false);
            finEstado = -1f;
        }
        if (finAviso > 0f && Time.time >= finAviso)
        {
            aviso.gameObject.SetActive(false);
            finAviso = -1f;
        }

        float alto = camara.orthographicSize, ancho = alto * camara.aspect, z = -camara.transform.position.z;
        float arriba = alto - margen - monedas.AltoMundo / (2f * monedas.transform.localScale.y) * escala;
        float crece = Time.time < finSalto ? 1f + 0.25f * ((finSalto - Time.time) / duracionSalto) : 1f;
        monedas.transform.localScale = new Vector3(escala * crece, escala * crece, 1f);
        monedas.transform.localPosition = new Vector3(ancho - margen, arriba, z);
        if (iconoMoneda != null)
        {
            float anchoIcono = iconoMoneda.rect.width / iconoMoneda.pixelsPerUnit * escala;
            moneda.transform.localScale = new Vector3(escala * crece, escala * crece, 1f);
            moneda.transform.localPosition = new Vector3(ancho - margen - monedas.Ancho - 0.15f - anchoIcono / 2f, arriba, z);
        }
        nivel.transform.localPosition = new Vector3(0f, arriba, z);
        estado.transform.localPosition = new Vector3(-ancho + margen, -alto + margen + estado.AltoMundo / 2f, z);
        aviso.transform.localPosition = new Vector3(0f, alto * 0.3f, z);
        cargando.transform.localPosition = new Vector3(0f, 0f, z);
        if (pixel != null)
        {
            // El píxel tiene el pivote en su orilla izquierda (así crece la barra de la bolsa): el velo se corre la mitad de
            // su tamaño para quedar centrado en la cámara.
            float lado = pixel.rect.width / pixel.pixelsPerUnit;
            var tamano = new Vector3(2f * ancho / lado + 2f, 2f * alto / lado + 2f, 1f);
            Vector2 pivote = pixel.pivot / pixel.rect.size;
            velo.transform.localScale = tamano;
            velo.transform.localPosition = new Vector3((pivote.x - 0.5f) * lado * tamano.x, (pivote.y - 0.5f) * lado * tamano.y, z);
        }
    }

    SpriteRenderer Imagen(string nombre, Sprite sprite, int orden)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(raiz, false);
        go.transform.localScale = new Vector3(escala, escala, 1f);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.sortingOrder = orden;
        return r;
    }

    TextoPixel Texto(string nombre, Color color, TextoPixel.Alineacion alineacion, int orden, float tamano)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(raiz, false);
        go.transform.localScale = new Vector3(tamano, tamano, 1f);
        go.AddComponent<SpriteRenderer>();
        var t = go.AddComponent<TextoPixel>();
        t.Configurar(color, alineacion, orden);
        return t;
    }

    static bool Visible(TextoPixel t) => t != null && t.gameObject.activeSelf;
}
