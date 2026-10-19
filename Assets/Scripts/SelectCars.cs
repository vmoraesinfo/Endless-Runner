using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Garagem: navega entre os carros (filhos de carHolder, na mesma ordem do CarCatalog),
/// compra carros bloqueados com moedas e inicia a corrida com o carro escolhido.
/// </summary>
public class SelectCars : MonoBehaviour
{
    public const string GameScene = "Game";

    private List<GameObject> Cars;
    public GameObject carHolder;
    private int carIndex = 0;

    [Header("UI")]
    public TMP_Text carNameText;
    public TMP_Text coinsText;
    public TMP_Text bestText;
    public TMP_Text actionButtonText;
    public Button actionButton;
    public GameObject lockedBadge;
    public TMP_Text mapText;

    CarCatalog catalog;
    MapCatalog maps;
    int mapIndex;

    void Start()
    {
        Time.timeScale = 1f;
        catalog = CarCatalog.Load();
        maps = MapCatalog.Load();
        if (maps != null) mapIndex = maps.SelectedIndex;
        Cars = GetAllChildren(carHolder);
        if (Cars.Count == 0) return;
        carIndex = Mathf.Clamp(SaveData.SelectedCar, 0, Cars.Count - 1);
        DisplayCars();
        AdsManager.Instance.ShowBanner();
    }

    public void NextCar()
    {
        if (Cars.Count == 0) return;
        carIndex = (carIndex + 1) % Cars.Count;
        DisplayCars();
    }

    public void PreviousCar()
    {
        if (Cars.Count == 0) return;
        carIndex = (carIndex - 1 + Cars.Count) % Cars.Count;
        DisplayCars();
    }

    /// <summary>Alterna a pista. Pistas bloqueadas aparecem com o requisito, mas não dá para jogar nelas.</summary>
    public void NextMap()
    {
        if (maps == null || maps.Count == 0) return;
        mapIndex = (mapIndex + 1) % maps.Count;
        if (maps.IsUnlocked(mapIndex)) SaveData.SelectedMap = mapIndex;
        Sfx.I.Click();
        RefreshUI();
    }

    bool MapLocked => maps != null && !maps.IsUnlocked(mapIndex);

    /// <summary>Botão principal: joga com o carro se já for dele, ou tenta comprar.</summary>
    public void OnActionButton()
    {
        var entry = catalog.Get(carIndex);
        bool owned = SaveData.IsCarUnlocked(carIndex, entry.price);
        if (owned && MapLocked) return; // botão fica desativado; proteção extra para não cobrar de novo
        if (owned)
        {
            SaveData.SelectedCar = carIndex;
            AdsManager.Instance.HideBanner();
            SceneManager.LoadScene(GameScene);
            return;
        }

        if (SaveData.TrySpend(entry.price))
        {
            SaveData.UnlockCar(carIndex);
            SaveData.SelectedCar = carIndex;
        }
        DisplayCars();
    }

    void DisplayCars()
    {
        for (int i = 0; i < Cars.Count; i++)
        {
            Cars[i].SetActive(i == carIndex);
        }
        RefreshUI();
    }

    public void RefreshUI()
    {
        if (catalog == null || carNameText == null) return;
        var entry = catalog.Get(carIndex);
        bool unlocked = SaveData.IsCarUnlocked(carIndex, entry.price);

        carNameText.text = entry.displayName.ToUpperInvariant();
        coinsText.text = SaveData.Coins.ToString();
        bestText.text = SaveData.BestScore > 0 ? $"RECORDE: {SaveData.BestScore:N0} pts" : "";
        lockedBadge.SetActive(!unlocked);

        if (mapText != null && maps != null && maps.Count > 0)
        {
            var map = maps.Get(mapIndex);
            string bonus = map.coinValue > 1 ? $"  (moedas x{map.coinValue})" : "";
            mapText.text = MapLocked
                ? $"{map.displayName}: FAÇA {map.unlockDistance} m NA {maps.Get(mapIndex - 1).displayName}"
                : $"PISTA: {map.displayName}{bonus}";
        }

        if (unlocked && MapLocked)
        {
            actionButtonText.text = "PISTA BLOQUEADA";
            actionButton.interactable = false;
        }
        else if (unlocked)
        {
            actionButtonText.text = "JOGAR";
            actionButton.interactable = true;
        }
        else
        {
            actionButtonText.text = $"COMPRAR  {entry.price}";
            actionButton.interactable = SaveData.Coins >= entry.price;
        }
    }

    List<GameObject> GetAllChildren(GameObject parent)
    {
        List<GameObject> result = new List<GameObject>();

        foreach (Transform child in parent.transform)
        {
            if (child.GetComponent<Rotator>() == null) child.gameObject.AddComponent<Rotator>();
            result.Add(child.gameObject);
        }

        return result;
    }
}
