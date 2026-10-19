using UnityEngine;

/// <summary>
/// IDs do LevelPlay (ironSource). Preencha pelo painel do LevelPlay:
/// https://platform.ironsrc.com → Apps → seu app → Ad Units.
/// Fica em Resources/AdsSettings.asset.
/// </summary>
[CreateAssetMenu(menuName = "Endless Runner/Ads Settings")]
public class AdsSettings : ScriptableObject
{
    public string androidAppKey = "";
    public string bannerAdUnitId = "";
    public string interstitialAdUnitId = "";
    public string rewardedAdUnitId = "";

    [Tooltip("Mostra um intersticial a cada N partidas encerradas (0 = nunca).")]
    public int interstitialEveryNGameOvers = 3;

    [Tooltip("Liga o validador de integração do LevelPlay no log. Desligue antes de publicar.")]
    public bool debugValidateIntegration = false;
}
