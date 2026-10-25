using UnityEngine;

// Un nivel del juego: una ruta del camión de la basura. Trae su calle (la semilla de CalleModular y los tintes, para que la
// ruta 2 vaya al atardecer), dónde está cada propina y dónde termina: donde espera el camión. Cada propina es su x en el mundo y
// su altura sobre la banqueta: a 1 u se toma corriendo, de 2.8 a 3.2 u hace falta un salto y a 4.3 u el doble salto.
[CreateAssetMenu(fileName = "Ruta", menuName = "Recolector/Nivel (ruta)")]
public class Nivel : ScriptableObject
{
    [Tooltip("1 o 2: el número con que se guarda en la base.")]
    public int numero = 1;
    public string nombre = "RUTA 1";
    [Tooltip("La semilla de CalleModular: otro orden de fachadas.")]
    public int semilla = 743;
    public Color tinteCielo = Color.white;
    public Color tinteCalle = Color.white;
    [Tooltip("x de la parte de atrás del camión, donde termina la ruta.")]
    public float xMeta = 60f;
    [Tooltip("Cada propina: x en el mundo y altura sobre la banqueta.")]
    public Vector2[] monedas = new Vector2[0];
}
