using UnityEngine;
using UnityEngine.InputSystem;

// Estructura del personaje: lee el teclado, elige la animación de estado, se desplaza, salta y lanza.
// Flecha derecha o D: se desplaza a la derecha, mirando a la derecha, con la animación de Correr.
// Flecha izquierda o A: lo mismo hacia la izquierda. Sin dirección (o las dos a la vez): se detiene
// en Reposo, sin cambiar hacia dónde mira.
// Barra espaciadora (o W, o flecha arriba): salta con los pies en el suelo y, ya en el aire, una vez más: el doble salto.
// Al volver a tocar el suelo se recarga. El segundo salto parte de velocidad vertical cero, así que sube lo mismo que el primero.
// V sostenida: carga la bolsa sin dejar de correr. La mira aparece a mano (se sube y se baja con las flechas o W y S, que
// mientras tanto no saltan; la barra espaciadora sí) y la barra de fuerza se llena (MedidorCarga). Al soltar V la bolsa sale
// hacia la mira con esa fuerza: en la franja verde es un tiro perfecto; en el tramo rojo la bolsa se rompe en la mano y
// cae; quedarse en rojo la revienta y quita un corazón.
// F sostenida: ráfaga de botellas, como el disparo del tutorial (de 25:22 a 32:00): una cada cadencia segundos (0.25), rectas,
// horizontales, sin gravedad y a la misma rapidez, así que salen parejas y no se enciman. Cada una deja estela y se rompe en
// vidrios al chocar o al llegar a su alcance (Proyectil). La primera sale en el cuadro de la tecla.
// El cuerpo va en dos capas del Animator, como en Metal Slug: las piernas (el SpriteRenderer del recolector) con Reposo y
// Correr, y encima el torso (un hijo) con lo mismo o, al lanzar, con Cargar (mientras carga la bolsa), Lanzar (al soltarla),
// Rafaga (un lanzamiento corto por botella) y RafagaFin (el brazo que baja al soltar F). Así corre y lanza a la vez. Todo
// cambia en el cuadro de la tecla, sin tiempo de salida. El torso de lanzar se monta en la cadera de las piernas que se ven,
// la bolsa en la mano sigue al guante cuadro a cuadro (en la ráfaga, la botella siguiente aparece en el armado y el impulso), y el
// proyectil nace en la mano abierta del cuadro en que suelta.
// La física es la de Unity 2D: un Rigidbody2D dinámico con gravedad y la rotación congelada, y una
// CapsuleCollider2D del tamaño del cuerpo con la base en los pies, que se apoya en el collider del suelo.
// El desplazamiento es el del tutorial (de 11:53 a 15:23): en cada paso de la física la velocidad en x del
// Rigidbody2D es el eje horizontal por la rapidez; la y se deja a la gravedad y al salto. Sin aceleración:
// arranca y se detiene al instante. Aturdido por un golpe (Salud), no toca la velocidad y deja actuar el empujón.
[RequireComponent(typeof(Animator), typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
public class RecolectorControl : MonoBehaviour
{
    static readonly int Corriendo = Animator.StringToHash("corriendo");
    static readonly int Cargando = Animator.StringToHash("cargando");
    static readonly int LanzarDisparo = Animator.StringToHash("lanzar");
    static readonly int EstadoRafaga = Animator.StringToHash("Rafaga");
    static readonly int EstadoRafagaFin = Animator.StringToHash("RafagaFin");
    static readonly int EstadoLibre = Animator.StringToHash("Libre");
    // Más rápido que esto hacia arriba ya va despegando, aunque el rayo todavía alcance el suelo.
    const float VelocidadDespegue = 0.5f;
    // Hasta cuánto puede llegar tarde el cuadro a la cita de una botella y seguir contando desde la cita, en segundos.
    const double ToleranciaCita = 0.05;

    [Header("Movimiento")]
    [Tooltip("Rapidez al desplazarse a los lados, en unidades por segundo. La velocidad en x es el eje horizontal por este número.")]
    [SerializeField] float velocidad = 4f;

    [Header("Salto")]
    [Tooltip("Impulso hacia arriba al saltar. Con masa 1 es la velocidad de salida en unidades por segundo.")]
    [SerializeField] float fuerzaSalto = 5f;
    [Tooltip("Saltos que se pueden dar en el aire antes de volver a tocar el suelo. Con 1 es el doble salto; con 0, sólo el salto desde el suelo.")]
    [SerializeField] int saltosEnElAire = 1;
    [Tooltip("Capas que cuentan como suelo para el rayo: la del Tilemap Grid > Suelo.")]
    [SerializeField] LayerMask capaSuelo;
    [Tooltip("Cuánto llega el rayo por debajo de los pies, en unidades.")]
    [SerializeField] float margenSuelo = 0.1f;

    [Header("Lanzar")]
    [Tooltip("Prefab de la bolsa de basura: sale hacia la mira, con gravedad y rebotes.")]
    [SerializeField] GameObject bolsaPrefab;
    [Tooltip("Prefab de la botella: sale recta y rápida hacia donde mira el recolector.")]
    [SerializeField] GameObject botellaPrefab;
    [Tooltip("Hijo del recolector en la mano abierta del cuadro en que suelta. Como cuelga de él, se voltea con la escala en x.")]
    [SerializeField] Transform puntoDisparo;
    [Tooltip("La mira de la bolsa, hija del recolector.")]
    [SerializeField] Mira mira;
    [Tooltip("La barra de fuerza de la bolsa (V), hija del recolector.")]
    [SerializeField] MedidorCarga medidorCarga;
    [Tooltip("Segundos entre una botella y la siguiente con F sostenida. El tutorial dispara cada 0.25 s.")]
    [SerializeField] float cadencia = 0.25f;
    [Tooltip("Destello que se ve un instante en la mano al soltar cada botella.")]
    [SerializeField] Sprite destello;
    [Tooltip("Segundos que dura el destello de la mano.")]
    [SerializeField] float vidaDestello = 0.05f;
    [Tooltip("Ángulo con que aparece la mira al empezar a cargar, en grados.")]
    [SerializeField] float anguloInicial = 35f;
    [Tooltip("Grados por segundo que sube o baja la mira con las flechas mientras carga.")]
    [SerializeField] float rapidezMira = 90f;
    [Tooltip("Dónde se dibuja la bolsa o la botella en la mano durante la carga, en unidades desde los pies.")]
    [SerializeField] Vector2 anclaMano = new Vector2(-0.3f, 1.9f);
    [Tooltip("Los cuadros de la carga y, en el mismo orden, dónde está el guante en cada uno (unidades desde los pies). El objeto en la mano lo sigue cuadro a cuadro; en un cuadro que no esté aquí se queda en anclaMano.")]
    [SerializeField] Sprite[] cuadrosMano;
    [SerializeField] Vector2[] anclasMano;

    [Header("Torso y piernas")]
    [Tooltip("Hijo del recolector con el torso (cabeza, tronco y brazos); el SpriteRenderer del recolector dibuja las piernas.")]
    [SerializeField] SpriteRenderer torso;
    [Tooltip("Los cuadros de piernas y, en el mismo orden, cuánto se movió su cadera respecto a la de reposo 0, en unidades.")]
    [SerializeField] Sprite[] cuadrosPiernas;
    [SerializeField] Vector2[] caderas;
    [Tooltip("Los cuadros del torso de lanzar (hechos sobre la cadera de reposo 0): con ellos el torso se mueve a la cadera de las piernas que se ven. Los de reposo y correr ya traen la suya.")]
    [SerializeField] Sprite[] cuadrosTorsoLanzar;

    Animator animator;
    Rigidbody2D cuerpo;
    CapsuleCollider2D capsula;
    Salud salud;
    // Las piernas.
    SpriteRenderer dibujo;
    SpriteRenderer objetoEnMano;
    // Saltos dados en el aire desde la última vez que tocó el suelo.
    int saltosAereos;
    // La capa Brazos del Animator: pesa 1 mientras carga o lanza y 0 el resto del tiempo, para que el torso sea el de la
    // capa base. En su estado Libre la capa no anima nada, pero con peso 1 dejaría el torso en el último cuadro de lanzar.
    int capaBrazos = -1;
    // La ráfaga: si está en marcha (F sostenida), si todavía no sale su primera botella y la cita de la siguiente, en
    // segundos del reloj del juego.
    bool enRafaga, primeraDeRafaga;
    double proximoTiro = double.NegativeInfinity;

    // -1 izquierda, 0 sin dirección, 1 derecha; como Input.GetAxisRaw("Horizontal").
    public float Horizontal { get; private set; }
    // Ángulo de la mira en el último lanzamiento de bolsa (grados sobre la horizontal, hacia donde mira).
    public float UltimoAngulo { get; private set; }
    // Si en este cuadro tiene los pies en el suelo: el rayo lo toca y no va despegando.
    public bool EnSuelo { get; private set; }
    // Saltos dados desde que empezó la escena.
    public int Saltos { get; private set; }
    // Con la bolsa cargando: W y la flecha arriba apuntan la mira en lugar de saltar.
    public bool Apuntando => medidorCarga != null && medidorCarga.Activo;
    // Con la bolsa cargando o la ráfaga de botellas en marcha.
    public bool Preparando => Apuntando || enRafaga;
    public bool EnRafaga => enRafaga;
    public float Cadencia => cadencia;
    // Botellas lanzadas desde que empezó la escena, y la cita (segundos del reloj) de la última.
    public int BotellasLanzadas { get; private set; }
    public double UltimaCita { get; private set; }
    // Cuánto se movió el torso para quedar sobre la cadera de las piernas que se ven, en unidades (cero sin lanzar).
    public Vector2 DesfaseTorso { get; private set; }
    // Lo que pasó en el último lanzamiento, para quien quiera leerlo (la prueba, por ejemplo).
    public string UltimoResultado { get; private set; } = "";
    public float UltimaFuerza { get; private set; }
    public Proyectil UltimoProyectil { get; private set; }
    public int Reventadas { get; private set; }
    // Dónde nació el último proyectil, en coordenadas del mundo, y el desfase del torso en ese momento.
    public Vector2 UltimoOrigen { get; private set; }
    public Vector2 UltimoDesfase { get; private set; }

    void Start()
    {
        animator = GetComponent<Animator>();
        cuerpo = GetComponent<Rigidbody2D>();
        capsula = GetComponent<CapsuleCollider2D>();
        salud = GetComponent<Salud>();
        dibujo = GetComponent<SpriteRenderer>();
        capaBrazos = animator.GetLayerIndex("Brazos");
        PesoBrazos(0f);
        CrearObjetoEnMano();
    }

    // Como en el tutorial, la física se toca en FixedUpdate y no en Update. Sólo cambia la x de la velocidad: la y la
    // siguen manejando la gravedad y el salto, así que en el aire el control es el mismo.
    void FixedUpdate()
    {
        if (salud != null && salud.Aturdido)
        {
            return;
        }
        cuerpo.linearVelocity = new Vector2(Horizontal * velocidad, cuerpo.linearVelocity.y);
    }

    void Update()
    {
        var teclado = Keyboard.current;
        bool aturdido = salud != null && salud.Aturdido;
        if (aturdido && Preparando)
        {
            Cancelar();
        }

        Horizontal = aturdido ? 0f : LeerHorizontal(teclado);
        animator.SetBool(Corriendo, Horizontal != 0f);

        // La escala negativa en x voltea el sprite y, con él, al punto de lanzamiento y a la mira.
        if (Horizontal < 0f)
        {
            transform.localScale = new Vector3(-1f, 1f, 1f);
        }
        else if (Horizontal > 0f)
        {
            transform.localScale = new Vector3(1f, 1f, 1f);
        }

        EnSuelo = TocaSuelo();
        if (EnSuelo)
        {
            saltosAereos = 0;
        }

        if (teclado == null || aturdido)
        {
            return;
        }

        // El salto: la barra espaciadora (o W, o la flecha arriba, si no está apuntando la bolsa), en el cuadro en que se
        // presiona. Con los pies en el suelo, el salto normal; en el aire, sólo si le queda un salto extra (el doble salto).
        if (teclado.spaceKey.wasPressedThisFrame || (!Apuntando && (teclado.wKey.wasPressedThisFrame || teclado.upArrowKey.wasPressedThisFrame)))
        {
            if (EnSuelo)
            {
                Saltar();
            }
            else if (saltosAereos < saltosEnElAire)
            {
                saltosAereos++;
                Saltar();
            }
        }

        LeerBolsa(teclado);
        LeerBotella(teclado);
    }

    // El Animator ya puso los sprites de este cuadro. Con un torso de lanzar, el torso va a la cadera de las piernas que se
    // ven; con el de reposo o correr se queda en su lugar. El objeto en la mano (hijo del torso) va al guante del torso.
    void LateUpdate()
    {
        // Terminó el lanzamiento (o se cortó la carga): la capa Brazos suelta el torso desde el cuadro siguiente.
        if (capaBrazos >= 0 && animator.GetLayerWeight(capaBrazos) > 0f && !animator.IsInTransition(capaBrazos)
            && animator.GetCurrentAnimatorStateInfo(capaBrazos).IsName("Libre"))
        {
            PesoBrazos(0f);
        }
        if (torso != null)
        {
            DesfaseTorso = EsTorsoDeLanzar(torso.sprite) ? CaderaDe(dibujo.sprite) : Vector2.zero;
            torso.transform.localPosition = DesfaseTorso;
        }
        // En la ráfaga, la botella siguiente se ve en la mano en los cuadros que tienen guante (el armado y el impulso);
        // en el de suelta ya va volando y en el seguimiento la mano está vacía.
        if (enRafaga && objetoEnMano != null)
        {
            objetoEnMano.enabled = torso != null && TieneAncla(torso.sprite);
        }
        if (objetoEnMano != null && objetoEnMano.enabled)
        {
            objetoEnMano.transform.localPosition = AnclaDe(torso != null ? torso.sprite : dibujo.sprite);
        }
    }

    bool EsTorsoDeLanzar(Sprite cuadro)
    {
        if (cuadrosTorsoLanzar != null)
        {
            foreach (var s in cuadrosTorsoLanzar)
            {
                if (s == cuadro)
                {
                    return true;
                }
            }
        }
        return false;
    }

    Vector2 CaderaDe(Sprite piernas)
    {
        if (cuadrosPiernas != null && caderas != null)
        {
            for (int i = 0; i < cuadrosPiernas.Length && i < caderas.Length; i++)
            {
                if (cuadrosPiernas[i] == piernas)
                {
                    return caderas[i];
                }
            }
        }
        return Vector2.zero;
    }

    bool TieneAncla(Sprite cuadro)
    {
        if (cuadrosMano != null && anclasMano != null)
        {
            for (int i = 0; i < cuadrosMano.Length && i < anclasMano.Length; i++)
            {
                if (cuadrosMano[i] == cuadro)
                {
                    return true;
                }
            }
        }
        return false;
    }

    Vector2 AnclaDe(Sprite cuadro)
    {
        if (cuadrosMano != null && anclasMano != null)
        {
            for (int i = 0; i < cuadrosMano.Length && i < anclasMano.Length; i++)
            {
                if (cuadrosMano[i] == cuadro)
                {
                    return anclasMano[i];
                }
            }
        }
        return anclaMano;
    }

    // V: cargar mientras se sostiene, apuntar con las flechas, lanzar al soltar.
    void LeerBolsa(Keyboard teclado)
    {
        if (medidorCarga == null || mira == null || enRafaga)
        {
            return;
        }

        if (!medidorCarga.Activo)
        {
            if (teclado.vKey.wasPressedThisFrame)
            {
                medidorCarga.Empezar();
                mira.MostrarManual(anguloInicial);
                EmpezarCarga(bolsaPrefab);
            }
            return;
        }

        float arriba = (teclado.upArrowKey.isPressed || teclado.wKey.isPressed) ? 1f : 0f;
        float abajo = (teclado.downArrowKey.isPressed || teclado.sKey.isPressed) ? 1f : 0f;
        mira.Ajustar((arriba - abajo) * rapidezMira * Time.deltaTime);

        if (teclado.vKey.wasReleasedThisFrame)
        {
            var resultado = medidorCarga.Soltar(out float fuerza);
            UltimoAngulo = mira.Angulo;
            Vector2 direccion = mira.Direccion;
            mira.Ocultar();
            TerminarCarga(true);
            UltimaFuerza = fuerza;
            if (resultado == MedidorCarga.Resultado.Inutil)
            {
                UltimoResultado = "bolsa_inutil";
                UltimoProyectil = Lanzar(bolsaPrefab, Vector2.zero, 0f, false, true);
            }
            else
            {
                bool perfecto = resultado == MedidorCarga.Resultado.Perfecto;
                UltimoResultado = perfecto ? "bolsa_perfecta" : "bolsa_normal";
                UltimoProyectil = Lanzar(bolsaPrefab, direccion, fuerza, perfecto, false);
            }
        }
        else if (medidorCarga.Avanzar(Time.deltaTime))
        {
            // Reventó en la mano: pedazos, un corazón menos y un empujón hacia atrás.
            mira.Ocultar();
            TerminarCarga(false);
            Reventadas++;
            UltimoResultado = "bolsa_revento";
            UltimaFuerza = 0f;
            UltimoProyectil = null;
            var proyectil = bolsaPrefab != null ? bolsaPrefab.GetComponent<Proyectil>() : null;
            if (proyectil != null && objetoEnMano != null)
            {
                Pedazo.Soltar(PixelDe(proyectil), objetoEnMano.transform.position, new Color32(60, 66, 72, 255), 10, 4f, -Sentido(), objetoEnMano.sortingOrder + 1);
            }
            if (salud != null)
            {
                salud.Herir(-Sentido());
            }
        }
    }

    // F sostenida: la ráfaga. Como en el tutorial (31:33), sale una botella cada cadencia segundos mientras se sostiene la
    // tecla, pero la cita de la siguiente es la de la anterior más la cadencia, no el cuadro en que salió: el tutorial
    // guarda Time.time y el retraso de cada cuadro se suma al intervalo, que así va y viene. Aquí sale una cada 15 cuadros
    // a 60 por segundo, sin atrasarse ni adelantarse, y tocar F más rápido no adelanta la siguiente. La primera sale en el
    // cuadro de la tecla, si ya pasó la cadencia desde la última. Al soltar F, el brazo baja (RafagaFin).
    void LeerBotella(Keyboard teclado)
    {
        if (botellaPrefab == null || Apuntando)
        {
            return;
        }

        if (!enRafaga)
        {
            if (!teclado.fKey.wasPressedThisFrame)
            {
                return;
            }
            enRafaga = true;
            primeraDeRafaga = true;
            if (objetoEnMano != null)
            {
                var dibujoPrefab = botellaPrefab.GetComponent<SpriteRenderer>();
                objetoEnMano.sprite = dibujoPrefab != null ? dibujoPrefab.sprite : null;
                objetoEnMano.enabled = false;
            }
        }

        if (!teclado.fKey.isPressed)
        {
            TerminarRafaga(true);
            return;
        }

        double ahora = Time.timeAsDouble;
        if (ahora < proximoTiro - 1e-4)
        {
            return;
        }
        // La primera de la ráfaga sale ahora. Las demás salen con la hora de su cita, aunque el cuadro llegue un poco
        // después; si llega muy tarde (un tirón del juego), la cuenta vuelve a empezar desde ahora.
        double cita = !primeraDeRafaga && ahora - proximoTiro <= ToleranciaCita ? proximoTiro : ahora;
        primeraDeRafaga = false;
        proximoTiro = cita + cadencia;
        Tirar(cita);
    }

    // Una botella de la ráfaga: el lanzamiento corto del torso desde el cuadro de suelta, la botella recta hacia donde mira
    // y el destello en la mano.
    void Tirar(double cita)
    {
        PesoBrazos(1f);
        if (capaBrazos >= 0)
        {
            animator.Play(EstadoRafaga, capaBrazos, 0f);
        }
        if (objetoEnMano != null)
        {
            objetoEnMano.enabled = false;
        }
        UltimoResultado = "botella";
        UltimaFuerza = 1f;
        UltimaCita = cita;
        UltimoProyectil = Lanzar(botellaPrefab, new Vector2(Sentido(), 0f), 1f, false, false, cita);
        if (UltimoProyectil == null)
        {
            return;
        }
        BotellasLanzadas++;
        if (destello != null)
        {
            var dibujoBotella = UltimoProyectil.GetComponent<SpriteRenderer>();
            Desvanecer.Crear(destello, UltimoOrigen, Quaternion.identity, 1f, Color.white, vidaDestello, 0f,
                dibujoBotella.sortingLayerID, dibujoBotella.sortingOrder + 1, transform, dibujoBotella.sharedMaterial);
        }
    }

    // Termina la ráfaga: con el brazo que baja (al soltar F) o de golpe, de vuelta al torso de la capa base.
    void TerminarRafaga(bool bajarBrazo)
    {
        enRafaga = false;
        if (objetoEnMano != null)
        {
            objetoEnMano.enabled = false;
        }
        if (capaBrazos < 0)
        {
            return;
        }
        var estado = animator.GetCurrentAnimatorStateInfo(capaBrazos);
        if (bajarBrazo && estado.shortNameHash == EstadoRafaga)
        {
            animator.Play(EstadoRafagaFin, capaBrazos, 0f);
        }
        else if (!bajarBrazo && (estado.shortNameHash == EstadoRafaga || estado.shortNameHash == EstadoRafagaFin))
        {
            animator.Play(EstadoLibre, capaBrazos, 0f);
            PesoBrazos(0f);
        }
    }

    // La pose de carga y el objeto en la mano, en el mismo cuadro de la tecla.
    void EmpezarCarga(GameObject prefab)
    {
        animator.ResetTrigger(LanzarDisparo);
        animator.SetBool(Cargando, true);
        PesoBrazos(1f);
        if (objetoEnMano != null && prefab != null)
        {
            var dibujoPrefab = prefab.GetComponent<SpriteRenderer>();
            objetoEnMano.sprite = dibujoPrefab != null ? dibujoPrefab.sprite : null;
            objetoEnMano.enabled = objetoEnMano.sprite != null;
        }
    }

    // Termina la pose de carga: con lanzamiento (dispara la animación Lanzar) o sin él (reventó o se canceló).
    void TerminarCarga(bool lanza)
    {
        animator.SetBool(Cargando, false);
        if (lanza)
        {
            animator.SetTrigger(LanzarDisparo);
        }
        else
        {
            PesoBrazos(0f);
        }
        if (objetoEnMano != null)
        {
            objetoEnMano.enabled = false;
        }
    }

    void PesoBrazos(float peso)
    {
        if (capaBrazos >= 0)
        {
            animator.SetLayerWeight(capaBrazos, peso);
        }
    }

    // Un golpe corta cualquier carga sin lanzar, y la ráfaga.
    void Cancelar()
    {
        if (enRafaga)
        {
            TerminarRafaga(false);
            return;
        }
        if (medidorCarga != null && medidorCarga.Activo)
        {
            medidorCarga.Cancelar();
        }
        if (mira != null)
        {
            mira.Ocultar();
        }
        TerminarCarga(false);
    }

    void CrearObjetoEnMano()
    {
        var go = new GameObject("ObjetoEnMano");
        go.transform.SetParent(torso != null ? torso.transform : transform, false);
        go.transform.localPosition = new Vector3(anclaMano.x, anclaMano.y, 0f);
        objetoEnMano = go.AddComponent<SpriteRenderer>();
        var encima = torso != null ? torso : GetComponent<SpriteRenderer>();
        objetoEnMano.sortingLayerID = encima.sortingLayerID;
        objetoEnMano.sortingOrder = encima.sortingOrder + 1;
        objetoEnMano.enabled = false;
    }

    // Como en el tutorial: un rayo hacia abajo; si toca el suelo, se puede saltar. Sale del centro de la
    // cápsula y llega margenSuelo por debajo de los pies. Sólo ve la capa del suelo, así que no se detecta
    // a sí mismo ni a la bolsa o la botella (el tutorial lo resuelve apagando Queries Start In Colliders).
    // Después del impulso, mientras la física no da su siguiente paso, el rayo todavía alcanza el suelo: la
    // velocidad hacia arriba dice que ya va en el aire. Debug.DrawRay lo dibuja en la vista de escena.
    bool TocaSuelo()
    {
        Bounds caja = capsula.bounds;
        Vector2 origen = caja.center;
        float distancia = caja.extents.y + margenSuelo;
        bool toca = Physics2D.Raycast(origen, Vector2.down, distancia, capaSuelo).collider != null
            && cuerpo.linearVelocity.y <= VelocidadDespegue;
        Debug.DrawRay(origen, Vector2.down * distancia, toca ? Color.green : Color.red);
        return toca;
    }

    // AddForce hacia arriba, como en el tutorial, aplicada como impulso: la velocidad de salida es
    // fuerzaSalto entre la masa y no depende del paso de la física. Se parte de velocidad vertical cero
    // para que todos los saltos suban lo mismo, aunque la tecla llegue en el último instante de una caída.
    void Saltar()
    {
        cuerpo.linearVelocity = new Vector2(cuerpo.linearVelocity.x, 0f);
        cuerpo.AddForce(Vector2.up * fuerzaSalto, ForceMode2D.Impulse);
        Saltos++;
    }

    int Sentido() => transform.localScale.x >= 0f ? 1 : -1;

    // Como en el tutorial, la dirección depende del signo de la escala en x y el proyectil nace en el
    // punto de lanzamiento, no en el centro del personaje. Cada prefab trae su propia física (Proyectil); la fuerza
    // multiplica su rapidez. Si se resbala, nace en la mano y cae a los pies. Una botella de la ráfaga trae su cita.
    Proyectil Lanzar(GameObject prefab, Vector2 direccion, float fuerza, bool perfecto, bool resbala, double cita = double.NaN)
    {
        if (prefab == null || puntoDisparo == null)
        {
            return null;
        }

        // PuntoDisparo está en la mano abierta del torso de lanzar sobre reposo 0; corriendo, el torso va en la cadera.
        Vector3 origen = puntoDisparo.position + (torso != null ? torso.transform.position - transform.position : Vector3.zero);
        UltimoOrigen = origen;
        UltimoDesfase = DesfaseTorso;
        Vector3 nacimiento = origen;
        var modelo = prefab.GetComponent<Proyectil>();
        if (!double.IsNaN(cita) && modelo != null)
        {
            nacimiento += (Vector3)(direccion.normalized * modelo.Rapidez * fuerza * (float)Proyectil.TiempoDeVuelo(cita));
        }
        GameObject copia = Instantiate(prefab, nacimiento, Quaternion.identity);
        var colliderCopia = copia.GetComponent<Collider2D>();

        // No choca con el recolector (su cápsula) ni con los otros proyectiles.
        foreach (var propio in GetComponentsInChildren<Collider2D>())
        {
            Physics2D.IgnoreCollision(colliderCopia, propio);
        }
        foreach (var otro in FindObjectsByType<Proyectil>())
        {
            if (otro.gameObject != copia)
            {
                Physics2D.IgnoreCollision(colliderCopia, otro.GetComponent<Collider2D>());
            }
        }

        var proyectil = copia.GetComponent<Proyectil>();
        if (resbala)
        {
            proyectil.Resbalar(Sentido());
        }
        else if (!double.IsNaN(cita))
        {
            proyectil.Disparar(direccion, Sentido(), origen, cita);
        }
        else
        {
            proyectil.Lanzar(direccion, Sentido(), fuerza, perfecto);
        }
        return proyectil;
    }

    static Sprite PixelDe(Proyectil proyectil) => proyectil.PedazoSprite;

    static float LeerHorizontal(Keyboard teclado)
    {
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
