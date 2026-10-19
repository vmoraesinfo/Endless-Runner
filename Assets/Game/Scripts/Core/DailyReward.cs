using System;
using UnityEngine;

/// <summary>
/// Recompensa por entrar no jogo em dias seguidos (7 dias, depois recomeça).
/// Perder um dia volta a sequência para o dia 1.
/// </summary>
public static class DailyReward
{
    const string LastKey = "daily_last";
    const string StreakKey = "daily_streak";
    static readonly int[] Rewards = { 50, 75, 100, 150, 200, 250, 500 };

    static int Today => ToInt(DateTime.Now.Date);
    static int Yesterday => ToInt(DateTime.Now.Date.AddDays(-1));
    static int ToInt(DateTime d) => d.Year * 10000 + d.Month * 100 + d.Day;

    public static bool Available => PlayerPrefs.GetInt(LastKey, 0) != Today;

    /// <summary>Dia da sequência (1..7) que será resgatado agora.</summary>
    public static int NextDay
    {
        get
        {
            int last = PlayerPrefs.GetInt(LastKey, 0);
            int streak = PlayerPrefs.GetInt(StreakKey, 0);
            if (last != Yesterday) return 1;
            return streak % Rewards.Length + 1;
        }
    }

    public static int RewardFor(int day) => Rewards[Mathf.Clamp(day, 1, Rewards.Length) - 1];

    public static int Claim()
    {
        if (!Available) return 0;
        int day = NextDay;
        int reward = RewardFor(day);
        PlayerPrefs.SetInt(LastKey, Today);
        PlayerPrefs.SetInt(StreakKey, day);
        SaveData.Coins += reward;
        return reward;
    }
}
