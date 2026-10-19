using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// IDs do AdMob (admob.google.com → Apps → Endless Runner → Blocos de anúncios).
/// Fica em Resources/AdsSettings.asset. O App ID também precisa estar em
/// Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings (o GameBuilder copia de lá para cá).
/// </summary>
[CreateAssetMenu(menuName = "Endless Runner/Ads Settings")]
public class AdsSettings : ScriptableObject
{
    public string androidAppId = "";
    public string bannerAdUnitId = "";
    public string interstitialAdUnitId = "";
    public string rewardedAdUnitId = "";

    [Tooltip("Mostra um intersticial a cada N partidas encerradas (0 = nunca).")]
    public int interstitialEveryNGameOvers = 3;

    [Tooltip("Usa os anúncios de teste do Google. Builds de desenvolvimento (Development Build) sempre usam teste.")]
    public bool forceTestAds = false;

    [Tooltip("IDs de aparelhos de teste (aparecem no logcat como 'Use RequestConfiguration...setTestDeviceIds').")]
    public List<string> testDeviceIds = new List<string>();

    // Blocos de teste oficiais do Google para Android: nunca geram receita nem risco de suspensão.
    public const string TestBanner = "ca-app-pub-3940256099942544/6300978111";
    public const string TestInterstitial = "ca-app-pub-3940256099942544/1033173712";
    public const string TestRewarded = "ca-app-pub-3940256099942544/5224354917";

    public bool UseTestAds => forceTestAds || Debug.isDebugBuild;
    public string Banner => UseTestAds ? TestBanner : bannerAdUnitId;
    public string Interstitial => UseTestAds ? TestInterstitial : interstitialAdUnitId;
    public string Rewarded => UseTestAds ? TestRewarded : rewardedAdUnitId;
}
