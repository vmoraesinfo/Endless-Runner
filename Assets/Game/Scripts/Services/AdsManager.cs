using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

/// <summary>
/// Ponto único de anúncios (Google AdMob). Cria-se sozinho na primeira chamada e sobrevive às trocas de cena.
/// Fluxo: pede consentimento (formulário do Google, só aparece para quem precisa — ex.: Europa),
/// inicializa o SDK e deixa intersticial e premiado sempre carregados.
/// No editor (Play) aparecem os anúncios de demonstração do próprio plugin do Google, com os blocos de teste.
/// Em batchmode (teste automatizado) nada é carregado: o premiado é simulado e concede a recompensa na hora.
/// </summary>
public class AdsManager : MonoBehaviour
{
    static AdsManager instance;

    AdsSettings settings;
    bool initialized;
    int gameOvers;
    bool bannerWanted;
    BannerView banner;
    InterstitialAd interstitial;
    RewardedAd rewarded;
    Action pendingReward;
    Action pendingRewardClosed;
    Action pendingInterstitialClosed;
    bool rewardEarned;

    static bool Live => Application.platform == RuntimePlatform.Android || (Application.isEditor && !Application.isBatchMode);
    static bool InEditor => Application.isEditor;

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
        if (!Live) return;

        // Callbacks do SDK chegam na thread principal (pode mexer em UI/cena com segurança).
        MobileAds.RaiseAdEventsOnUnityMainThread = true;
        if (settings.testDeviceIds.Count > 0)
            MobileAds.SetRequestConfiguration(new RequestConfiguration { TestDeviceIds = settings.testDeviceIds });
        // O formulário de consentimento só existe no aparelho; no editor inicializa direto.
        if (InEditor) InitializeSdk();
        else GatherConsent();
    }

    // ---------- Consentimento (UMP) + inicialização ----------

    void GatherConsent()
    {
        var request = new ConsentRequestParameters { TagForUnderAgeOfConsent = false };
        ConsentInformation.Update(request, updateError =>
        {
            if (updateError != null) Debug.LogWarning("[Ads] Consentimento: " + updateError.Message);
            ConsentForm.LoadAndShowConsentFormIfRequired(showError =>
            {
                if (showError != null) Debug.LogWarning("[Ads] Formulário de consentimento: " + showError.Message);
                if (ConsentInformation.CanRequestAds()) InitializeSdk();
            });
        });
        // Quem já respondeu numa sessão anterior pode carregar anúncios sem esperar.
        if (ConsentInformation.CanRequestAds()) InitializeSdk();
    }

    void InitializeSdk()
    {
        if (initialized) return;
        initialized = true;
        MobileAds.Initialize(_ =>
        {
            LoadInterstitial();
            LoadRewarded();
            if (bannerWanted) ShowBanner();
        });
    }

    void LoadInterstitial()
    {
        if (string.IsNullOrEmpty(settings.Interstitial)) return;
        interstitial?.Destroy();
        interstitial = null;
        InterstitialAd.Load(settings.Interstitial, new AdRequest(), (ad, error) =>
        {
            if (error != null || ad == null) { Debug.LogWarning("[Ads] Intersticial não carregou: " + error); return; }
            interstitial = ad;
            ad.OnAdFullScreenContentClosed += FinishInterstitial;
            ad.OnAdFullScreenContentFailed += _ => FinishInterstitial();
        });
    }

    void LoadRewarded()
    {
        if (string.IsNullOrEmpty(settings.Rewarded)) return;
        rewarded?.Destroy();
        rewarded = null;
        RewardedAd.Load(settings.Rewarded, new AdRequest(), (ad, error) =>
        {
            if (error != null || ad == null) { Debug.LogWarning("[Ads] Premiado não carregou: " + error); return; }
            rewarded = ad;
            ad.OnAdFullScreenContentClosed += FinishRewarded;
            ad.OnAdFullScreenContentFailed += _ => FinishRewarded();
        });
    }

    /// <summary>Precisa mostrar o botão "Privacidade" (obrigatório para usuários da Europa).</summary>
    public bool PrivacyOptionsRequired =>
        Live && !InEditor && ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

    public void ShowPrivacyOptions()
    {
        if (!Live || InEditor) return;
        ConsentForm.ShowPrivacyOptionsForm(error =>
        {
            if (error != null) Debug.LogWarning("[Ads] Privacidade: " + error.Message);
        });
    }

    // ---------- Banner ----------

    public void ShowBanner()
    {
        if (SaveData.NoAds) { HideBanner(); return; } // comprou "Remover anúncios"
        bannerWanted = true;
        if (!initialized || string.IsNullOrEmpty(settings.Banner)) return;
        if (banner == null)
        {
            // Rodapé centralizado (nunca no meio da tela — gera clique acidental).
            banner = new BannerView(settings.Banner, AdSize.Banner, AdPosition.Bottom);
            banner.LoadAd(new AdRequest());
        }
        else banner.Show();
    }

    public void HideBanner()
    {
        bannerWanted = false;
        banner?.Hide();
    }

    // ---------- Premiado ----------

    public bool IsRewardedReady => !Live || (rewarded != null && rewarded.CanShowAd());

    /// <summary>Mostra um anúncio premiado. onReward só é chamado se o jogador assistir até o fim.</summary>
    public void ShowRewarded(Action onReward, Action onClosed = null)
    {
        if (!Live)
        {
            onReward?.Invoke();
            onClosed?.Invoke();
            return;
        }
        if (!IsRewardedReady) { onClosed?.Invoke(); return; }
        pendingReward = onReward;
        pendingRewardClosed = onClosed;
        rewardEarned = false;
        rewarded.Show(_ => rewardEarned = true);
    }

    void FinishRewarded()
    {
        var reward = rewardEarned ? pendingReward : null;
        var closed = pendingRewardClosed;
        pendingReward = null;
        pendingRewardClosed = null;
        rewardEarned = false;
        reward?.Invoke();
        closed?.Invoke();
        LoadRewarded();
    }

    // ---------- Intersticial ----------

    /// <summary>Conta uma partida encerrada e, a cada N, mostra um intersticial antes de continuar.</summary>
    public void OnGameOverThen(Action next)
    {
        gameOvers++;
        int every = settings.interstitialEveryNGameOvers;
        bool due = every > 0 && gameOvers % every == 0 && !SaveData.NoAds;
        if (due && interstitial != null && interstitial.CanShowAd())
        {
            pendingInterstitialClosed = next;
            interstitial.Show();
            return;
        }
        next?.Invoke();
    }

    void FinishInterstitial()
    {
        var next = pendingInterstitialClosed;
        pendingInterstitialClosed = null;
        LoadInterstitial();
        next?.Invoke();
    }
}
