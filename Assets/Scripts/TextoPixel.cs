using System.Collections.Generic;
using UnityEngine;

// Texto en pixel art para el marcador y los avisos: la tipografía de 4x5 píxeles de los letreros de las fachadas, con las
// vocales acentuadas, la Ñ y los signos de apertura del español. Cada letra mide 5 píxeles de alto más dos arriba para el
// acento, y lleva una sombra de un píxel abajo a la derecha, como los letreros. Todo se escribe en mayúsculas. El texto
// completo es un solo sprite que se arma en una textura sin suavizado y se vuelve a armar sólo cuando cambia.
[RequireComponent(typeof(SpriteRenderer))]
public class TextoPixel : MonoBehaviour
{
    public enum Alineacion { Izquierda, Centro, Derecha }

    const int Alto = 7;

    // Cada letra, fila por fila de arriba hacia abajo; '#' es un píxel. Las de 5 filas reciben dos vacías arriba.
    static readonly Dictionary<char, string[]> Letras = CrearLetras();

    [SerializeField] string texto = "";
    [SerializeField] Color color = Color.white;
    [Tooltip("Color de la sombra, un píxel abajo a la derecha. Con alfa 0 no hay sombra.")]
    [SerializeField] Color sombra = new Color32(28, 15, 30, 255);
    [SerializeField] Alineacion alineacion = Alineacion.Izquierda;
    [Tooltip("Píxeles de la tipografía por unidad del mundo. Con 48, como el HUD, y escala 2, cada píxel mide 4 de la pantalla a 1080.")]
    [SerializeField] float pixelesPorUnidad = 48f;

    SpriteRenderer dibujo;
    Texture2D textura;
    Sprite sprite;
    string armado;
    Color colorArmado, sombraArmada;

    public string Texto
    {
        get => texto;
        set
        {
            texto = value ?? "";
            Armar();
        }
    }

    public Color Color
    {
        get => color;
        set
        {
            color = value;
            Armar();
        }
    }

    public SpriteRenderer Dibujo => dibujo != null ? dibujo : dibujo = GetComponent<SpriteRenderer>();
    // Lo que mide en el mundo, ya con la escala.
    public float Ancho => sprite != null ? sprite.rect.width / pixelesPorUnidad * Mathf.Abs(transform.lossyScale.x) : 0f;
    public float AltoMundo => (Alto + 1) / pixelesPorUnidad * Mathf.Abs(transform.lossyScale.y);

    void Awake()
    {
        Armar();
    }

    void OnDestroy()
    {
        Liberar();
    }

    public void Configurar(Color nuevoColor, Alineacion nuevaAlineacion, int orden)
    {
        color = nuevoColor;
        alineacion = nuevaAlineacion;
        Dibujo.sortingOrder = orden;
        armado = null;
        Armar();
    }

    void Armar()
    {
        if (armado == texto && colorArmado == color && sombraArmada == sombra && sprite != null)
        {
            return;
        }
        Liberar();
        armado = texto;
        colorArmado = color;
        sombraArmada = sombra;
        if (string.IsNullOrEmpty(texto))
        {
            Dibujo.sprite = null;
            return;
        }

        string s = texto.ToUpperInvariant();
        int ancho = 0;
        foreach (char c in s)
        {
            ancho += Letra(c)[0].Length + 1;
        }
        // La última letra no lleva espacio después; la sombra ocupa una columna y una fila más.
        int w = ancho, h = Alto + 1;
        textura = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "Texto " + s
        };
        var px = new Color32[w * h];
        int x = 0;
        foreach (char c in s)
        {
            var filas = Letra(c);
            for (int j = 0; j < filas.Length; j++)
            {
                for (int i = 0; i < filas[j].Length; i++)
                {
                    if (filas[j][i] != '#')
                    {
                        continue;
                    }
                    // La textura cuenta las filas desde abajo.
                    int y = h - 1 - j;
                    if (sombra.a > 0f)
                    {
                        Poner(px, w, x + i + 1, y - 1, sombra, true);
                    }
                    Poner(px, w, x + i, y, color, false);
                }
            }
            x += filas[0].Length + 1;
        }
        textura.SetPixels32(px);
        textura.Apply(false, true);
        float pivoteX = alineacion == Alineacion.Izquierda ? 0f : alineacion == Alineacion.Centro ? 0.5f : 1f;
        sprite = Sprite.Create(textura, new Rect(0, 0, w, h), new Vector2(pivoteX, 0.5f), pixelesPorUnidad, 0, SpriteMeshType.FullRect);
        sprite.name = textura.name;
        Dibujo.sprite = sprite;
    }

    // La sombra no tapa una letra ya puesta; la letra sí tapa una sombra.
    static void Poner(Color32[] px, int w, int x, int y, Color c, bool esSombra)
    {
        if (x < 0 || y < 0 || x >= w || y >= px.Length / w)
        {
            return;
        }
        int k = y * w + x;
        if (esSombra && px[k].a > 0)
        {
            return;
        }
        px[k] = c;
    }

    void Liberar()
    {
        if (sprite != null)
        {
            Destroy(sprite);
            sprite = null;
        }
        if (textura != null)
        {
            Destroy(textura);
            textura = null;
        }
    }

    static string[] Letra(char c)
    {
        return Letras.TryGetValue(c, out var filas) ? filas : Letras[' '];
    }

    static Dictionary<char, string[]> CrearLetras()
    {
        // Las de los letreros de las fachadas (pixlib, 3.1).
        var basicas = new Dictionary<char, string[]>
        {
            ['A'] = new[] { ".##.", "#..#", "####", "#..#", "#..#" },
            ['B'] = new[] { "###.", "#..#", "###.", "#..#", "###." },
            ['C'] = new[] { ".###", "#...", "#...", "#...", ".###" },
            ['D'] = new[] { "###.", "#..#", "#..#", "#..#", "###." },
            ['E'] = new[] { "####", "#...", "###.", "#...", "####" },
            ['F'] = new[] { "####", "#...", "###.", "#...", "#..." },
            ['G'] = new[] { ".###", "#...", "#.##", "#..#", ".###" },
            ['H'] = new[] { "#..#", "#..#", "####", "#..#", "#..#" },
            ['I'] = new[] { "###", ".#.", ".#.", ".#.", "###" },
            ['J'] = new[] { "..##", "...#", "...#", "#..#", ".##." },
            ['K'] = new[] { "#..#", "#.#.", "##..", "#.#.", "#..#" },
            ['L'] = new[] { "#...", "#...", "#...", "#...", "####" },
            ['M'] = new[] { "#...#", "##.##", "#.#.#", "#...#", "#...#" },
            ['N'] = new[] { "#..#", "##.#", "#.##", "#..#", "#..#" },
            ['O'] = new[] { ".##.", "#..#", "#..#", "#..#", ".##." },
            ['P'] = new[] { "###.", "#..#", "#..#", "###.", "#..." },
            ['Q'] = new[] { ".##.", "#..#", "#..#", "#.##", ".###" },
            ['R'] = new[] { "###.", "#..#", "###.", "#.#.", "#..#" },
            ['S'] = new[] { ".###", "#...", ".##.", "...#", "###." },
            ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#.." },
            ['U'] = new[] { "#..#", "#..#", "#..#", "#..#", ".##." },
            ['V'] = new[] { "#...#", "#...#", ".#.#.", ".#.#.", "..#.." },
            ['W'] = new[] { "#...#", "#...#", "#.#.#", "##.##", "#...#" },
            ['X'] = new[] { "#..#", "#..#", ".##.", "#..#", "#..#" },
            ['Y'] = new[] { "#...#", ".#.#.", "..#..", "..#..", "..#.." },
            ['Z'] = new[] { "####", "...#", ".##.", "#...", "####" },
            ['0'] = new[] { ".##.", "#..#", "#..#", "#..#", ".##." },
            ['1'] = new[] { ".#.", "##.", ".#.", ".#.", "###" },
            ['2'] = new[] { ".##.", "#..#", "..#.", ".#..", "####" },
            ['3'] = new[] { "###.", "...#", ".##.", "...#", "###." },
            ['4'] = new[] { "#..#", "#..#", "####", "...#", "...#" },
            ['5'] = new[] { "####", "#...", "###.", "...#", "###." },
            ['6'] = new[] { ".###", "#...", "###.", "#..#", ".##." },
            ['7'] = new[] { "####", "...#", "..#.", ".#..", ".#.." },
            ['8'] = new[] { ".##.", "#..#", ".##.", "#..#", ".##." },
            ['9'] = new[] { ".##.", "#..#", ".###", "...#", "###." },
            ['.'] = new[] { ".", ".", ".", ".", "#" },
            ['!'] = new[] { "#", "#", "#", ".", "#" },
            ['-'] = new[] { "...", "...", "###", "...", "..." },
            [' '] = new[] { "..", "..", "..", "..", ".." },
            ['$'] = new[] { ".###.", "#.#..", ".###.", "..#.#", ".###." },
            // Las que faltaban para los avisos.
            [':'] = new[] { ".", "#", ".", "#", "." },
            [','] = new[] { "..", "..", "..", ".#", "#." },
            ['/'] = new[] { "..#", "..#", ".#.", "#..", "#.." },
            ['?'] = new[] { "###.", "...#", ".##.", "....", ".#.." },
            ['¿'] = new[] { "..#.", "....", ".##.", "#...", ".###" },
            ['¡'] = new[] { "#", ".", "#", "#", "#" },
            ['('] = new[] { ".#", "#.", "#.", "#.", ".#" },
            [')'] = new[] { "#.", ".#", ".#", ".#", "#." },
            ['+'] = new[] { "...", ".#.", "###", ".#.", "..." },
            ['·'] = new[] { ".", ".", "#", ".", "." },
            ['↓'] = new[] { "..#..", "..#..", "#.#.#", ".###.", "..#.." },
        };
        var letras = new Dictionary<char, string[]>();
        foreach (var par in basicas)
        {
            letras[par.Key] = Arriba(par.Value, null, null);
        }
        // Acento agudo: dos píxeles en diagonal sobre la letra; tilde de la Ñ: una onda.
        letras['Á'] = Acento(basicas['A']);
        letras['É'] = Acento(basicas['E']);
        letras['Í'] = Acento(basicas['I']);
        letras['Ó'] = Acento(basicas['O']);
        letras['Ú'] = Acento(basicas['U']);
        letras['Ñ'] = Arriba(basicas['N'], ".#.#", "#.#.");
        letras['Ü'] = Arriba(basicas['U'], "....", "#..#");
        return letras;
    }

    static string[] Acento(string[] letra)
    {
        int w = letra[0].Length;
        var a = new string('.', w).ToCharArray();
        var b = new string('.', w).ToCharArray();
        a[Mathf.Min(w - 1, w / 2 + 1)] = '#';
        b[w / 2] = '#';
        return Arriba(letra, new string(a), new string(b));
    }

    static string[] Arriba(string[] letra, string fila0, string fila1)
    {
        int w = letra[0].Length;
        var filas = new string[Alto];
        filas[0] = fila0 ?? new string('.', w);
        filas[1] = fila1 ?? new string('.', w);
        for (int j = 0; j < letra.Length; j++)
        {
            filas[j + 2] = letra[j];
        }
        return filas;
    }
}
