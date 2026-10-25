using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Los dos niveles del juego (dos rutas del camión de la basura) y la partida guardada en Firestore (BaseDeDatos).
//   Al abrir el juego, el recolector espera quieto bajo el velo de "cargando partida" mientras se lee la partida. Con ella
//   se juega el nivel alcanzado: si no es el de la escena, la escena se vuelve a cargar con ese.
//   Al armar un nivel: su calle (semilla y tinte), sus propinas menos las que ya se tomaron y el camión de la meta.
//   Al jugar: cada propina suma en el marcador y se guarda; al alcanzar el camión el nivel queda completo, se guarda, se
//   avisa en grande y, si hay otro nivel, se pasa a él recargando la escena. El nivel en juego sobrevive a esa recarga
//   (es estático); al cerrar el juego se pierde, y al abrirlo lo vuelve a decidir la partida de la base.
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
    [Tooltip("Segundos que se ve el aviso de nivel completo antes de pasar al siguiente.")]
    [SerializeField] float pausaAlCompletar = 2.5f;

    static int nivelEnJuego = 1;
    // Lo que se avisa al terminar de cargar la partida, aunque haya que recargar la escena para ir al nivel alcanzado.
    static string avisoDeCarga;
    const float DuracionAvisoDeCarga = 5f;

    readonly List<Moneda> monedas = new List<Moneda>();
    BaseDeDatos bd;
    Nivel nivel;
    Meta meta;
    bool cambiando;
    // Mientras se ve el aviso de carga, un "guardado" no lo tapa (la partida nueva se escribe en cuanto se crea).
    float finAvisoDeCarga = -1f;

    public Nivel NivelActual => nivel;
    public bool Listo { get; private set; }
    public bool Cambiando => cambiando;
    public IReadOnlyList<Moneda> Monedas => monedas;
    public Meta MetaActual => meta;
    public Marcador Marcador => marcador;
    public float Banqueta => banqueta;
    public static int NivelEnJuego => nivelEnJuego;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        nivelEnJuego = 1;
        avisoDeCarga = null;
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
    }

    IEnumerator Start()
    {
        bd = BaseDeDatos.Asegurar();
        bd.AlCambiar += AlCambiarBase;
        marcador.Nivel(nivel.nombre);
        if (bd.Partida == null)
        {
            marcador.Cargando(bd.Configurada ? "CARGANDO PARTIDA DE FIRESTORE..." : "PREPARANDO LA RUTA...");
            AlCambiarBase(bd.EstadoActual);
            yield return bd.Cargar();
            var p = bd.Partida;
            if (bd.Configurada)
            {
                avisoDeCarga = bd.Nueva ? "PARTIDA NUEVA EN FIRESTORE"
                    : "PARTIDA CARGADA DE FIRESTORE: " + p.Monedas + (p.Monedas == 1 ? " PROPINA, " : " PROPINAS, ") + (Buscar(p.nivelAlcanzado)?.nombre ?? "RUTA " + p.nivelAlcanzado);
            }
        }
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
        if (partida.Nivel(nivel.numero).completado && Buscar(nivel.numero + 1) == null)
        {
            marcador.Aviso("¡LAS DOS RUTAS COMPLETAS!", 4f);
        }
        recolector.Bloqueado = false;
        Listo = true;
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
            marcador.Aviso("¡LAS DOS RUTAS COMPLETAS!", 4f);
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
