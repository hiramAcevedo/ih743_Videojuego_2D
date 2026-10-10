using UnityEngine;

// La cámara sigue al recolector como en el tutorial (34:07): en cada cuadro copia su posición en x y deja
// la altura y la profundidad que tiene en la escena, así que el salto no la mueve. Como en el tutorial
// (46:40), si el objetivo deja de existir la cámara se queda donde está y el juego sigue.
//   El tamaño de la vista no se toca aquí: lo fija la Pixel Perfect Camera (referencia 1920x1080 a 96 PPU,
//   20 x 11.25 unidades). Ella también pone la cámara en el píxel más cercano a su posición y dibuja al
//   recolector en el píxel más cercano a la suya; como la x es la misma, caen en el mismo y él no se mueve
//   en la pantalla.
// Se ejecuta antes que CalleModular para que la calle se arme con la posición de este cuadro.
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Camera))]
public class CamaraSigue : MonoBehaviour
{
    [Tooltip("A quién sigue: el Recolector. Si se destruye, la cámara se queda donde está.")]
    [SerializeField] Transform objetivo;

    void LateUpdate()
    {
        if (objetivo == null)
        {
            return;
        }

        Vector3 posicion = transform.position;
        posicion.x = objetivo.position.x;
        transform.position = posicion;
    }
}
