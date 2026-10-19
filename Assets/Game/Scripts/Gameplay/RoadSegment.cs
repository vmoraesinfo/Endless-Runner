using UnityEngine;

/// <summary>
/// Um pedaço de estrada com cenário. A cada reaproveitamento sorteia posição, giro e escala
/// das decorações para a paisagem não se repetir de forma óbvia.
/// </summary>
public class RoadSegment : MonoBehaviour
{
    public Transform[] decorations;
    public float segmentLength = 24f;
    public Vector2 decorationXRange = new Vector2(9f, 30f);
    [Range(0f, 1f)] public float decorationChance = 0.75f;

    [Tooltip("Pontos fixos (ex.: lotes de prédio). Cada um tem várias variações como filhos; uma é sorteada por vez.")]
    public Transform[] variantSlots = new Transform[0];
    [Range(0f, 1f)] public float variantChance = 1f;

    public void Randomize()
    {
        foreach (var slot in variantSlots)
        {
            int count = slot.childCount;
            int pick = Random.value < variantChance ? Random.Range(0, count) : -1;
            for (int i = 0; i < count; i++) slot.GetChild(i).gameObject.SetActive(i == pick);
        }

        foreach (var d in decorations)
        {
            bool show = Random.value < decorationChance;
            d.gameObject.SetActive(show);
            if (!show) continue;
            float side = Random.value < 0.5f ? -1f : 1f;
            float x = side * Random.Range(decorationXRange.x, decorationXRange.y);
            d.localPosition = new Vector3(x, 0f, Random.Range(0f, segmentLength));
            d.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            d.localScale = Vector3.one * Random.Range(0.8f, 1.4f);
        }
    }
}
