using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// La conexión con la base de datos: Cloud Firestore, la base de Firebase del video de apoyo (The power ups, 2021), por su
// API REST con UnityWebRequest. No usa el paquete de Firebase para Unity porque en Mac y en Windows Google lo da como beta
// y sólo para probar en el editor; así funciona igual en el editor y en el ejecutable.
//   Quién juega. Como en el video, una sesión anónima de Firebase Authentication. La primera vez se crea el usuario
//   anónimo (accounts:signUp) y se guarda en este equipo su token de renovación (PlayerPrefs); las siguientes, la sesión
//   se renueva con él (securetoken) y es el mismo usuario. Su uid es el nombre del documento de su partida.
//   Qué se guarda. El documento partidas/{uid}: monedas (el total), nivelAlcanzado, y nivel1 y nivel2, cada uno con
//   completado, monedas y tomadas (los números de las propinas que ya se tomaron), más la hora de la última escritura.
//   Las reglas de Firestore (firestore.rules) sólo dejan a cada usuario leer y escribir su propio documento.
//   Cuándo. Al abrir el juego se lee (GET), mientras se ve el menú de inicio. Cada propina y cada nivel completo se
//   escriben (PATCH, el documento entero), juntando en una escritura lo que pase en un cuarto de segundo. Si se cierra el
//   juego con algo sin guardar, se espera a que termine (hasta 3 s). Sin conexión se reintenta, cada vez más espaciado; y
//   nunca se escribe antes de haber leído, para no pisar con una partida vacía la que está en la nube. NUEVA PARTIDA y
//   JUGAR DE NUEVO reemplazan el documento por una partida en cero (el mismo jugador, el mismo documento), también sólo
//   después de haberlo leído.
//   Configuración. Assets/StreamingAssets/firebase.json con apiKey y projectId del proyecto de Firebase. No se sube al
//   repositorio (.gitignore); el modelo es firebase.ejemplo.json. Sin ese archivo el juego corre igual, sin guardar, y el
//   marcador lo dice.
public class BaseDeDatos : MonoBehaviour
{
    public enum Estado { SinConfiguracion, Conectando, Cargada, Guardando, Guardada, SinConexion }

    const string ClaveToken = "ih743.firebase.refreshToken";
    const string ClaveProyecto = "ih743.firebase.proyecto";
    const string Coleccion = "partidas";
    // Segundos que se juntan los cambios antes de escribir.
    const float Agrupar = 0.25f;
    // Segundos que se espera una escritura pendiente al cerrar el juego.
    const float EsperaAlSalir = 3f;

    [Serializable] class Config { public string apiKey, projectId; }
    [Serializable] class RespuestaAlta { public string idToken, refreshToken, expiresIn, localId; }
    [Serializable] class RespuestaRenovar { public string id_token, refresh_token, expires_in, user_id; }
    // El documento como lo devuelve Firestore: cada valor dentro de un objeto con su tipo.
    [Serializable] class Documento { public Campos fields; public string updateTime; }
    [Serializable] class Campos { public Valor monedas, nivelAlcanzado, actualizado; public ValorMapa nivel1, nivel2; }
    [Serializable] class Valor { public string integerValue, timestampValue; public bool booleanValue; }
    [Serializable] class ValorMapa { public Mapa mapValue; }
    [Serializable] class Mapa { public CamposNivel fields; }
    [Serializable] class CamposNivel { public Valor completado, monedas; public ValorArreglo tomadas; }
    [Serializable] class ValorArreglo { public Arreglo arrayValue; }
    [Serializable] class Arreglo { public Valor[] values; }

    public static BaseDeDatos Instancia { get; private set; }

    public Partida Partida { get; private set; }
    public Estado EstadoActual { get; private set; } = Estado.Conectando;
    public bool Configurada => config != null;
    // Si este jugador no tenía partida en la nube (la primera vez).
    public bool Nueva { get; private set; }
    public string Uid { get; private set; }
    public string Proyecto => config != null ? config.projectId : null;
    public string RutaDocumento => Coleccion + "/" + Uid;
    // Lo último que se leyó de la nube, tal cual, y la hora de esa versión del documento.
    public string UltimoDocumento { get; private set; } = "";
    public string UltimaActualizacion { get; private set; } = "";
    public string UltimoError { get; private set; } = "";
    public int Lecturas { get; private set; }
    public int Escrituras { get; private set; }
    // Si hay algo sin guardar o escribiéndose.
    public bool Pendiente => pendiente || escribiendo;
    public event Action<Estado> AlCambiar;

    Config config;
    string idToken, tokenRenovar;
    float venceToken;
    bool pendiente, escribiendo, saliendo, puedeSalir, leyendo;

    // Si el editor entra a Play sin recargar el dominio, que no quede la de la vez anterior.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        Instancia = null;
    }

    // La única: se crea al pedirla y sobrevive a recargar la escena al pasar de nivel.
    public static BaseDeDatos Asegurar()
    {
        if (Instancia == null)
        {
            var go = new GameObject("BaseDeDatos");
            DontDestroyOnLoad(go);
            Instancia = go.AddComponent<BaseDeDatos>();
        }
        return Instancia;
    }

    void Awake()
    {
        config = LeerConfig();
        Application.wantsToQuit += AlQuererSalir;
    }

    void OnDestroy()
    {
        Application.wantsToQuit -= AlQuererSalir;
        if (Instancia == this)
        {
            Instancia = null;
        }
    }

    static Config LeerConfig()
    {
        string ruta = Path.Combine(Application.streamingAssetsPath, "firebase.json");
        if (!File.Exists(ruta))
        {
            return null;
        }
        try
        {
            var c = JsonUtility.FromJson<Config>(File.ReadAllText(ruta));
            return c != null && !string.IsNullOrEmpty(c.apiKey) && !string.IsNullOrEmpty(c.projectId) ? c : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Lee la partida de Firestore. Termina con Partida lista: la de la nube o, si este jugador no tenía, una nueva que se
    // escribe en ese momento. Sin configuración, una nueva que no se guarda.
    public IEnumerator Cargar()
    {
        if (Partida != null)
        {
            yield break;
        }
        if (config == null)
        {
            Partida = new Partida();
            Cambiar(Estado.SinConfiguracion);
            yield break;
        }
        float espera = 1f;
        while (true)
        {
            Cambiar(Estado.Conectando);
            yield return Sesion();
            if (idToken != null)
            {
                using (var pedido = Pedido("GET", UrlDocumento(), null))
                {
                    yield return pedido.SendWebRequest();
                    Lecturas++;
                    if (pedido.result == UnityWebRequest.Result.Success)
                    {
                        UltimoDocumento = pedido.downloadHandler.text;
                        Partida = Leer(UltimoDocumento, out string hora);
                        UltimaActualizacion = hora;
                        Nueva = false;
                        Debug.Log("Firestore: partida cargada de " + RutaDocumento + " (monedas " + Partida.Monedas + ", nivel alcanzado " + Partida.nivelAlcanzado + ")");
                        Cambiar(Estado.Cargada);
                        yield break;
                    }
                    if (pedido.responseCode == 404)
                    {
                        // Primera vez de este jugador: su documento se crea ya, con la partida en cero.
                        Partida = new Partida();
                        Nueva = true;
                        Debug.Log("Firestore: " + RutaDocumento + " no existía; partida nueva");
                        Cambiar(Estado.Cargada);
                        Guardar();
                        yield break;
                    }
                    if (pedido.responseCode == 401 || pedido.responseCode == 403)
                    {
                        idToken = null;
                    }
                    Fallo("leer", pedido);
                }
            }
            Cambiar(Estado.SinConexion);
            yield return new WaitForSecondsRealtime(espera);
            espera = Mathf.Min(espera * 2f, 30f);
        }
    }

    // Empieza a leer la partida sin esperarla (el menú de inicio la muestra cuando llega). Una sola lectura a la vez.
    public void Leer()
    {
        if (Partida == null && !leyendo)
        {
            StartCoroutine(LeerUnaVez());
        }
    }

    IEnumerator LeerUnaVez()
    {
        leyendo = true;
        yield return Cargar();
        leyendo = false;
    }

    // La partida vuelve a cero y así se guarda. Sólo con la partida ya leída, para no escribir a ciegas sobre la nube.
    public void PartidaNueva()
    {
        if (Partida == null)
        {
            return;
        }
        Partida = new Partida();
        Debug.Log("Firestore: partida nueva en " + (Configurada ? RutaDocumento : "este equipo (sin base)"));
        Guardar();
    }

    // Pide guardar la partida. Lo que cambie en el siguiente cuarto de segundo sale en la misma escritura.
    public void Guardar()
    {
        if (config == null || Partida == null)
        {
            return;
        }
        pendiente = true;
        if (!escribiendo)
        {
            StartCoroutine(Escribir());
        }
    }

    // Vuelve a leer el documento de la nube sin tocar la partida en juego (para comprobar lo que quedó guardado).
    public IEnumerator Releer(Action<Partida, string> listo)
    {
        if (config == null)
        {
            listo?.Invoke(null, "");
            yield break;
        }
        yield return Sesion();
        if (idToken == null)
        {
            listo?.Invoke(null, "");
            yield break;
        }
        using (var pedido = Pedido("GET", UrlDocumento(), null))
        {
            yield return pedido.SendWebRequest();
            Lecturas++;
            if (pedido.result != UnityWebRequest.Result.Success)
            {
                Fallo("releer", pedido);
                listo?.Invoke(null, "");
                yield break;
            }
            string texto = pedido.downloadHandler.text;
            listo?.Invoke(Leer(texto, out _), texto);
        }
    }

    IEnumerator Escribir()
    {
        escribiendo = true;
        float espera = 1f;
        while (pendiente)
        {
            yield return new WaitForSecondsRealtime(Agrupar);
            pendiente = false;
            Cambiar(Estado.Guardando);
            yield return Sesion();
            bool ok = false;
            if (idToken != null)
            {
                using (var pedido = Pedido("PATCH", UrlDocumento(), Json(Partida)))
                {
                    yield return pedido.SendWebRequest();
                    if (pedido.result == UnityWebRequest.Result.Success)
                    {
                        ok = true;
                        Escrituras++;
                        Debug.Log("Firestore: guardada en " + RutaDocumento + " (monedas " + Partida.Monedas + ", nivel alcanzado " + Partida.nivelAlcanzado + ")");
                    }
                    else
                    {
                        if (pedido.responseCode == 401 || pedido.responseCode == 403)
                        {
                            idToken = null;
                        }
                        Fallo("guardar", pedido);
                    }
                }
            }
            if (ok)
            {
                espera = 1f;
                if (!pendiente)
                {
                    Cambiar(Estado.Guardada);
                }
                continue;
            }
            // No se pudo: se reintenta con lo más reciente.
            pendiente = true;
            Cambiar(Estado.SinConexion);
            yield return new WaitForSecondsRealtime(espera);
            espera = Mathf.Min(espera * 2f, 30f);
        }
        escribiendo = false;
    }

    // Deja un idToken vigente: renueva la sesión con el token guardado o, si no hay (primera vez en este equipo o con este
    // proyecto) o ya no sirve, crea el usuario anónimo.
    IEnumerator Sesion()
    {
        if (idToken != null && Time.realtimeSinceStartup < venceToken)
        {
            yield break;
        }
        idToken = null;
        if (tokenRenovar == null && PlayerPrefs.GetString(ClaveProyecto, "") == config.projectId)
        {
            tokenRenovar = PlayerPrefs.GetString(ClaveToken, "");
        }
        if (!string.IsNullOrEmpty(tokenRenovar))
        {
            string forma = "grant_type=refresh_token&refresh_token=" + UnityWebRequest.EscapeURL(tokenRenovar);
            using (var pedido = new UnityWebRequest("https://securetoken.googleapis.com/v1/token?key=" + config.apiKey, "POST"))
            {
                pedido.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(forma)) { contentType = "application/x-www-form-urlencoded" };
                pedido.downloadHandler = new DownloadHandlerBuffer();
                pedido.timeout = 10;
                yield return pedido.SendWebRequest();
                if (pedido.result == UnityWebRequest.Result.Success)
                {
                    var r = JsonUtility.FromJson<RespuestaRenovar>(pedido.downloadHandler.text);
                    Aceptar(r.id_token, r.refresh_token, r.expires_in, r.user_id);
                    yield break;
                }
                Fallo("renovar la sesión", pedido);
                // Sin respuesta es que no hay red: se reintenta después. Con respuesta, el token ya no sirve.
                if (pedido.result != UnityWebRequest.Result.ProtocolError)
                {
                    yield break;
                }
                tokenRenovar = null;
            }
        }
        using (var pedido = Pedido("POST", "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=" + config.apiKey, "{\"returnSecureToken\":true}"))
        {
            yield return pedido.SendWebRequest();
            if (pedido.result == UnityWebRequest.Result.Success)
            {
                var r = JsonUtility.FromJson<RespuestaAlta>(pedido.downloadHandler.text);
                Aceptar(r.idToken, r.refreshToken, r.expiresIn, r.localId);
                Debug.Log("Firebase Authentication: usuario anónimo nuevo " + Uid);
            }
            else
            {
                Fallo("crear el usuario anónimo", pedido);
            }
        }
    }

    void Aceptar(string id, string renovar, string vence, string uid)
    {
        idToken = id;
        tokenRenovar = renovar;
        Uid = uid;
        float segundos = float.TryParse(vence, NumberStyles.Float, CultureInfo.InvariantCulture, out float s) ? s : 3600f;
        // Se renueva un minuto antes de que venza.
        venceToken = Time.realtimeSinceStartup + Mathf.Max(60f, segundos - 60f);
        PlayerPrefs.SetString(ClaveToken, renovar);
        PlayerPrefs.SetString(ClaveProyecto, config.projectId);
        PlayerPrefs.Save();
    }

    UnityWebRequest Pedido(string metodo, string url, string cuerpo)
    {
        var pedido = new UnityWebRequest(url, metodo) { downloadHandler = new DownloadHandlerBuffer(), timeout = 10 };
        if (cuerpo != null)
        {
            pedido.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(cuerpo)) { contentType = "application/json" };
        }
        if (idToken != null)
        {
            pedido.SetRequestHeader("Authorization", "Bearer " + idToken);
        }
        return pedido;
    }

    string UrlDocumento() => "https://firestore.googleapis.com/v1/projects/" + config.projectId + "/databases/(default)/documents/" + Coleccion + "/" + Uid;

    void Fallo(string que, UnityWebRequest pedido)
    {
        string cuerpo = pedido.downloadHandler != null ? pedido.downloadHandler.text : "";
        if (cuerpo != null && cuerpo.Length > 300)
        {
            cuerpo = cuerpo.Substring(0, 300);
        }
        UltimoError = que + ": " + pedido.responseCode + " " + pedido.error + " " + cuerpo;
        Debug.Log("Firestore: no se pudo " + UltimoError);
    }

    void Cambiar(Estado nuevo)
    {
        EstadoActual = nuevo;
        AlCambiar?.Invoke(nuevo);
    }

    // ---------- el documento ----------

    static string Json(Partida p)
    {
        var sb = new StringBuilder("{\"fields\":{");
        sb.Append("\"monedas\":").Append(Entero(p.Monedas)).Append(',');
        sb.Append("\"nivelAlcanzado\":").Append(Entero(p.nivelAlcanzado)).Append(',');
        for (int i = 0; i < Partida.Niveles; i++)
        {
            var n = p.niveles[i];
            sb.Append("\"nivel").Append(i + 1).Append("\":{\"mapValue\":{\"fields\":{");
            sb.Append("\"completado\":{\"booleanValue\":").Append(n.completado ? "true" : "false").Append("},");
            sb.Append("\"monedas\":").Append(Entero(n.tomadas.Count)).Append(',');
            sb.Append("\"tomadas\":{\"arrayValue\":{\"values\":[").Append(string.Join(",", n.tomadas.Select(t => Entero(t)))).Append("]}}");
            sb.Append("}}},");
        }
        sb.Append("\"actualizado\":{\"timestampValue\":\"").Append(DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)).Append("\"}");
        sb.Append("}}");
        return sb.ToString();
    }

    static string Entero(int v) => "{\"integerValue\":\"" + v.ToString(CultureInfo.InvariantCulture) + "\"}";

    static Partida Leer(string json, out string hora)
    {
        var d = JsonUtility.FromJson<Documento>(json);
        hora = d != null ? d.updateTime : "";
        var p = new Partida();
        if (d == null || d.fields == null)
        {
            return p;
        }
        p.nivelAlcanzado = Mathf.Clamp(Entero(d.fields.nivelAlcanzado, 1), 1, Partida.Niveles);
        LeerNivel(d.fields.nivel1, p.niveles[0]);
        LeerNivel(d.fields.nivel2, p.niveles[1]);
        return p;
    }

    static void LeerNivel(ValorMapa v, ProgresoNivel n)
    {
        var f = v?.mapValue?.fields;
        if (f == null)
        {
            return;
        }
        n.completado = f.completado != null && f.completado.booleanValue;
        n.tomadas.Clear();
        var valores = f.tomadas?.arrayValue?.values;
        if (valores != null)
        {
            n.tomadas.AddRange(valores.Select(x => Entero(x, -1)).Where(i => i >= 0).Distinct().OrderBy(i => i));
        }
    }

    static int Entero(Valor v, int siNo) => v != null && int.TryParse(v.integerValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : siNo;

    // ---------- al cerrar ----------

    // Cerrar el juego con una escritura pendiente: se espera a que termine (hasta 3 s) y entonces sí se cierra.
    bool AlQuererSalir()
    {
        if (puedeSalir || !Pendiente)
        {
            return true;
        }
        if (!saliendo)
        {
            StartCoroutine(SalirAlTerminar());
        }
        return false;
    }

    IEnumerator SalirAlTerminar()
    {
        saliendo = true;
        float limite = Time.realtimeSinceStartup + EsperaAlSalir;
        while (Pendiente && Time.realtimeSinceStartup < limite)
        {
            yield return null;
        }
        puedeSalir = true;
        Application.Quit();
    }
}
