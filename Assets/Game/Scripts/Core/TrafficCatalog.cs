using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Veículos do trânsito (obstáculos), separados dos carros jogáveis da garagem.
/// Mantêm a proporção real entre si (caminhão é mais comprido que hatch); `scale` ajusta o conjunto todo.
/// Fica em Resources para ser carregado na cena da corrida.
/// </summary>
[CreateAssetMenu(menuName = "Endless Runner/Traffic Catalog")]
public class TrafficCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public GameObject prefab;
        [Tooltip("Chance relativa de aparecer (caminhões menos que carros).")]
        public float weight = 1f;
    }

    [Tooltip("Escala aplicada a todos os veículos (os modelos vêm em metros).")]
    public float scale = 1.15f;
    [Tooltip("Largura máxima depois da escala, para caber na faixa.")]
    public float maxWidth = 2.9f;
    public List<Entry> vehicles = new List<Entry>();

    static TrafficCatalog instance;

    public static TrafficCatalog Load()
    {
        if (instance == null) instance = Resources.Load<TrafficCatalog>("TrafficCatalog");
        return instance;
    }

    public GameObject PickRandom()
    {
        float total = 0f;
        foreach (var v in vehicles) total += Mathf.Max(0f, v.weight);
        float r = UnityEngine.Random.value * total;
        foreach (var v in vehicles)
        {
            r -= Mathf.Max(0f, v.weight);
            if (r <= 0f) return v.prefab;
        }
        return vehicles[vehicles.Count - 1].prefab;
    }
}
