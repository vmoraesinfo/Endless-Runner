using UnityEngine;

/// <summary>
/// Pinturas por carro: a 0 é a cor original (grátis); as outras custam moedas.
/// Compra e escolha ficam salvas por carro (índice da garagem).
/// </summary>
public static class Paints
{
    public struct Paint
    {
        public string name;
        public Color color;
    }

    public const int Price = 250;

    public static readonly Paint[] All =
    {
        new Paint { name = "ORIGINAL", color = Color.white },
        new Paint { name = "VERMELHO", color = new Color(0.86f, 0.12f, 0.12f) },
        new Paint { name = "AZUL", color = new Color(0.12f, 0.38f, 0.9f) },
        new Paint { name = "VERDE", color = new Color(0.15f, 0.72f, 0.3f) },
        new Paint { name = "ROXO", color = new Color(0.55f, 0.22f, 0.85f) },
        new Paint { name = "DOURADO", color = new Color(0.95f, 0.72f, 0.12f) },
    };

    public static bool IsOwned(int car, int paint) =>
        paint == 0 || PlayerPrefs.GetInt($"paint_{car}_{paint}", 0) == 1;

    public static int Selected(int car)
    {
        int p = PlayerPrefs.GetInt($"paint_sel_{car}", 0);
        return p >= 0 && p < All.Length && IsOwned(car, p) ? p : 0;
    }

    public static void Select(int car, int paint)
    {
        if (!IsOwned(car, paint)) return;
        PlayerPrefs.SetInt($"paint_sel_{car}", paint);
        PlayerPrefs.Save();
    }

    public static bool TryBuy(int car, int paint)
    {
        if (IsOwned(car, paint)) return true;
        if (!SaveData.TrySpend(Price)) return false;
        PlayerPrefs.SetInt($"paint_{car}_{paint}", 1);
        Select(car, paint);
        return true;
    }
}
