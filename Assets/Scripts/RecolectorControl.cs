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
// F: arranca el swing de la botella (MedidorSwing); F otra vez la lanza, recta y horizontal, según dónde esté la aguja:
// verde perfecto, amarillo normal, rojo se le resbala y se rompe a sus pies. Si no se vuelve a tocar F, también se resbala.
// El cuerpo va en dos capas del Animator, como en Metal Slug: las piernas (el SpriteRenderer del recolector) con Reposo y
// Correr, y encima el torso (un hijo) con lo mismo o, al lanzar, con Cargar (mientras carga la bolsa o corre el swing) y
// Lanzar (al soltar). Así corre y lanza a la vez. Todo cambia en el cuadro de la tecla, sin tiempo de salida. El torso de
// lanzar se monta en la cadera de las piernas que se ven, la bolsa o la botella en la mano sigue al guante cuadro a cuadro,
// y el proyectil nace en la mano abierta del cuadro en que suelta.
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
    // Más rápido que esto hacia arriba ya va despegando, aunque el rayo todavía alcance el suelo.
    const float VelocidadDespegue = 0.5f;

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
    [Tooltip("El swing de la botella (F), hijo del recolector.")]
    [SerializeField] MedidorSwing medidorSwing;
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
    // Con la bolsa cargando o el swing de la botella en marcha.
    public bool Preparando => Apuntando || (medidorSwing != null && medidorSwing.Activo);
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
        if (medidorCarga == null || mira == null || (medidorSwing != null && medidorSwing.Activo))
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

    // F: el primer toque arranca el swing, el segundo lanza; si la aguja termina sin el segundo toque, se resbala.
    void LeerBotella(Keyboard teclado)
    {
        if (medidorSwing == null || (medidorCarga != null && medidorCarga.Activo))
        {
            return;
        }

        if (!medidorSwing.Activo)
        {
            if (teclado.fKey.wasPressedThisFrame)
            {
                medidorSwing.Empezar();
                EmpezarCarga(botellaPrefab);
            }
            return;
        }

        bool agotado = false;
        if (teclado.fKey.wasPressedThisFrame || (agotado = medidorSwing.Avanzar(Time.deltaTime)))
        {
            var resultado = agotado ? MedidorSwing.Resultado.Resbalon : medidorSwing.Resolver(out _);
            float multiplicador = 0f;
            if (!agotado)
            {
                multiplicador = resultado == MedidorSwing.Resultado.Perfecto ? medidorSwing.BonoPerfecto : resultado == MedidorSwing.Resultado.Normal ? 1f : 0f;
            }
            else
            {
                medidorSwing.Cancelar();
            }
            TerminarCarga(true);
            UltimaFuerza = multiplicador;
            if (resultado == MedidorSwing.Resultado.Resbalon)
            {
                UltimoResultado = agotado ? "botella_agotada" : "botella_resbalo";
                UltimoProyectil = Lanzar(botellaPrefab, Vector2.zero, 0f, false, true);
            }
            else
            {
                bool perfecto = resultado == MedidorSwing.Resultado.Perfecto;
                UltimoResultado = perfecto ? "botella_perfecta" : "botella_normal";
                UltimoProyectil = Lanzar(botellaPrefab, new Vector2(Sentido(), 0f), multiplicador, perfecto, false);
            }
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

    // Un golpe corta cualquier carga sin lanzar.
    void Cancelar()
    {
        if (medidorCarga != null && medidorCarga.Activo)
        {
            medidorCarga.Cancelar();
        }
        if (medidorSwing != null && medidorSwing.Activo)
        {
            medidorSwing.Cancelar();
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
    // multiplica su rapidez. Si se resbala, nace en la mano y cae a los pies.
    Proyectil Lanzar(GameObject prefab, Vector2 direccion, float fuerza, bool perfecto, bool resbala)
    {
        if (prefab == null || puntoDisparo == null)
        {
            return null;
        }

        // PuntoDisparo está en la mano abierta del torso de lanzar sobre reposo 0; corriendo, el torso va en la cadera.
        Vector3 origen = puntoDisparo.position + (torso != null ? torso.transform.position - transform.position : Vector3.zero);
        UltimoOrigen = origen;
        UltimoDesfase = DesfaseTorso;
        GameObject copia = Instantiate(prefab, origen, Quaternion.identity);
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
