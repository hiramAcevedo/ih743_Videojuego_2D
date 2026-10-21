using UnityEngine;

// Un sprite que se desvanece y desaparece: las copias de la estela de la botella y los destellos de la mano y de la
// rotura. No tiene física ni choca con nada; sólo se ve.
public class Desvanecer : MonoBehaviour
{
    SpriteRenderer dibujo;
    Color color;
    float vida, edad;

    public float Edad => edad;
    public float Vida => vida;
    public float Opacidad => dibujo != null ? dibujo.color.a : 0f;

    // Pone el sprite en posicion (del mundo) con su giro y su escala y lo desvanece desde color en vida segundos. Puede
    // nacer con edad: la estela lo usa para que cada copia tenga la que le toca aunque el cuadro llegue tarde. Con padre,
    // se mueve con él (el destello de la mano sigue al recolector que corre).
    public static Desvanecer Crear(Sprite sprite, Vector3 posicion, Quaternion giro, float escala, Color color, float vida, float edad,
        int capa, int orden, Transform padre = null, Material material = null, bool volteado = false)
    {
        if (sprite == null || vida <= 0f || edad >= vida)
        {
            return null;
        }

        var go = new GameObject("Desvanecer");
        if (padre != null)
        {
            go.transform.SetParent(padre, false);
        }
        go.transform.position = posicion;
        go.transform.rotation = giro;
        go.transform.localScale = new Vector3(escala, escala, 1f);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        if (material != null)
        {
            r.sharedMaterial = material;
        }
        r.sortingLayerID = capa;
        r.sortingOrder = orden;
        r.flipX = volteado;
        var d = go.AddComponent<Desvanecer>();
        d.dibujo = r;
        d.color = color;
        d.vida = vida;
        d.edad = edad;
        d.Pintar();
        return d;
    }

    void Update()
    {
        edad += Time.deltaTime;
        if (edad >= vida)
        {
            Destroy(gameObject);
            return;
        }
        Pintar();
    }

    void Pintar()
    {
        var c = color;
        c.a *= 1f - edad / vida;
        dibujo.color = c;
    }
}
