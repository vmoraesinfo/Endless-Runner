using UnityEngine;

/// <summary>Faz o elemento "respirar" (escala), para chamar atenção em textos como "toque para começar".</summary>
public class Pulse : MonoBehaviour
{
    public float amount = 0.06f;
    public float speed = 3f;

    void Update()
    {
        transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * speed) * amount);
    }
}
