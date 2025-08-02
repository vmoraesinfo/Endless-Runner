using UnityEngine;
using com.unity3d.mediation;
using System.Collections;

public class BannerAdSample : MonoBehaviour
{
    private LevelPlayBannerAd bannerAd;

    void Start()
    {
        Debug.Log("Iniciando SDK IronSource...");

        // Escuta o evento de inicialização do SDK
        IronSourceEvents.onSdkInitializationCompletedEvent += OnSdkInitialized;

        // Inicia o SDK com os ad units necessários
        IronSource.Agent.init("1pox3ykjcdvt5p9y", IronSourceAdUnits.BANNER);
        IronSource.Agent.validateIntegration();
    }

    private void OnSdkInitialized()
    {
        Debug.Log("SDK inicializado. Aguardando um instante para criar o banner...");
        StartCoroutine(DelayedBannerInit());
    }

    private IEnumerator DelayedBannerInit()
    {
        // Aguarda 1 segundo só por segurança (LevelPlay pode precisar de um pequeno tempo após init)
        yield return new WaitForSeconds(15f);

        var adConfig = new LevelPlayBannerAd.Config.Builder()
            .SetPlacementName("Home_Screen")
            .SetPosition(LevelPlayBannerPosition.Center) // ou Center
            .SetDisplayOnLoad(true)
            .SetRespectSafeArea(true)
            .Build();

        // Cria o banner
        bannerAd = new LevelPlayBannerAd("1pox3ykjcdvt5p9y", LevelPlayAdSize.BANNER);

        // Registra os eventos
        bannerAd.OnAdLoaded += BannerOnAdLoadedEvent;
        bannerAd.OnAdLoadFailed += BannerOnAdLoadFailedEvent;
        bannerAd.OnAdDisplayed += BannerOnAdDisplayedEvent;
        bannerAd.OnAdDisplayFailed += BannerOnAdDisplayFailedEvent;
        bannerAd.OnAdClicked += BannerOnAdClickedEvent;
        bannerAd.OnAdCollapsed += BannerOnAdCollapsedEvent;
        bannerAd.OnAdLeftApplication += BannerOnAdLeftApplicationEvent;
        bannerAd.OnAdExpanded += BannerOnAdExpandedEvent;

        Debug.Log("Solicitando carregamento do banner...");
        bannerAd.LoadAd();
    }

    public void LoadBannerAd() => bannerAd?.LoadAd();
    public void ShowBannerAd() => bannerAd?.ShowAd();
    public void HideBannerAd() => bannerAd?.HideAd();
    public void DestroyBannerAd() => bannerAd?.DestroyAd();

    // Callbacks para debug
    private void BannerOnAdLoadedEvent(LevelPlayAdInfo adInfo) => Debug.Log("✅ Banner carregado com sucesso.");
    private void BannerOnAdLoadFailedEvent(LevelPlayAdError error) => Debug.LogError($"❌ Falha ao carregar banner: {error}");
    private void BannerOnAdClickedEvent(LevelPlayAdInfo adInfo) => Debug.Log("👉 Banner clicado.");
    private void BannerOnAdDisplayedEvent(LevelPlayAdInfo adInfo) => Debug.Log("📢 Banner exibido.");
    private void BannerOnAdDisplayFailedEvent(LevelPlayAdDisplayInfoError error) => Debug.LogError($"❌ Erro ao exibir banner: {error}");
    private void BannerOnAdCollapsedEvent(LevelPlayAdInfo adInfo) => Debug.Log("📉 Banner recolhido.");
    private void BannerOnAdLeftApplicationEvent(LevelPlayAdInfo adInfo) => Debug.Log("🚪 Usuário saiu do app após clicar no banner.");
    private void BannerOnAdExpandedEvent(LevelPlayAdInfo adInfo) => Debug.Log("📈 Banner expandido.");
}
