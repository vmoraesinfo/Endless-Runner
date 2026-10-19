using UnityEngine;

public enum UpgradeType { Nitro, Magnet, Shield }

/// <summary>
/// Melhorias compradas com moedas na garagem (5 níveis cada), salvas em PlayerPrefs.
/// Os valores efetivos (duração, cargas) ficam aqui para o jogo e a UI usarem a mesma fonte.
/// </summary>
public static class Upgrades
{
    public const int MaxLevel = 5;
    static readonly int[] Prices = { 200, 500, 1000, 2000, 4000 };

    public static int Level(UpgradeType t) => Mathf.Clamp(PlayerPrefs.GetInt("upgrade_" + t, 0), 0, MaxLevel);

    /// <summary>Preço do próximo nível, ou -1 se já está no máximo.</summary>
    public static int NextPrice(UpgradeType t)
    {
        int level = Level(t);
        return level >= MaxLevel ? -1 : Prices[level];
    }

    public static bool TryBuy(UpgradeType t)
    {
        int price = NextPrice(t);
        if (price < 0 || !SaveData.TrySpend(price)) return false;
        PlayerPrefs.SetInt("upgrade_" + t, Level(t) + 1);
        PlayerPrefs.Save();
        return true;
    }

    public static float NitroDuration(int level) => 4f + 0.6f * level;      // 4s → 7s
    public static float MagnetDuration(int level) => 8f + 2f * level;      // 8s → 18s
    public static int ShieldCharges(int level) => 1 + level;               // 1 → 6 batidas

    public static string Title(UpgradeType t) => t switch
    {
        UpgradeType.Nitro => "NITRO",
        UpgradeType.Magnet => "ÍMÃ",
        _ => "ESCUDO",
    };

    /// <summary>Efeito atual e o do próximo nível, ex.: "4,0s → 4,6s".</summary>
    public static string Describe(UpgradeType t)
    {
        int l = Level(t);
        string now = Value(t, l);
        return l >= MaxLevel ? $"{now} (máximo)" : $"{now}  →  {Value(t, l + 1)}";
    }

    static string Value(UpgradeType t, int level) => t switch
    {
        UpgradeType.Nitro => $"{NitroDuration(level):0.0}s de nitro",
        UpgradeType.Magnet => $"{MagnetDuration(level):0}s de ímã",
        _ => ShieldCharges(level) == 1 ? "aguenta 1 batida" : $"aguenta {ShieldCharges(level)} batidas",
    };
}
