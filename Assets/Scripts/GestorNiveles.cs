using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Los dos niveles del juego (dos rutas del camión de la basura), la partida guardada en Firestore (BaseDeDatos) y los
// menús (MenuJuego).
//   Al abrir el juego se ve el menú de inicio sobre la calle, con el recolector quieto, mientras se lee la partida.
//   NUEVA PARTIDA la deja en cero y empieza en la ruta 1; CONTINUAR sigue en el nivel alcanzado: si no es el de la escena,
//   la escena se vuelve a cargar con ese. SALIR cierra el programa.
//   Al armar un nivel: su calle (semilla y tinte), sus propinas menos las que ya se tomaron y el camión de la meta.
//   Al jugar: cada propina suma en el marcador y se guarda; al alcanzar el camión el nivel queda completo, se guarda, se
//   avisa en grande y, si hay otro nivel, se pasa a él recargando la escena. Al completar el último, el menú del final:
//   JUGAR DE NUEVO, MENÚ PRINCIPAL o SALIR. Esc detiene el juego y abre la pausa. El nivel en juego y si la escena empieza
//   con el menú sobreviven a las recargas (son estáticos); al cerrar el juego se pierden, y al abrirlo vuelve el menú.
[DefaultExecutionOrder(-200)]
public class GestorNiveles : MonoBehaviour
{
    [Tooltip("Los niveles en orden: Ruta1 y Ruta2.")]
    [SerializeField] Nivel[] niveles = new Nivel[0];
    [SerializeField] CalleModular calle;
    [SerializeField] RecolectorControl recolector;
    [SerializeField] Marcador marcador;
    [SerializeField] Moneda prefabMoneda;
    [SerializeField] Meta prefabMeta;
    [Tooltip("Altura de los pies del recolector parado en la banqueta: la base de las propinas.")]
    [SerializeField] float banqueta = 0f;
    [Tooltip("Altura de las llantas del camión de la meta, en el asfalto, delante de la banqueta.")]
    [SerializeField] float alturaMeta = -1.55f;
    [Tooltip("Segundos que se ve el aviso de nivel completo antes de pasar al siguiente (y, en el último, antes del menú del final).")]
    [SerializeField] float pausaAlCompletar = 2.5f;

    static int nivelEnJuego = 1;
    // Lo que se avisa al terminar de cargar la partida, aunque haya que recargar la escena para ir al nivel alcanzado.
    static string avisoDeCarga;
    // Si la escena empieza con el menú de inicio: al abrir el juego y al volver a él desde la pausa o el final.
    static bool conMenu = true;
    const float DuracionAvisoDeCarga = 5f;

    readonly List<Moneda> monedas = new List<Moneda>();
    BaseDeDatos bd;
    MenuJuego menu;
    Nivel nivel;
    Meta meta;
    bool cambiando;
    // El cuadro en que se cerró un menú: la misma Esc que cerró la pausa no la vuelve a abrir.
    int cuadroMenuCerrado = -1;
    // Mientras se ve el aviso de carga, un "guardado" no lo tapa (la partida nueva se escribe en cuanto se crea).
    float finAvisoDeCarga = -1f;

    public Nivel NivelActual => nivel;
    public bool Listo { get; private set; }
    public bool Cambiando => cambiando;
    public IReadOnlyList<Moneda> Monedas => monedas;
    public Meta MetaActual => meta;
    public Marcador Marcador => marcador;
    public float Banqueta => banqueta;
    public MenuJuego Menu => menu;
    public static int NivelEnJuego => nivelEnJuego;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        nivelEnJuego = 1;
        avisoDeCarga = null;
        conMenu = true;
    }

    void Awake()
    {
        nivel = Buscar(nivelEnJuego) ?? (niveles.Length > 0 ? niveles[0] : null);
        if (nivel != null && calle != null)
        {
            calle.Configurar(nivel.semilla, nivel.tinteCielo, nivel.tinteCalle);
        }
        if (recolector != null)
        {
            recolector.Bloqueado = true;
        }
        menu = gameObject.AddComponent<MenuJuego>();
    }

    IEnumerator Start()
    {
        bd = BaseDeDatos.Asegurar();
        bd.AlCambiar += AlCambiarBase;
        marcador.Nivel(nivel.nombre);
        menu.Preparar(marcador);
        AlCambiarBase(bd.EstadoActual);
        // La partida se lee de Firestore mientras se ve el menú de inicio (o, sin menú, bajo el velo de carga).
        bd.Leer();
        if (conMenu)
        {
            var opcion = MenuJuego.Opcion.Ninguna;
            yield return menu.Mostrar(MenuJuego.Tipo.Inicio, o => opcion = o);
            cuadroMenuCerrado = Time.frameCount;
            if (opcion == MenuJuego.Opcion.Salir)
            {
                Salir();
                yield break;
            }
            conMenu = false;
            yield return Esperar();
            var p = bd.Partida;
            if (opcion == MenuJuego.Opcion.NuevaPartida)
            {
                bd.PartidaNueva();
                avisoDeCarga = bd.Configurada ? "PARTIDA NUEVA EN FIRESTORE" : null;
            }
            else if (bd.Configurada)
            {
                avisoDeCarga = "PARTIDA CARGADA DE FIRESTORE: " + p.Monedas + (p.Monedas == 1 ? " PROPINA, " : " PROPINAS, ") + (Buscar(p.nivelAlcanzado)?.nombre ?? "RUTA " + p.nivelAlcanzado);
            }
        }
        yield return Esperar();
        var partida = bd.Partida;
        if (partida.nivelAlcanzado != nivelEnJuego && Buscar(partida.nivelAlcanzado) != null)
        {
            nivelEnJuego = partida.nivelAlcanzado;
            SceneManager.LoadScene(gameObject.scene.buildIndex);
            yield break;
        }

        Armar(partida);
        marcador.Cargando(null);
        marcador.Monedas(partida.Monedas);
        if (avisoDeCarga != null)
        {
            marcador.Estado(avisoDeCarga, marcador.ColorBase, DuracionAvisoDeCarga);
            finAvisoDeCarga = Time.time + DuracionAvisoDeCarga;
            avisoDeCarga = null;
        }
        else
        {
            AlCambiarBase(bd.EstadoActual);
        }
        recolector.Bloqueado = false;
        Listo = true;
        // Una partida que ya tenía las dos rutas completas: directo al menú del final.
        if (partida.Nivel(nivel.numero).completado && Buscar(nivel.numero + 1) == null)
        {
            StartCoroutine(Terminar());
        }
    }

    // Hasta que la partida esté leída, bajo el velo de carga.
    IEnumerator Esperar()
    {
        if (bd.Partida != null)
        {
            yield break;
        }
        marcador.Cargando(bd.Configurada ? "CARGANDO PARTIDA DE FIRESTORE..." : "PREPARANDO LA RUTA...");
        while (bd.Partida == null)
        {
            yield return null;
        }
    }

    // Esc mientras se juega: la pausa.
    void Update()
    {
        // Con el menú de inicio abierto, el marcador de atrás muestra las propinas de la partida guardada, como la nota.
        if (menu.Abierto && menu.TipoActual == MenuJuego.Tipo.Inicio && bd != null && bd.Partida != null)
        {
            marcador.Monedas(bd.Partida.Monedas);
        }
        if (!Listo || cambiando || menu.Abierto || Time.frameCount == cuadroMenuCerrado)
        {
            return;
        }
        var teclado = Keyboard.current;
        if (teclado != null && teclado.escapeKey.wasPressedThisFrame)
        {
            StartCoroutine(Pausa());
        }
    }

    IEnumerator Pausa()
    {
        recolector.Bloqueado = true;
        Time.timeScale = 0f;
        var opcion = MenuJuego.Opcion.Ninguna;
        yield return menu.Mostrar(MenuJuego.Tipo.Pausa, o => opcion = o);
        Time.timeScale = 1f;
        cuadroMenuCerrado = Time.frameCount;
        if (opcion == MenuJuego.Opcion.MenuPrincipal)
        {
            IrAlMenu();
        }
        else if (opcion == MenuJuego.Opcion.Salir)
        {
            Salir();
        }
        else
        {
            // Un cuadro después: la tecla que cerró el menú (Enter o la barra espaciadora) no hace saltar al recolector.
            yield return null;
            recolector.Bloqueado = false;
        }
    }

    // Las dos rutas completas: el aviso en grande y, después, el menú del final.
    IEnumerator Terminar()
    {
        cambiando = true;
        recolector.Bloqueado = true;
        marcador.Aviso("¡LAS DOS RUTAS COMPLETAS!", pausaAlCompletar);
        yield return new WaitForSeconds(pausaAlCompletar);
        marcador.Aviso(null, 0f);
        var opcion = MenuJuego.Opcion.Ninguna;
        yield return menu.Mostrar(MenuJuego.Tipo.Fin, o => opcion = o);
        cuadroMenuCerrado = Time.frameCount;
        if (opcion == MenuJuego.Opcion.JugarDeNuevo)
        {
            bd.PartidaNueva();
            avisoDeCarga = bd.Configurada ? "PARTIDA NUEVA EN FIRESTORE" : null;
            nivelEnJuego = 1;
            conMenu = false;
            SceneManager.LoadScene(gameObject.scene.buildIndex);
        }
        else if (opcion == MenuJuego.Opcion.MenuPrincipal)
        {
            IrAlMenu();
        }
        else
        {
            Salir();
        }
    }

    // De vuelta al menú de inicio, sobre la calle del nivel en juego.
    void IrAlMenu()
    {
        conMenu = true;
        SceneManager.LoadScene(gameObject.scene.buildIndex);
    }

    // Cierra el programa, como el script de pausa del video de apoyo. Si hay algo sin guardar, BaseDeDatos espera a que se
    // escriba (hasta 3 s). En el editor sólo se sale de Play.
    void Salir()
    {
        Debug.Log("SALIR: Application.Quit");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    void OnDestroy()
    {
        if (bd != null)
        {
            bd.AlCambiar -= AlCambiarBase;
        }
    }

    void Armar(Partida partida)
    {
        var progreso = partida.Nivel(nivel.numero);
        for (int i = 0; i < nivel.monedas.Length; i++)
        {
            if (progreso.tomadas.Contains(i))
            {
                continue;
            }
            var p = nivel.monedas[i];
            var m = Instantiate(prefabMoneda, new Vector3(p.x, banqueta + p.y, 0f), Quaternion.identity, transform);
            m.name = "Propina " + i;
            m.Preparar(i, AlTomar);
            monedas.Add(m);
        }
        meta = Instantiate(prefabMeta, new Vector3(nivel.xMeta, alturaMeta, 0f), Quaternion.identity, transform);
        meta.name = "Meta " + nivel.nombre;
        meta.Preparar(AlLlegar, progreso.completado);
    }

    void AlTomar(Moneda m)
    {
        monedas.Remove(m);
        if (!bd.Partida.Tomar(nivel.numero, m.Indice))
        {
            return;
        }
        marcador.Monedas(bd.Partida.Monedas);
        bd.Guardar();
    }

    void AlLlegar()
    {
        if (cambiando)
        {
            return;
        }
        bd.Partida.Completar(nivel.numero);
        bd.Guardar();
        var siguiente = Buscar(nivel.numero + 1);
        if (siguiente == null)
        {
            StartCoroutine(Terminar());
            return;
        }
        StartCoroutine(Pasar(siguiente));
    }

    IEnumerator Pasar(Nivel siguiente)
    {
        cambiando = true;
        recolector.Bloqueado = true;
        marcador.Aviso("¡" + nivel.nombre + " COMPLETA!", pausaAlCompletar);
        yield return new WaitForSeconds(pausaAlCompletar);
        nivelEnJuego = siguiente.numero;
        SceneManager.LoadScene(gameObject.scene.buildIndex);
    }

    void AlCambiarBase(BaseDeDatos.Estado estado)
    {
        if (marcador == null)
        {
            return;
        }
        switch (estado)
        {
            case BaseDeDatos.Estado.SinConfiguracion:
                marcador.Estado("SIN FIREBASE: LA PARTIDA NO SE GUARDA", marcador.ColorAlerta, 0f);
                break;
            case BaseDeDatos.Estado.Conectando:
                marcador.Estado("CONECTANDO CON FIRESTORE...", marcador.ColorBase, 0f);
                break;
            case BaseDeDatos.Estado.Guardada:
                if (Time.time >= finAvisoDeCarga)
                {
                    marcador.Estado("GUARDADO EN FIRESTORE", marcador.ColorBase, 2f);
                }
                break;
            case BaseDeDatos.Estado.SinConexion:
                marcador.Estado("SIN CONEXIÓN CON FIRESTORE: SE REINTENTA", marcador.ColorAlerta, 0f);
                break;
        }
    }

    Nivel Buscar(int numero)
    {
        foreach (var n in niveles)
        {
            if (n != null && n.numero == numero)
            {
                return n;
            }
        }
        return null;
    }
}
