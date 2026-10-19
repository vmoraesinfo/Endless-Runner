using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Telas da corrida: início, HUD (pontos, combo, nitro, power-ups), pausa e fim de jogo.</summary>
public class GameUI : MonoBehaviour
{
    public struct GameOverInfo
    {
        public int score, best, distance, coins, nearMisses;
        public float bestMultiplier;
        public bool newBest, canRevive, canDouble, missionsReady;
    }

    [Header("Painéis")]
    public GameObject startPanel;
    public GameObject hudPanel;
    public GameObject pausePanel;
    public GameObject gameOverPanel;

    [Header("Início")]
    public TMP_Text startBestText;
    public GameObject tutorialHint;

    [Header("HUD")]
    public TMP_Text scoreText;
    public TMP_Text coinsText;
    public TMP_Text multiplierText;
    public Image comboBar;
    public Image nitroFill;
    public TMP_Text nitroLabel;
    public GameObject nitroReadyGlow;
    public TMP_Text powerUpText;
    public RectTransform popupRoot;

    [Header("Pausa")]
    public TMP_Text vibrationText;
    public TMP_Text soundText;

    [Header("Fim de jogo")]
    public TMP_Text goScoreText;
    public TMP_Text goBestText;
    public GameObject goNewBest;
    public TMP_Text goCoinsText;
    public TMP_Text goStatsText;
    public GameObject reviveButton;
    public GameObject doubleButton;
    public GameObject missionsHint;

    int shownScore = -1;
    int shownCoins = -1;
    float shownMultiplier = -1f;
    Coroutine punch;
    readonly List<TMP_Text> popupPool = new List<TMP_Text>();
    int popupSlot;

    public void ShowStart(int best, bool showTutorial)
    {
        SetOnly(startPanel);
        hudPanel.SetActive(true);
        startBestText.text = best > 0 ? $"RECORDE: {best:N0} pts" : "";
        // Dica de controle só aparece até o jogador completar a primeira partida.
        tutorialHint.SetActive(showTutorial);
    }

    public void ShowHud() => SetOnly(hudPanel);

    public void ShowPause(bool vibration, bool sound)
    {
        SetOnly(pausePanel);
        vibrationText.text = vibration ? "VIBRAÇÃO: LIGADA" : "VIBRAÇÃO: DESLIGADA";
        soundText.text = sound ? "SOM: LIGADO" : "SOM: DESLIGADO";
    }

    public void ShowGameOver(GameOverInfo info)
    {
        SetOnly(gameOverPanel);
        goScoreText.text = $"{info.score:N0}";
        goBestText.text = $"RECORDE: {info.best:N0}";
        goNewBest.SetActive(info.newBest);
        goCoinsText.text = $"+{info.coins} moedas";
        goStatsText.text = $"{info.distance:N0} m   |   {info.nearMisses} raspões   |   combo x{info.bestMultiplier:0.#}";
        reviveButton.SetActive(info.canRevive);
        doubleButton.SetActive(info.canDouble);
        missionsHint.SetActive(info.missionsReady);
    }

    public void UpdateHud(int score, int coins)
    {
        if (score != shownScore) { shownScore = score; scoreText.text = score.ToString("N0"); }
        if (coins != shownCoins) { shownCoins = coins; coinsText.text = coins.ToString(); }
    }

    public void UpdateCombo(float multiplier, float timer01)
    {
        if (!Mathf.Approximately(multiplier, shownMultiplier))
        {
            bool up = multiplier > shownMultiplier;
            shownMultiplier = multiplier;
            multiplierText.text = $"x{multiplier:0.#}";
            multiplierText.color = multiplier >= 3f ? new Color(1f, 0.5f, 0.2f) : multiplier > 1f ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 1f, 1f, 0.6f);
            if (up && multiplier > 1f) StartCoroutine(Punch(multiplierText.transform, 0.5f));
        }
        comboBar.fillAmount = timer01;
    }

    public void UpdateNitro(float fill01, bool ready, bool active)
    {
        nitroFill.fillAmount = fill01;
        nitroFill.color = active ? new Color(1f, 0.55f, 0.15f) : ready ? new Color(0.35f, 0.85f, 1f) : new Color(0.25f, 0.55f, 0.9f);
        nitroReadyGlow.SetActive(ready);
        nitroLabel.text = active ? "NITRO!" : ready ? "NITRO PRONTO" : "NITRO";
    }

    public void UpdatePowerUps(float magnetSeconds, bool shield)
    {
        string text = "";
        if (magnetSeconds > 0f) text += $"ÍMÃ {Mathf.CeilToInt(magnetSeconds)}s";
        if (shield) text += (text.Length > 0 ? "   " : "") + "ESCUDO";
        if (powerUpText.text != text) powerUpText.text = text;
    }

    /// <summary>Texto que sobe e some no meio da tela (raspão, nitro, bioma...).</summary>
    public void Popup(string title, string subtitle, Color color, float scale)
    {
        if (popupPool.Count < 4)
        {
            var t = Instantiate(popupRoot.GetChild(0).GetComponent<TMP_Text>(), popupRoot);
            popupPool.Add(t);
        }
        var text = popupPool[popupSlot];
        popupSlot = (popupSlot + 1) % popupPool.Count;
        text.text = string.IsNullOrEmpty(subtitle) ? title : $"{title}\n<size=60%>{subtitle}</size>";
        text.color = color;
        StartCoroutine(AnimatePopup(text, scale));
    }

    IEnumerator AnimatePopup(TMP_Text text, float scale)
    {
        text.gameObject.SetActive(true);
        var rt = (RectTransform)text.transform;
        float t = 0f;
        const float duration = 0.9f;
        Color c = text.color;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = t / duration;
            rt.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, 160f, k));
            float pop = k < 0.15f ? Mathf.Lerp(0.5f, 1.15f, k / 0.15f) : Mathf.Lerp(1.15f, 1f, (k - 0.15f) / 0.2f);
            rt.localScale = Vector3.one * scale * pop;
            text.color = new Color(c.r, c.g, c.b, k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);
            yield return null;
        }
        text.gameObject.SetActive(false);
    }

    public void PunchCoins()
    {
        if (punch != null) StopCoroutine(punch);
        punch = StartCoroutine(Punch(coinsText.transform, 0.35f));
    }

    IEnumerator Punch(Transform t, float amount)
    {
        float time = 0f;
        while (time < 0.15f)
        {
            time += Time.unscaledDeltaTime;
            t.localScale = Vector3.one * (1f + amount * Mathf.Sin(time / 0.15f * Mathf.PI));
            yield return null;
        }
        t.localScale = Vector3.one;
    }

    void SetOnly(GameObject panel)
    {
        startPanel.SetActive(panel == startPanel);
        hudPanel.SetActive(panel == hudPanel);
        pausePanel.SetActive(panel == pausePanel);
        gameOverPanel.SetActive(panel == gameOverPanel);
    }
}
