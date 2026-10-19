using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>Telas da corrida: início, HUD, pausa e fim de jogo. Os botões chamam o GameManager.</summary>
public class GameUI : MonoBehaviour
{
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

    [Header("Pausa")]
    public TMP_Text vibrationText;

    [Header("Fim de jogo")]
    public TMP_Text goScoreText;
    public TMP_Text goBestText;
    public GameObject goNewBest;
    public TMP_Text goCoinsText;
    public GameObject reviveButton;

    int shownScore = -1;
    int shownCoins = -1;
    Coroutine punch;

    public void ShowStart(int best, bool showTutorial)
    {
        SetOnly(startPanel);
        hudPanel.SetActive(true);
        startBestText.text = best > 0 ? $"RECORDE: {best} m" : "";
        // Dica de controle só aparece até o jogador completar a primeira partida.
        tutorialHint.SetActive(showTutorial);
    }

    public void ShowHud()
    {
        SetOnly(hudPanel);
    }

    public void ShowPause(bool vibration)
    {
        SetOnly(pausePanel);
        vibrationText.text = vibration ? "VIBRAÇÃO: LIGADA" : "VIBRAÇÃO: DESLIGADA";
    }

    public void ShowGameOver(int score, int best, bool newBest, int coins, bool canRevive)
    {
        SetOnly(gameOverPanel);
        goScoreText.text = $"{score} m";
        goBestText.text = $"RECORDE: {best} m";
        goNewBest.SetActive(newBest);
        goCoinsText.text = $"+{coins} moedas";
        reviveButton.SetActive(canRevive);
    }

    public void UpdateHud(int score, int coins)
    {
        if (score != shownScore) { shownScore = score; scoreText.text = $"{score} m"; }
        if (coins != shownCoins) { shownCoins = coins; coinsText.text = coins.ToString(); }
    }

    public void PunchCoins()
    {
        if (punch != null) StopCoroutine(punch);
        punch = StartCoroutine(Punch(coinsText.transform));
    }

    IEnumerator Punch(Transform t)
    {
        float time = 0f;
        while (time < 0.15f)
        {
            time += Time.unscaledDeltaTime;
            t.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(time / 0.15f * Mathf.PI));
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
