using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lista de carros jogáveis, compartilhada entre a garagem e a corrida.
/// Fica em Resources para ser carregada de qualquer cena.
/// </summary>
[CreateAssetMenu(menuName = "Endless Runner/Car Catalog")]
public class CarCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string displayName;
        public GameObject prefab;
        public int price;
    }

    public List<Entry> cars = new List<Entry>();

    static CarCatalog instance;

    public static CarCatalog Load()
    {
        if (instance == null) instance = Resources.Load<CarCatalog>("CarCatalog");
        return instance;
    }

    public int Count => cars.Count;

    public Entry Get(int index)
    {
        if (cars.Count == 0) return null;
        return cars[Mathf.Clamp(index, 0, cars.Count - 1)];
    }
}
