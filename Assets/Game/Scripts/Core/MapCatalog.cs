using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pistas (mapas) do jogo. Cada uma tem cenário, trânsito, obstáculos e recompensa próprios,
/// e é desbloqueada ao percorrer uma distância na pista anterior.
/// Fica em Resources/MapCatalog.asset.
/// </summary>
[CreateAssetMenu(menuName = "Endless Runner/Map Catalog")]
public class MapCatalog : ScriptableObject
{
    [Serializable]
    public class Map
    {
        public string id;
        public string displayName;
        [Tooltip("Distância (m) numa única corrida na pista anterior para desbloquear. 0 = liberada.")]
        public int unlockDistance;
        [Tooltip("Quantas moedas cada moeda coletada vale nesta pista.")]
        public int coinValue = 1;

        [Header("Cenário")]
        public RoadSegment segmentPrefab;
        public BiomeManager.Biome[] biomes;

        [Header("Trânsito")]
        public TrafficCatalog traffic;
        [Tooltip("Velocidade do trânsito (m/s). Com 0 o trânsito fica parado (congestionamento) e obras são possíveis.")]
        public float trafficSpeed = 6f;
        [Range(0f, 1f)] public float laneChangeChance;
        [Range(0f, 1f)] public float roadworksChance;
        public GameObject[] roadworkPrefabs;
    }

    public List<Map> maps = new List<Map>();

    static MapCatalog instance;

    public static MapCatalog Load()
    {
        if (instance == null) instance = Resources.Load<MapCatalog>("MapCatalog");
        return instance;
    }

    public int Count => maps.Count;
    public Map Get(int index) => maps[Mathf.Clamp(index, 0, maps.Count - 1)];

    public bool IsUnlocked(int index)
    {
        if (index <= 0) return true;
        var map = Get(index);
        return map.unlockDistance <= 0 || SaveData.BestDistance(Get(index - 1).id) >= map.unlockDistance;
    }

    /// <summary>Pista escolhida no save, ou a primeira se a escolhida estiver bloqueada.</summary>
    public int SelectedIndex
    {
        get
        {
            int i = Mathf.Clamp(SaveData.SelectedMap, 0, maps.Count - 1);
            return IsUnlocked(i) ? i : 0;
        }
    }
}
