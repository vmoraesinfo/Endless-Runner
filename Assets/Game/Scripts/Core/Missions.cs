using System;
using System.Collections.Generic;
using UnityEngine;

public enum MissionType { CoinsTotal, NearMissInRun, DistanceInRun, NitroTotal, RunsTotal, ComboInRun }

/// <summary>
/// Sempre há 3 missões ativas. Ao resgatar uma, entra outra um pouco mais difícil (nível sobe).
/// Missões "InRun" valem o melhor resultado numa única corrida; "Total" acumulam entre corridas.
/// </summary>
public static class Missions
{
    [Serializable]
    public class Mission
    {
        public MissionType type;
        public int target;
        public int progress;
        public int reward;
        public bool Done => progress >= target;
    }

    [Serializable]
    class SaveBlob { public int level; public List<Mission> list = new List<Mission>(); }

    const string Key = "missions_v1";
    public const int ActiveCount = 3;
    static SaveBlob blob;

    static SaveBlob Data
    {
        get
        {
            if (blob != null) return blob;
            string json = PlayerPrefs.GetString(Key, "");
            blob = string.IsNullOrEmpty(json) ? new SaveBlob() : JsonUtility.FromJson<SaveBlob>(json);
            while (blob.list.Count < ActiveCount) blob.list.Add(Generate());
            Save();
            return blob;
        }
    }

    public static IReadOnlyList<Mission> Active => Data.list;

    public static bool AnyClaimable
    {
        get { foreach (var m in Data.list) if (m.Done) return true; return false; }
    }

    static void Save() => PlayerPrefs.SetString(Key, JsonUtility.ToJson(blob));

    /// <summary>Acumula progresso. Valores "total" são deltas; valores "InRun" são o resultado atual da corrida.</summary>
    public static void Report(int coinsDelta, int nitroDelta, int runsDelta, int nearMissInRun, int distanceInRun, int comboInRun)
    {
        foreach (var m in Data.list)
        {
            if (m.Done) continue;
            switch (m.type)
            {
                case MissionType.CoinsTotal: m.progress += coinsDelta; break;
                case MissionType.NitroTotal: m.progress += nitroDelta; break;
                case MissionType.RunsTotal: m.progress += runsDelta; break;
                case MissionType.NearMissInRun: m.progress = Mathf.Max(m.progress, nearMissInRun); break;
                case MissionType.DistanceInRun: m.progress = Mathf.Max(m.progress, distanceInRun); break;
                case MissionType.ComboInRun: m.progress = Mathf.Max(m.progress, comboInRun); break;
            }
            m.progress = Mathf.Min(m.progress, m.target);
        }
        Save();
        PlayerPrefs.Save();
    }

    /// <summary>Resgata a missão concluída, credita as moedas e coloca uma nova no lugar.</summary>
    public static int Claim(int index)
    {
        var m = Data.list[index];
        if (!m.Done) return 0;
        SaveData.Coins += m.reward;
        blob.level++;
        blob.list[index] = Generate();
        Save();
        PlayerPrefs.Save();
        return m.reward;
    }

    static Mission Generate()
    {
        int lv = blob.level;
        var used = new HashSet<MissionType>();
        foreach (var existing in blob.list) used.Add(existing.type);
        var types = (MissionType[])Enum.GetValues(typeof(MissionType));
        MissionType type;
        int guard = 0;
        do { type = types[UnityEngine.Random.Range(0, types.Length)]; } while (used.Contains(type) && ++guard < 50);

        int target = type switch
        {
            MissionType.CoinsTotal => 100 + lv * 40,
            MissionType.NearMissInRun => 3 + lv,
            MissionType.DistanceInRun => 600 + lv * 300,
            MissionType.NitroTotal => 1 + lv / 2,
            MissionType.RunsTotal => 3 + lv,
            MissionType.ComboInRun => Mathf.Min(5, 3 + lv / 2),
            _ => 1
        };
        return new Mission { type = type, target = target, reward = 60 + lv * 25 };
    }

    public static string Describe(Mission m) => m.type switch
    {
        MissionType.CoinsTotal => $"Junte {m.target} moedas",
        MissionType.NearMissInRun => $"Faça {m.target} raspões numa corrida",
        MissionType.DistanceInRun => $"Percorra {m.target} m numa corrida",
        MissionType.NitroTotal => $"Use o nitro {m.target}x",
        MissionType.RunsTotal => $"Jogue {m.target} partidas",
        MissionType.ComboInRun => $"Chegue ao combo x{m.target}",
        _ => "?"
    };
}
