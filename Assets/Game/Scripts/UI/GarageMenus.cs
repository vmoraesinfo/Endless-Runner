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
        dailyPanel.SetActive(DailyReward.Available);
        if (DailyReward.Available)
        {
            int day = DailyReward.NextDay;
            dailyDayText.text = $"DIA {day} DE 7";
            dailyRewardText.text = $"+{DailyReward.RewardFor(day)}";
        }
        RefreshBadge();
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
