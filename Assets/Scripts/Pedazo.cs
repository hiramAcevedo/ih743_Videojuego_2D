using UnityEngine;

// Los pedazos de una bolsa o una botella que se rompe: cuadritos de un color que saltan en abanico, caen con la gravedad
// y desaparecen en medio segundo. No tienen física ni chocan con nada; sólo se ven.
public class Pedazo : MonoBehaviour
{
    Vector2 velocidad;
    float vida;

    // Suelta cantidad pedazos desde posicion, repartidos en abanico hacia arriba; sentido inclina el abanico.
    public static void Soltar(Sprite pixel, Vector3 posicion, Color color, int cantidad, float rapidez, int sentido, int orden)
    {
        if (pixel == null)
        {
            return;
        }

        for (int i = 0; i < cantidad; i++)
        {
            float f = cantidad > 1 ? i / (cantidad - 1f) : 0.5f;
            float angulo = Mathf.Lerp(20f, 160f, f) * Mathf.Deg2Rad;
            var go = new GameObject("Pedazo");
            go.transform.position = posicion;
            float lado = 2f + (i % 2);
            go.transform.localScale = new Vector3(lado, lado, 1f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = pixel;
            r.color = color;
            r.sortingOrder = orden;
            var p = go.AddComponent<Pedazo>();
            p.velocidad = new Vector2(Mathf.Cos(angulo) + 0.4f * sentido, Mathf.Sin(angulo)) * rapidez * (0.7f + 0.3f * ((i * 7) % 5) / 4f);
            p.vida = 0.45f + 0.05f * (i % 3);
        }
    }

    void Update()
    {
        velocidad += Physics2D.gravity * Time.deltaTime;
        transform.position += (Vector3)(velocidad * Time.deltaTime);
        vida -= Time.deltaTime;
        if (vida <= 0f)
        {
            Destroy(gameObject);
        }
    }
}
