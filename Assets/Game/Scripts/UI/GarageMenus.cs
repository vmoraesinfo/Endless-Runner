using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Painéis da garagem: missões (3 ativas, resgatáveis) e recompensa diária.</summary>
public class GarageMenus : MonoBehaviour
{
    [System.Serializable]
    public class MissionRow
    {
        public TMP_Text description;
        public TMP_Text progress;
        public Image bar;
        public Button claimButton;
        public TMP_Text claimLabel;
    }

    public SelectCars garage;

    [Header("Missões")]
    public GameObject missionsPanel;
    public GameObject missionsBadge;
    public MissionRow[] rows;

    [Header("Recompensa diária")]
    public GameObject dailyPanel;
    public TMP_Text dailyDayText;
    public TMP_Text dailyRewardText;

    void Start()
    {
        missionsPanel.SetActive(false);
        if (upgradesPanel != null) upgradesPanel.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(false);
        dailyPanel.SetActive(DailyReward.Available);
        if (DailyReward.Available)
        {
            int day = DailyReward.NextDay;
            dailyDayText.text = $"DIA {day} DE 7";
            dailyRewardText.text = $"+{DailyReward.RewardFor(day)}";
        }
        RefreshBadge();
    }

    // ---------- Loja (compras com dinheiro real) ----------

    [Header("Loja")]
    public GameObject shopPanel;
    public TMP_Text noAdsPrice;
    public Button noAdsButton;
    public TMP_Text starterPrice;
    public TMP_Text shopStatus;

    void OnEnable() => Purchases.Changed += RefreshShop;
    void OnDisable() => Purchases.Changed -= RefreshShop;

    public void OpenShop()
    {
        Sfx.I.Click();
        shopPanel.SetActive(true);
        shopStatus.text = "";
        RefreshShop();
    }

    public void CloseShop()
    {
        Sfx.I.Click();
        shopPanel.SetActive(false);
    }

    public void BuyNoAds() => Buy(Purchases.RemoveAdsId);
    public void BuyStarter() => Buy(Purchases.StarterPackId);

    public void RestorePurchases()
    {
        Purchases.Instance.Restore();
        shopStatus.text = "Compras restauradas.";
    }

    void Buy(string id)
    {
        shopStatus.text = "Abrindo a loja...";
        Purchases.Instance.Buy(id, ok =>
        {
            shopStatus.text = ok ? "Compra concluída. Obrigado!" : "Compra não concluída.";
            if (ok) Sfx.I.PowerUp();
            RefreshShop();
            garage.RefreshUI();
        });
    }

    void RefreshShop()
    {
        if (shopPanel == null || !shopPanel.activeSelf) return;
        bool noAds = SaveData.NoAds;
        noAdsButton.interactable = !noAds;
        noAdsPrice.text = noAds ? "COMPRADO" : Purchases.Instance.Price(Purchases.RemoveAdsId, "R$ 9,90");
        starterPrice.text = Purchases.Instance.Price(Purchases.StarterPackId, "R$ 4,90");
    }

    // ---------- Melhorias ----------

    [System.Serializable]
    public class UpgradeRow
    {
        public UpgradeType type;
        public TMP_Text description;
        public Image[] pips;
        public Button buyButton;
        public TMP_Text buyLabel;
    }

    [Header("Melhorias")]
    public GameObject upgradesPanel;
    public UpgradeRow[] upgradeRows;

    public void OpenUpgrades()
    {
        Sfx.I.Click();
        upgradesPanel.SetActive(true);
        RefreshUpgrades();
    }

    public void CloseUpgrades()
    {
        Sfx.I.Click();
        upgradesPanel.SetActive(false);
    }

    public void BuyNitro() => Buy(UpgradeType.Nitro);
    public void BuyMagnet() => Buy(UpgradeType.Magnet);
    public void BuyShield() => Buy(UpgradeType.Shield);

    void Buy(UpgradeType type)
    {
        if (Upgrades.TryBuy(type)) Sfx.I.PowerUp();
        RefreshUpgrades();
        garage.RefreshUI();
    }

    void RefreshUpgrades()
    {
        foreach (var row in upgradeRows)
        {
            int level = Upgrades.Level(row.type);
            int price = Upgrades.NextPrice(row.type);
            row.description.text = Upgrades.Describe(row.type);
            for (int i = 0; i < row.pips.Length; i++)
                row.pips[i].color = i < level ? new Color(1f, 0.8f, 0.15f) : new Color(1f, 1f, 1f, 0.15f);
            row.buyButton.interactable = price >= 0 && SaveData.Coins >= price;
            row.buyLabel.text = price < 0 ? "MÁXIMO" : $"MELHORAR {price}";
        }
    }

    public void ClaimDaily()
    {
        DailyReward.Claim();
        dailyPanel.SetActive(false);
        Sfx.I.PowerUp();
        garage.RefreshUI();
    }

    public void OpenMissions()
    {
        Sfx.I.Click();
        missionsPanel.SetActive(true);
        RefreshMissions();
    }

    public void CloseMissions()
    {
        Sfx.I.Click();
        missionsPanel.SetActive(false);
        RefreshBadge();
    }

    public void Claim(int index)
    {
        if (Missions.Claim(index) > 0) Sfx.I.PowerUp();
        RefreshMissions();
        garage.RefreshUI();
    }

    // Botões da UI só aceitam métodos com até 1 parâmetro; um por linha deixa a ligação explícita.
    public void Claim0() => Claim(0);
    public void Claim1() => Claim(1);
    public void Claim2() => Claim(2);

    void RefreshMissions()
    {
        var list = Missions.Active;
        for (int i = 0; i < rows.Length && i < list.Count; i++)
        {
            var m = list[i];
            var row = rows[i];
            row.description.text = Missions.Describe(m);
            row.progress.text = $"{m.progress}/{m.target}";
            row.bar.fillAmount = m.target > 0 ? m.progress / (float)m.target : 0f;
            row.claimButton.interactable = m.Done;
            row.claimLabel.text = m.Done ? $"PEGAR +{m.reward}" : $"+{m.reward}";
        }
        RefreshBadge();
    }

    void RefreshBadge()
    {
        missionsBadge.SetActive(Missions.AnyClaimable);
    }
}
