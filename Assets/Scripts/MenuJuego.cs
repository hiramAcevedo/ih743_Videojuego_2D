using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// Los menús del juego, dibujados como el marcador: sprites hijos de la cámara, texto TextoPixel y un velo encima de la calle.
//   Inicio  al abrir el juego: NUEVA PARTIDA, CONTINUAR y SALIR. Debajo del título se ve lo que hay guardado en Firestore;
//           CONTINUAR sólo se puede elegir cuando ya llegó la partida y tiene avance (propinas o una ruta completa).
//   Pausa   con Esc mientras se juega (el juego se detiene): CONTINUAR, MENÚ PRINCIPAL y SALIR. Esc también vuelve al juego.
//   Fin     al completar las dos rutas: JUGAR DE NUEVO (una partida nueva desde la ruta 1), MENÚ PRINCIPAL y SALIR.
// Se elige con las flechas (o W y S) y Enter (o la barra espaciadora), o con el ratón. GestorNiveles decide qué hace cada
// opción; SALIR cierra el programa con Application.Quit, como el script de pausa del video de apoyo.
public class MenuJuego : MonoBehaviour
{
    public enum Tipo { Inicio, Pausa, Fin }
    public enum Opcion { Ninguna, NuevaPartida, Continuar, JugarDeNuevo, MenuPrincipal, Salir }

    const int Orden = 300;
    const float EscalaOpcion = 3f, EscalaNota = 2f;
    // Los colores del marcador: el texto, el dorado de las propinas, el azul de la base y el de las alertas.
    static readonly Color ColorTexto = new Color32(255, 244, 214, 255);
    static readonly Color ColorElegida = new Color32(255, 214, 92, 255);
    static readonly Color ColorApagada = new Color32(132, 120, 134, 255);
    static readonly Color ColorBase = new Color32(170, 226, 255, 255);
    static readonly Color ColorAlerta = new Color32(255, 150, 120, 255);
    // El velo es más opaco que el de carga del marcador: la mezcla es en espacio lineal y con 0.93 la calle queda a un
    // tercio de su brillo, lo bastante oscura para leer las opciones encima de los letreros de las fachadas.
    static readonly Color ColorVelo = new Color(0.07f, 0.04f, 0.09f, 0.93f);

    Camera camara;
    Transform raiz;
    SpriteRenderer velo;
    Sprite pixel;
    TextoPixel titulo, nota, ayuda;
    TextoPixel[] textos = new TextoPixel[0];
    Opcion[] opciones = new Opcion[0];
    Opcion resultado;
    int abiertoEn = -1;
    Vector2 ratonAntes;
    float escalaTitulo = 6f;

    public bool Abierto { get; private set; }
    public Tipo TipoActual { get; private set; }
    public int Seleccion { get; private set; }
    public Opcion Elegida => Seleccion >= 0 && Seleccion < opciones.Length ? opciones[Seleccion] : Opcion.Ninguna;
    public string Titulo => titulo != null ? titulo.Texto : "";
    public string Nota => nota != null && nota.gameObject.activeSelf ? nota.Texto : "";
    public Opcion[] Opciones => opciones.ToArray();

    public void Preparar(Marcador marcador)
    {
        camara = Camera.main;
        pixel = marcador != null ? marcador.Pixel : null;
        if (camara == null || raiz != null)
        {
            return;
        }
        raiz = new GameObject("Menu").transform;
        raiz.SetParent(camara.transform, false);
        velo = new GameObject("Velo del menú").AddComponent<SpriteRenderer>();
        velo.transform.SetParent(raiz, false);
        velo.sprite = pixel;
        velo.color = ColorVelo;
        velo.sortingOrder = Orden;
        titulo = Texto("Título", ColorElegida, Orden + 1, escalaTitulo);
        nota = Texto("Nota", ColorBase, Orden + 1, EscalaNota);
        ayuda = Texto("Ayuda", ColorApagada, Orden + 1, EscalaNota);
        raiz.gameObject.SetActive(false);
    }

    // Abre el menú y termina cuando se elige una opción; la avisa con elegida.
    public IEnumerator Mostrar(Tipo tipo, Action<Opcion> elegida)
    {
        Abrir(tipo);
        while (resultado == Opcion.Ninguna)
        {
            yield return null;
        }
        var r = resultado;
        Cerrar();
        elegida?.Invoke(r);
    }

    // Lo mismo que elegir con el teclado o el ratón (si la opción se puede elegir en este momento).
    public bool Elegir(Opcion opcion)
    {
        if (!Abierto || !Activa(opcion))
        {
            return false;
        }
        resultado = opcion;
        return true;
    }

    public bool Activa(Opcion opcion)
    {
        if (opcion != Opcion.Continuar || TipoActual != Tipo.Inicio)
        {
            return Array.IndexOf(opciones, opcion) >= 0;
        }
        var bd = BaseDeDatos.Instancia;
        return bd != null && bd.Partida != null && HayAvance(bd.Partida);
    }

    public static bool HayAvance(Partida p) => p.Monedas > 0 || p.nivelAlcanzado > 1 || p.niveles.Any(n => n.completado);

    void Abrir(Tipo tipo)
    {
        TipoActual = tipo;
        resultado = Opcion.Ninguna;
        abiertoEn = Time.frameCount;
        switch (tipo)
        {
            case Tipo.Inicio:
                opciones = new[] { Opcion.NuevaPartida, Opcion.Continuar, Opcion.Salir };
                escalaTitulo = 6f;
                titulo.Texto = "EL RECOLECTOR";
                ayuda.Texto = "FLECHAS Y ENTER, O EL RATÓN";
                break;
            case Tipo.Pausa:
                opciones = new[] { Opcion.Continuar, Opcion.MenuPrincipal, Opcion.Salir };
                escalaTitulo = 5f;
                titulo.Texto = "PAUSA";
                ayuda.Texto = "ESC: VOLVER AL JUEGO";
                break;
            default:
                opciones = new[] { Opcion.JugarDeNuevo, Opcion.MenuPrincipal, Opcion.Salir };
                escalaTitulo = 4f;
                titulo.Texto = "¡LAS DOS RUTAS COMPLETAS!";
                ayuda.Texto = "FLECHAS Y ENTER, O EL RATÓN";
                break;
        }
        titulo.transform.localScale = new Vector3(escalaTitulo, escalaTitulo, 1f);
        foreach (var t in textos)
        {
            Destroy(t.gameObject);
        }
        textos = opciones.Select(o => Texto(o.ToString(), ColorTexto, Orden + 1, EscalaOpcion)).ToArray();
        Seleccion = 0;
        if (Mouse.current != null)
        {
            ratonAntes = Mouse.current.position.ReadValue();
        }
        raiz.gameObject.SetActive(true);
        Abierto = true;
        Pintar();
    }

    void Cerrar()
    {
        Abierto = false;
        raiz.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!Abierto || resultado != Opcion.Ninguna)
        {
            return;
        }
        // En el cuadro en que se abre no se lee nada: la tecla que lo abrió (Esc) no lo cierra.
        if (Time.frameCount != abiertoEn)
        {
            LeerTeclado(Keyboard.current);
            LeerRaton(Mouse.current);
        }
        Pintar();
    }

    void LeerTeclado(Keyboard k)
    {
        if (k == null)
        {
            return;
        }
        if (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame)
        {
            Mover(-1);
        }
        if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame)
        {
            Mover(1);
        }
        if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)
        {
            Elegir(Elegida);
        }
        if (k.escapeKey.wasPressedThisFrame && TipoActual == Tipo.Pausa)
        {
            Elegir(Opcion.Continuar);
        }
    }

    void LeerRaton(Mouse m)
    {
        if (m == null || camara == null)
        {
            return;
        }
        Vector2 pos = m.position.ReadValue();
        int bajo = Bajo(pos);
        if (bajo >= 0 && Activa(opciones[bajo]) && (pos - ratonAntes).sqrMagnitude > 0.5f)
        {
            Seleccion = bajo;
        }
        ratonAntes = pos;
        if (m.leftButton.wasPressedThisFrame && bajo >= 0)
        {
            Elegir(opciones[bajo]);
        }
    }

    // La opción bajo el puntero, o -1.
    int Bajo(Vector2 pantalla)
    {
        var mundo = camara.ScreenToWorldPoint(new Vector3(pantalla.x, pantalla.y, -camara.transform.position.z));
        Vector3 local = mundo - camara.transform.position;
        for (int i = 0; i < textos.Length; i++)
        {
            var p = textos[i].transform.localPosition;
            float medioAncho = Mathf.Max(textos[i].Ancho, 2f) / 2f + 0.2f, medioAlto = textos[i].AltoMundo / 2f + 0.15f;
            if (Mathf.Abs(local.x - p.x) <= medioAncho && Mathf.Abs(local.y - p.y) <= medioAlto)
            {
                return i;
            }
        }
        return -1;
    }

    // Pasa a la siguiente opción que se puede elegir, hacia arriba (-1) o hacia abajo (1).
    void Mover(int paso)
    {
        for (int i = 1; i <= opciones.Length; i++)
        {
            int j = ((Seleccion + paso * i) % opciones.Length + opciones.Length) % opciones.Length;
            if (Activa(opciones[j]))
            {
                Seleccion = j;
                return;
            }
        }
    }

    void Pintar()
    {
        if (!Activa(Elegida))
        {
            Mover(1);
        }
        for (int i = 0; i < textos.Length; i++)
        {
            string nombre = Nombre(opciones[i]);
            bool activa = Activa(opciones[i]);
            textos[i].Texto = i == Seleccion && activa ? "> " + nombre + " <" : nombre;
            textos[i].Color = !activa ? ColorApagada : i == Seleccion ? ColorElegida : ColorTexto;
        }
        string texto = TextoNota(out bool alerta);
        nota.Texto = texto;
        nota.Color = alerta ? ColorAlerta : ColorBase;
        nota.gameObject.SetActive(!string.IsNullOrEmpty(texto));
    }

    static string Nombre(Opcion o)
    {
        switch (o)
        {
            case Opcion.NuevaPartida: return "NUEVA PARTIDA";
            case Opcion.Continuar: return "CONTINUAR";
            case Opcion.JugarDeNuevo: return "JUGAR DE NUEVO";
            case Opcion.MenuPrincipal: return "MENÚ PRINCIPAL";
            case Opcion.Salir: return "SALIR";
            default: return "";
        }
    }

    // Debajo del título: en el inicio, lo que hay guardado en Firestore; al final, las propinas de la partida.
    string TextoNota(out bool alerta)
    {
        alerta = false;
        var bd = BaseDeDatos.Instancia;
        if (TipoActual == Tipo.Pausa || bd == null)
        {
            return "";
        }
        var p = bd.Partida;
        if (TipoActual == Tipo.Fin)
        {
            return p == null ? "" : Propinas(p.Monedas) + " EN LAS DOS RUTAS";
        }
        if (!bd.Configurada)
        {
            alerta = true;
            return "SIN FIREBASE: LA PARTIDA NO SE GUARDA";
        }
        if (p == null)
        {
            alerta = bd.EstadoActual == BaseDeDatos.Estado.SinConexion;
            return alerta ? "SIN CONEXIÓN CON FIRESTORE: SE REINTENTA" : "LEYENDO LA PARTIDA DE FIRESTORE...";
        }
        if (!HayAvance(p))
        {
            return "SIN PARTIDA GUARDADA";
        }
        bool terminada = p.niveles.All(n => n.completado);
        return (terminada ? "PARTIDA TERMINADA: LAS DOS RUTAS" : "PARTIDA GUARDADA: RUTA " + p.nivelAlcanzado) + " · " + Propinas(p.Monedas);
    }

    static string Propinas(int n) => n + (n == 1 ? " PROPINA" : " PROPINAS");

    // En cada cuadro, por si cambia el tamaño de la vista.
    void LateUpdate()
    {
        if (!Abierto || camara == null)
        {
            return;
        }
        float alto = camara.orthographicSize, ancho = alto * camara.aspect, z = -camara.transform.position.z;
        // Las opciones quedan arriba de la cabeza del recolector, que está al centro de la vista.
        titulo.transform.localPosition = new Vector3(0f, alto * 0.52f, z);
        nota.transform.localPosition = new Vector3(0f, alto * 0.52f - 1.05f, z);
        float y = alto * 0.17f;
        foreach (var t in textos)
        {
            t.transform.localPosition = new Vector3(0f, y, z);
            y -= 0.95f;
        }
        ayuda.transform.localPosition = new Vector3(0f, -alto + 0.75f, z);
        if (pixel != null)
        {
            // Como el velo del marcador: el píxel tiene el pivote en su orilla izquierda, así que se corre la mitad de su
            // tamaño para quedar centrado en la cámara.
            float lado = pixel.rect.width / pixel.pixelsPerUnit;
            var tamano = new Vector3(2f * ancho / lado + 2f, 2f * alto / lado + 2f, 1f);
            Vector2 pivote = pixel.pivot / pixel.rect.size;
            velo.transform.localScale = tamano;
            velo.transform.localPosition = new Vector3((pivote.x - 0.5f) * lado * tamano.x, (pivote.y - 0.5f) * lado * tamano.y, z);
        }
    }

    TextoPixel Texto(string nombre, Color color, int orden, float escala)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(raiz, false);
        go.transform.localScale = new Vector3(escala, escala, 1f);
        go.AddComponent<SpriteRenderer>();
        var t = go.AddComponent<TextoPixel>();
        t.Configurar(color, TextoPixel.Alineacion.Centro, orden);
        return t;
    }
}
