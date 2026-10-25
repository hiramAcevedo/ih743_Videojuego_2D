using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// La partida que se guarda en Firestore: el nivel alcanzado (la ruta en la que va el jugador) y, por nivel, si ya se
// completó y qué propinas se tomaron (su número dentro de la ruta). El total de propinas es la suma de las tomadas en los
// dos niveles: así ninguna se cuenta dos veces y, al volver a abrir el juego, las que ya se tomaron no aparecen otra vez.
[System.Serializable]
public class Partida
{
    public const int Niveles = 2;

    public int nivelAlcanzado = 1;
    public ProgresoNivel[] niveles = { new ProgresoNivel(), new ProgresoNivel() };

    public int Monedas => niveles.Sum(n => n.tomadas.Count);

    public ProgresoNivel Nivel(int numero) => niveles[Mathf.Clamp(numero, 1, Niveles) - 1];

    // Falso si esa propina ya estaba tomada.
    public bool Tomar(int nivel, int indice)
    {
        var n = Nivel(nivel);
        if (n.tomadas.Contains(indice))
        {
            return false;
        }
        n.tomadas.Add(indice);
        n.tomadas.Sort();
        return true;
    }

    // El nivel queda completo y el alcanzado pasa al siguiente (el último se queda en el último).
    public void Completar(int nivel)
    {
        Nivel(nivel).completado = true;
        nivelAlcanzado = Mathf.Max(nivelAlcanzado, Mathf.Min(nivel + 1, Niveles));
    }
}

[System.Serializable]
public class ProgresoNivel
{
    public bool completado;
    public List<int> tomadas = new List<int>();
}
