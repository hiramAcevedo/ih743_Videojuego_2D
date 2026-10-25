using System;
using UnityEngine;

// La meta de un nivel: el camión de la basura, estacionado al final de la ruta en el asfalto, delante de la banqueta. Cuando
// el recolector alcanza su parte de atrás, el nivel queda completo (GestorNiveles lo guarda en la base y pasa al siguiente).
// Encima flota un letrero de META con una flecha; si el nivel ya estaba completo, no hay letrero y llegar no hace nada.
[RequireComponent(typeof(BoxCollider2D))]
public class Meta : MonoBehaviour
{
    [Tooltip("El letrero de META, hijo del camión.")]
    [SerializeField] TextoPixel letrero;
    [Tooltip("Cuánto sube y baja el letrero, en unidades.")]
    [SerializeField] float flotacion = 0.1f;
    [SerializeField] float periodo = 1f;

    Action alLlegar;
    Vector3 reposoLetrero;

    public bool Alcanzada { get; private set; }

    public void Preparar(Action avisar, bool yaCompleta)
    {
        alLlegar = avisar;
        Alcanzada = yaCompleta;
        if (letrero != null)
        {
            letrero.gameObject.SetActive(!yaCompleta);
            reposoLetrero = letrero.transform.localPosition;
        }
    }

    void Update()
    {
        if (letrero != null && letrero.gameObject.activeSelf)
        {
            letrero.transform.localPosition = reposoLetrero + Vector3.up * (Mathf.Sin(Time.time * 2f * Mathf.PI / periodo) * flotacion);
        }
    }

    void OnTriggerEnter2D(Collider2D otro)
    {
        if (Alcanzada)
        {
            return;
        }
        var cuerpo = otro.attachedRigidbody;
        if (cuerpo == null || cuerpo.GetComponent<RecolectorControl>() == null)
        {
            return;
        }
        Alcanzada = true;
        if (letrero != null)
        {
            letrero.gameObject.SetActive(false);
        }
        alLlegar?.Invoke();
    }
}
