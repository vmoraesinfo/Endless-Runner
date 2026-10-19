using System;
using UnityEngine;
using Unity.Services.LevelPlay;

/// <summary>
/// Ponto único de anúncios (LevelPlay). Cria-se sozinho na primeira chamada e sobrevive às trocas de cena.
/// No editor os anúncios com recompensa são simulados (concedem a recompensa na hora) para dar para testar o fluxo.
/// </summary>
public class AdsManager : MonoBehaviour
{
    static AdsManager instance;

    AdsSettings settings;
    bool initialized;
    LevelPlayBannerAd banner;
    LevelPlayInterstitialAd interstitial;
    LevelPlayRewardedAd rewarded;
    Action pendingReward;
    Action pendingRewardClosed;
    Action pendingInterstitialClosed;
    bool rewardEarned;
    int gameOvers;
    bool bannerWanted;

    public static AdsManager Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("AdsManager");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<AdsManager>();
            }
            return instance;
        }
    }

    void Awake()
    {
        settings = Resources.Load<AdsSettings>("AdsSettings");
        if (settings == null) settings = ScriptableObject.CreateInstance<AdsSettings>();
        Init();
    }

    void Init()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (string.IsNullOrEmpty(settings.androidAppKey))
        {
            Debug.LogWarning("[Ads] androidAppKey vazio em AdsSettings; anúncios desativados.");
            return;
        }
        LevelPlay.OnInitSuccess += _ => OnInitialized();
        LevelPlay.OnInitFailed += error => Debug.LogWarning("[Ads] Falha ao iniciar LevelPlay: " + error);
        LevelPlay.Init(settings.androidAppKey);
#endif
    }

    void OnInitialized()
    {
        initialized = true;
        if (settings.debugValidateIntegration) LevelPlay.ValidateIntegration();

        if (!string.IsNullOrEmpty(settings.interstitialAdUnitId))
        {
            interstitial = new LevelPlayInterstitialAd(settings.interstitialAdUnitId);
            interstitial.OnAdClosed += _ => FinishInterstitial();
            interstitial.OnAdDisplayFailed += _ => FinishInterstitial();
            interstitial.LoadAd();
        }

        if (!string.IsNullOrEmpty(settings.rewardedAdUnitId))
        {
            rewarded = new LevelPlayRewardedAd(settings.rewardedAdUnitId);
            rewarded.OnAdRewarded += (_, __) => rewardEarned = true;
            rewarded.OnAdClosed += _ => FinishRewarded();
            rewarded.OnAdDisplayFailed += _ => FinishRewarded();
            rewarded.LoadAd();
        }

        if (bannerWanted) ShowBanner();
    }

    // ---------- Banner ----------

    public void ShowBanner()
    {
        bannerWanted = true;
        if (!initialized || string.IsNullOrEmpty(settings.bannerAdUnitId)) return;
        if (banner == null)
        {
            // Posição padrão: rodapé centralizado (nunca no meio da tela — gera clique acidental).
            banner = new LevelPlayBannerAd(settings.bannerAdUnitId, respectSafeArea: true);
            banner.LoadAd();
        }
        else banner.ShowAd();
    }

    public void HideBanner()
    {
        bannerWanted = false;
        banner?.HideAd();
    }

    // ---------- Rewarded ----------

    public bool IsRewardedReady
    {
        get
        {
#if UNITY_EDITOR
            return true;
#else
            return rewarded != null && rewarded.IsAdReady();
#endif
        }
    }

    /// <summary>Mostra um anúncio com recompensa. onReward só é chamado se o jogador assistir até o fim.</summary>
    public void ShowRewarded(Action onReward, Action onClosed = null)
    {
#if UNITY_EDITOR
        onReward?.Invoke();
        onClosed?.Invoke();
#else
        if (!IsRewardedReady) { onClosed?.Invoke(); return; }
        pendingReward = onReward;
        pendingRewardClosed = onClosed;
        rewardEarned = false;
        rewarded.ShowAd();
#endif
    }

    void FinishRewarded()
    {
        if (rewardEarned) pendingReward?.Invoke();
        pendingRewardClosed?.Invoke();
        pendingReward = null;
        pendingRewardClosed = null;
        rewarded?.LoadAd();
    }

    // ---------- Interstitial ----------

    /// <summary>Conta uma partida encerrada e, a cada N, mostra um intersticial antes de continuar.</summary>
    public void OnGameOverThen(Action next)
    {
        gameOvers++;
        int every = settings.interstitialEveryNGameOvers;
        bool due = every > 0 && gameOvers % every == 0;
        if (!due || interstitial == null || !interstitial.IsAdReady())
        {
            next?.Invoke();
            return;
        }
        pendingInterstitialClosed = next;
        interstitial.ShowAd();
    }

    void FinishInterstitial()
    {
        var next = pendingInterstitialClosed;
        pendingInterstitialClosed = null;
        interstitial?.LoadAd();
        next?.Invoke();
    }
}
