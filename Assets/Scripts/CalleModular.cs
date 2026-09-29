using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Calle modular: encadena fachadas en orden aleatorio con semilla fija, pinta el suelo en el Tilemap
// y repite el cielo hasta cubrir lo que ve la cámara más un margen; lo que queda atrás se recicla.
// Con la cámara quieta sólo llena la vista. Cuando la cámara siga al personaje, la calle avanza
// sin fin con este mismo código. El orden depende sólo de la semilla y de la posición de cada
// fachada, así que al regresar aparecen las mismas fachadas en el mismo lugar.
[DisallowMultipleComponent]
public class CalleModular : MonoBehaviour
{
    [Header("Fachadas")]
    [Tooltip("Sprites de las fachadas, con el pivote en la esquina inferior izquierda.")]
    [SerializeField] Sprite[] modulos = new Sprite[0];
    [Tooltip("Avance de cada fachada en unidades de mundo (ancho en píxeles entre PPU).")]
    [SerializeField] float[] anchos = new float[0];
    [Tooltip("Altura del mundo donde apoyan las fachadas: el borde superior del suelo.")]
    [SerializeField] float baseFachadas;
    [Tooltip("Donde empieza la fachada 0.")]
    [SerializeField] float origen;
    [SerializeField] int semilla = 743;
    [SerializeField] int ordenFachadas = -10;

    [Header("Suelo")]
    [SerializeField] Tilemap suelo;
    [Tooltip("Tile de cada fila del suelo, de arriba hacia abajo.")]
    [SerializeField] TileBase[] filasSuelo = new TileBase[0];

    [Header("Cielo")]
    [SerializeField] Sprite cielo;
    [Tooltip("Altura del mundo del borde inferior del cielo.")]
    [SerializeField] float baseCielo;
    [SerializeField] float origenCielo;
    [Tooltip("0: el cielo queda fijo en el mundo; 1: acompaña a la cámara. En medio, capa lejana.")]
    [Range(0f, 1f)] [SerializeField] float seguimientoCielo = 0.8f;
    [SerializeField] int ordenCielo = -20;

    [Header("Vista")]
    [SerializeField] Camera camara;
    [Tooltip("Unidades de mundo que se construyen de más a cada lado de la vista.")]
    [SerializeField] float margen = 4f;
    [Tooltip("Proporción mínima de pantalla que se cubre, por si la cámara aún no tiene tamaño.")]
    [SerializeField] float aspectoMinimo = 16f / 9f;

    struct Fachada
    {
        public int indice;
        public float x;
        public SpriteRenderer sr;
    }

    readonly List<Fachada> fachadas = new List<Fachada>();          // de izquierda a derecha
    readonly Stack<SpriteRenderer> libres = new Stack<SpriteRenderer>();
    readonly List<SpriteRenderer> piezasCielo = new List<SpriteRenderer>();
    readonly Dictionary<int, int[]> bolsas = new Dictionary<int, int[]>();
    Transform contFachadas, contCielo;
    int refIndice, colMin, colMax;
    float refX;
    bool conSuelo, preparado;

    void Start()
    {
        Reconstruir();
    }

    void LateUpdate()
    {
        if (!preparado) return;
        Vista(out float izq, out float der);
        Actualizar(izq, der);
    }

    // Arma la calle desde cero para la vista actual. En el editor deja la vista previa en la escena;
    // en juego reutiliza esas mismas piezas.
    public void Reconstruir()
    {
        contFachadas = Contenedor("Fachadas");
        contCielo = Contenedor("Cielo");
        fachadas.Clear();
        libres.Clear();
        piezasCielo.Clear();
        bolsas.Clear();
        conSuelo = false;
        refIndice = 0;
        refX = origen;
        // Se apilan al revés para que la primera fachada colocada reciba el primer hijo: así la vista
        // previa queda igual cada vez que se reconstruye con el mismo arte.
        for (int k = contFachadas.childCount - 1; k >= 0; k--)
        {
            var t = contFachadas.GetChild(k);
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr == null) continue;
            t.gameObject.SetActive(false);
            libres.Push(sr);
        }
        foreach (Transform t in contCielo)
        {
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr != null) piezasCielo.Add(sr);
        }
        preparado = true;
        Vista(out float izq, out float der);
        Actualizar(izq, der);
        if (!Application.isPlaying) DescartarSobrantes();
    }

    // Cubre el tramo [izquierda, derecha] del mundo y recicla lo que quede lejos de él.
    public void Actualizar(float izquierda, float derecha)
    {
        if (!preparado || modulos == null || modulos.Length == 0) return;
        ActualizarFachadas(izquierda, derecha);
        ActualizarSuelo(izquierda, derecha);
        ActualizarCielo(izquierda, derecha);
    }

    void Vista(out float izq, out float der)
    {
        var cam = camara != null ? camara : Camera.main;
        float x = cam != null ? cam.transform.position.x : 0f;
        float medio = cam != null ? cam.orthographicSize * Mathf.Max(cam.aspect, aspectoMinimo) : 12f;
        izq = x - medio - margen;
        der = x + medio + margen;
    }

    // ---------- fachadas ----------

    void ActualizarFachadas(float izq, float der)
    {
        if (fachadas.Count > 0 && (fachadas[0].x > der || Derecha(fachadas[fachadas.Count - 1]) < izq))
        {
            // La vista se fue lejos de lo construido: se recicla todo y se parte de la primera fachada.
            refIndice = fachadas[0].indice;
            refX = fachadas[0].x;
            while (fachadas.Count > 0) Quitar(fachadas.Count - 1);
        }
        if (fachadas.Count == 0)
        {
            int i = refIndice;
            float x = refX;
            while (x + Ancho(i) <= izq) { x += Ancho(i); i++; }
            while (x > izq) { i--; x -= Ancho(i); }
            Colocar(i, x, true);
        }
        while (Derecha(fachadas[fachadas.Count - 1]) < der)
        {
            var u = fachadas[fachadas.Count - 1];
            Colocar(u.indice + 1, Derecha(u), true);
        }
        while (fachadas[0].x > izq)
        {
            int i = fachadas[0].indice - 1;
            Colocar(i, fachadas[0].x - Ancho(i), false);
        }
        // Se recicla con un margen extra para no quitar y poner la misma pieza en el borde.
        while (fachadas.Count > 1 && Derecha(fachadas[0]) < izq - margen) Quitar(0);
        while (fachadas.Count > 1 && fachadas[fachadas.Count - 1].x > der + margen) Quitar(fachadas.Count - 1);
    }

    void Colocar(int indice, float x, bool alFinal)
    {
        var sr = libres.Count > 0 ? libres.Pop() : NuevaPieza(contFachadas);
        var sprite = modulos[ModuloEn(indice)];
        sr.sprite = sprite;
        sr.sortingOrder = ordenFachadas;
        sr.transform.position = new Vector3(x, baseFachadas, 0f);
        sr.gameObject.name = "Fachada " + indice + " " + sprite.name;
        sr.gameObject.SetActive(true);
        var f = new Fachada { indice = indice, x = x, sr = sr };
        if (alFinal) fachadas.Add(f);
        else fachadas.Insert(0, f);
    }

    void Quitar(int posicion)
    {
        var sr = fachadas[posicion].sr;
        sr.gameObject.SetActive(false);
        libres.Push(sr);
        fachadas.RemoveAt(posicion);
    }

    float Derecha(Fachada f) => f.x + Ancho(f.indice);

    float Ancho(int indice)
    {
        int m = ModuloEn(indice);
        if (anchos != null && m < anchos.Length && anchos[m] > 0f) return anchos[m];
        return modulos[m].rect.width / modulos[m].pixelsPerUnit;
    }

    // Bolsa barajada por bloques de n fachadas: cada bloque trae todas una vez y dos vecinas
    // nunca se repiten. Depende sólo de la semilla y del índice, a la derecha o a la izquierda.
    int ModuloEn(int indice)
    {
        int n = modulos.Length;
        if (n == 1) return 0;
        if (n == 2) return ((indice % 2) + 2) % 2;
        int bloque = Mathf.FloorToInt(indice / (float)n);
        if (!bolsas.TryGetValue(bloque, out var orden))
        {
            orden = Barajar(bloque);
            // Sólo se tocan las posiciones 0 y 1, así que la última del bloque anterior es la barajada.
            if (orden[0] == Barajar(bloque - 1)[n - 1]) (orden[0], orden[1]) = (orden[1], orden[0]);
            if (bolsas.Count > 256) bolsas.Clear();
            bolsas[bloque] = orden;
        }
        return orden[indice - bloque * n];
    }

    int[] Barajar(int bloque)
    {
        int n = modulos.Length;
        var orden = new int[n];
        for (int k = 0; k < n; k++) orden[k] = k;
        uint estado = Mezclar((uint)semilla * 0x9E3779B9u ^ (uint)bloque * 0x85EBCA6Bu);
        for (int k = n - 1; k > 0; k--)
        {
            estado = Mezclar(estado + 0x6D2B79F5u);
            int j = (int)(estado % (uint)(k + 1));
            (orden[k], orden[j]) = (orden[j], orden[k]);
        }
        return orden;
    }

    static uint Mezclar(uint x)
    {
        x ^= x >> 16;
        x *= 0x7FEB352Du;
        x ^= x >> 15;
        x *= 0x846CA68Bu;
        x ^= x >> 16;
        return x;
    }

    // ---------- suelo ----------

    void ActualizarSuelo(float izq, float der)
    {
        if (suelo == null || filasSuelo == null || filasSuelo.Length == 0) return;
        float x0 = suelo.CellToWorld(Vector3Int.zero).x;
        float ancho = suelo.layoutGrid.cellSize.x;
        int a = Mathf.FloorToInt((izq - x0) / ancho);
        int b = Mathf.FloorToInt((der - x0) / ancho);
        if (!conSuelo || b < colMin || a > colMax)
        {
            suelo.ClearAllTiles();
            Pintar(a, b, true);
            colMin = a;
            colMax = b;
            conSuelo = true;
            return;
        }
        if (a < colMin) { Pintar(a, colMin - 1, true); colMin = a; }
        if (b > colMax) { Pintar(colMax + 1, b, true); colMax = b; }
        int extra = Mathf.CeilToInt(margen / ancho);
        if (colMin < a - extra) { Pintar(colMin, a - extra - 1, false); colMin = a - extra; }
        if (colMax > b + extra) { Pintar(b + extra + 1, colMax, false); colMax = b + extra; }
    }

    void Pintar(int desde, int hasta, bool poner)
    {
        for (int c = desde; c <= hasta; c++)
            for (int f = 0; f < filasSuelo.Length; f++)
                suelo.SetTile(new Vector3Int(c, -1 - f, 0), poner ? filasSuelo[f] : null);
    }

    // ---------- cielo ----------

    void ActualizarCielo(float izq, float der)
    {
        if (cielo == null) return;
        var cam = camara != null ? camara : Camera.main;
        float camX = cam != null ? cam.transform.position.x : 0f;
        float ancho = cielo.rect.width / cielo.pixelsPerUnit;
        float inicio = origenCielo + camX * seguimientoCielo;
        int k0 = Mathf.FloorToInt((izq - inicio) / ancho);
        int k1 = Mathf.FloorToInt((der - inicio) / ancho);
        int n = k1 - k0 + 1;
        while (piezasCielo.Count < n) piezasCielo.Add(NuevaPieza(contCielo));
        for (int j = 0; j < piezasCielo.Count; j++)
        {
            var sr = piezasCielo[j];
            bool usar = j < n;
            if (sr.gameObject.activeSelf != usar) sr.gameObject.SetActive(usar);
            if (!usar) continue;
            if (sr.sprite != cielo) sr.sprite = cielo;
            sr.sortingOrder = ordenCielo;
            sr.transform.position = new Vector3(inicio + (k0 + j) * ancho, baseCielo, 0f);
            sr.gameObject.name = "Cielo " + (k0 + j);
        }
    }

    // ---------- piezas ----------

    Transform Contenedor(string nombre)
    {
        var t = transform.Find(nombre);
        if (t == null)
        {
            t = new GameObject(nombre).transform;
            t.SetParent(transform, false);
        }
        return t;
    }

    static SpriteRenderer NuevaPieza(Transform padre)
    {
        var go = new GameObject("Pieza");
        go.transform.SetParent(padre, false);
        return go.AddComponent<SpriteRenderer>();
    }

    // En el editor la vista previa se guarda en la escena: sin piezas sobrantes.
    void DescartarSobrantes()
    {
        while (libres.Count > 0) DestroyImmediate(libres.Pop().gameObject);
        for (int j = piezasCielo.Count - 1; j >= 0; j--)
        {
            if (piezasCielo[j].gameObject.activeSelf) continue;
            DestroyImmediate(piezasCielo[j].gameObject);
            piezasCielo.RemoveAt(j);
        }
    }
}
