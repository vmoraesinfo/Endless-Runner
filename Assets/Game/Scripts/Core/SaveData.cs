using UnityEngine;

/// <summary>
/// Progresso do jogador salvo em PlayerPrefs: moedas, recorde, carro escolhido e carros comprados.
/// </summary>
public static class SaveData
{
    const string CoinsKey = "coins";
    const string BestKey = "best_score";
    const string SelectedCarKey = "selected_car";
    const string UnlockedPrefix = "car_unlocked_";
    const string VibrationKey = "vibration";
    const string TutorialKey = "tutorial_done";

    public static int Coins
    {
        get => PlayerPrefs.GetInt(CoinsKey, 0);
        set { PlayerPrefs.SetInt(CoinsKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }

    public static int BestScore
    {
        get => PlayerPrefs.GetInt(BestKey, 0);
        set { PlayerPrefs.SetInt(BestKey, value); PlayerPrefs.Save(); }
    }

    public static int SelectedCar
    {
        get => PlayerPrefs.GetInt(SelectedCarKey, 0);
        set { PlayerPrefs.SetInt(SelectedCarKey, value); PlayerPrefs.Save(); }
    }

    public static bool Vibration
    {
        get => PlayerPrefs.GetInt(VibrationKey, 1) == 1;
        set { PlayerPrefs.SetInt(VibrationKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static int SelectedMap
    {
        get => PlayerPrefs.GetInt("selected_map", 0);
        set { PlayerPrefs.SetInt("selected_map", value); PlayerPrefs.Save(); }
    }

    /// <summary>Maior distância (m) numa única corrida em cada pista; libera a pista seguinte.</summary>
    public static int BestDistance(string mapId) => PlayerPrefs.GetInt("best_dist_" + mapId, 0);

    public static void ReportDistance(string mapId, int meters)
    {
        if (meters <= BestDistance(mapId)) return;
        PlayerPrefs.SetInt("best_dist_" + mapId, meters);
        PlayerPrefs.Save();
    }

    /// <summary>Comprou "Remover anúncios": sem banner e sem intersticial.</summary>
    public static bool NoAds
    {
        get => PlayerPrefs.GetInt("no_ads", 0) == 1;
        set { PlayerPrefs.SetInt("no_ads", value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static bool Sound
    {
        get => PlayerPrefs.GetInt("sound", 1) == 1;
        set { PlayerPrefs.SetInt("sound", value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static bool TutorialDone
    {
        get => PlayerPrefs.GetInt(TutorialKey, 0) == 1;
        set { PlayerPrefs.SetInt(TutorialKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static bool IsCarUnlocked(int index, int price)
    {
        return price <= 0 || PlayerPrefs.GetInt(UnlockedPrefix + index, 0) == 1;
    }

    public static void UnlockCar(int index)
    {
        PlayerPrefs.SetInt(UnlockedPrefix + index, 1);
        PlayerPrefs.Save();
    }

    /// <summary>Tenta gastar moedas. Retorna false se não houver saldo.</summary>
    public static bool TrySpend(int amount)
    {
        if (Coins < amount) return false;
        Coins -= amount;
        return true;
    }
}
