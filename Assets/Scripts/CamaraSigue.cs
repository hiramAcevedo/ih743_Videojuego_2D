using UnityEngine;

// La cámara sigue al recolector como en el tutorial (34:07): en cada cuadro copia su posición en x. En y lo deja
// subir hasta alturaLibre sin moverse, así que el salto sencillo no la mueve y el suelo sigue a la vista; si sube más,
// con el doble salto, la cámara sube con él y al bajar regresa a la altura que tiene en la escena. Como en el tutorial
// (46:40), si el objetivo deja de existir la cámara se queda donde está y el juego sigue.
//   El tamaño de la vista no se toca aquí: lo fija la Pixel Perfect Camera (referencia 1920x1080 a 96 PPU,
//   20 x 11.25 unidades). Ella pone la cámara en el píxel más cercano a su posición y dibuja al recolector en el píxel
//   más cercano a la suya. En x la posición es la misma y en y, cuando lo sigue, la distancia entre los dos es un número
//   entero de píxeles: caen en el mismo píxel y él no tiembla en la pantalla.
// Se ejecuta antes que CalleModular para que la calle se arme con la posición de este cuadro.
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Camera))]
public class CamaraSigue : MonoBehaviour
{
    [Tooltip("A quién sigue: el Recolector. Si se destruye, la cámara se queda donde está.")]
    [SerializeField] Transform objetivo;

    [Tooltip("Cuánto puede subir el recolector, en unidades, antes de que la cámara suba con él. Con 1.3 el salto sencillo (1.22 u) no la mueve y el doble salto sí; con 0 lo sigue siempre en vertical.")]
    [SerializeField] float alturaLibre = 1.3f;

    [Tooltip("Píxeles por unidad de la Pixel Perfect Camera: la distancia en y se redondea a píxeles enteros.")]
    [SerializeField] float pixelesPorUnidad = 96f;

    // La altura de la cámara en la escena: de ahí no baja y a ella regresa.
    float alturaCamara;
    // La altura más baja que ha tenido el recolector: la de los pies en el suelo.
    float piso;

    void Start()
    {
        alturaCamara = transform.position.y;
        piso = objetivo != null ? objetivo.position.y : 0f;
    }

    void LateUpdate()
    {
        if (objetivo == null)
        {
            return;
        }

        Vector3 o = objetivo.position;
        if (o.y < piso)
        {
            piso = o.y;
        }

        // Cuando ya lo sigue en y, la cámara va a esta distancia del recolector, en píxeles enteros.
        float distancia = Mathf.Round((alturaCamara - piso - alturaLibre) * pixelesPorUnidad) / pixelesPorUnidad;

        Vector3 posicion = transform.position;
        posicion.x = o.x;
        posicion.y = Mathf.Max(alturaCamara, o.y + distancia);
        transform.position = posicion;
    }
}
