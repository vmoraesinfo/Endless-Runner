using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum GameState { Ready, Running, Paused, GameOver }

/// <summary>
/// Controla a partida: velocidade, pontos, combo de raspões, nitro, power-ups, moedas, batida,
/// continuar (revive), pausa, missões e troca de cena.
///
/// Mecânica central: trocar de faixa no último instante antes de passar por um carro é um RASPÃO.
/// Raspões em sequência aumentam o multiplicador de pontos e enchem o nitro. Com o nitro cheio,
/// swipe para cima: o carro acelera, fica invencível e arremessa o trânsito para longe.
/// </summary>
[DefaultExecutionOrder(-50)] // configura a pista antes do Awake/Start da estrada, trânsito e biomas
public class GameManager : MonoBehaviour
{
    public const string GarageScene = "CarChose";

    public static GameManager Instance { get; private set; }

    [Header("Referências")]
    public PlayerController player;
    public TrafficSpawner traffic;
    public CameraFollow cameraFollow;
    public BiomeManager biomes;
    public TrackGenerator track;
    public GameUI ui;
    public ParticleSystem crashEffect;
    public ParticleSystem coinEffect;

    [Header("Velocidade (m/s)")]
    public float startSpeed = 20f;
    public float maxSpeed = 50f;
    [Tooltip("Quanto a velocidade sobe por segundo.")]
    public float acceleration = 0.45f;

    [Header("Raspão e combo")]
    [Tooltip("Troca de faixa até este tempo antes de passar pelo carro conta como raspão.")]
    public float nearMissWindow = 0.6f;
    public float perfectWindow = 0.3f;
    [Tooltip("Tempo sem raspão até o combo zerar.")]
    public float comboWindow = 6f;
    public int maxComboSteps = 8;
    public int nearMissPoints = 50;

    [Header("Nitro")]
    public float nitroDuration = 4f;
    public float nitroSpeedFactor = 1.6f;
    public float nitroPerNearMiss = 0.2f;
    public float nitroPerPerfect = 0.34f;
    public float nitroPerCoin = 0.015f;
    public int smashPoints = 100;

    [Header("Power-ups")]
    public float magnetDuration = 8f;
    public float magnetRange = 22f;

    [Header("Continuar")]
    public float invulnerableAfterRevive = 2f;
    public float clearDistanceOnRevive = 60f;

    public GameState State { get; private set; }
    /// <summary>Velocidade base (sem nitro); define a dificuldade.</summary>
    public float BaseSpeed { get; private set; }
    public float Speed => BaseSpeed * (NitroActive ? nitroSpeedFactor : 1f);
    public float Difficulty => Mathf.InverseLerp(startSpeed, maxSpeed, BaseSpeed);
    public float Distance => player.transform.position.z - startZ;
    public int Score => Mathf.FloorToInt(points);
    public int RunCoins { get; private set; }
    public int Combo { get; private set; }
    public float Multiplier => 1f + Mathf.Min(Combo, maxComboSteps) * 0.5f;
    public int NearMisses { get; private set; }
    public float BestMultiplier { get; private set; } = 1f;
    public float Nitro01 { get; private set; }
    public bool NitroActive { get; private set; }
    public int NitrosUsed { get; private set; }
    /// <summary>Batidas que o escudo ainda aguenta (melhoria "Escudo" aumenta).</summary>
    public int ShieldCharges { get; private set; }
    public bool ShieldActive => ShieldCharges > 0;
    public float MagnetTime { get; private set; }

    float points;
    float startZ;
    float speedBeforeCrash;
    float invulnerableUntil;
    float comboTimer;
    float nitroTimer;
    bool revived;
    bool doubled;
    bool lastNewBest;
    int bankedCoins;
    int coinStreak;
    // Progresso já enviado para as missões (para mandar só a diferença).
    int reportedCoins, reportedNitros;
    bool reportedRun;

    void Awake()
    {
        Instance = this;
        ApplyMap();
        Time.timeScale = 1f;
        Application.targetFrameRate = 60;
    }

    /// <summary>Pista desta corrida (null se o catálogo de pistas não existir).</summary>
    public MapCatalog.Map Map { get; private set; }

    /// <summary>Troca cenário, trânsito e paleta conforme a pista escolhida na garagem.</summary>
    void ApplyMap()
    {
        var catalog = MapCatalog.Load();
        if (catalog == null || catalog.Count == 0) return;
        Map = catalog.Get(catalog.SelectedIndex);
        if (track != null && Map.segmentPrefab != null) track.segmentPrefab = Map.segmentPrefab;
        if (traffic != null) traffic.Configure(Map);
        if (biomes != null && Map.biomes != null && Map.biomes.Length > 0) biomes.biomes = Map.biomes;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Sfx.StopEngine();
    }

    void Start()
    {
        var catalog = CarCatalog.Load();
        var entry = catalog.Get(SaveData.SelectedCar);
        player.SetCar(entry.prefab);
        CarPaint.Apply(player.Visual.gameObject, Paints.Selected(SaveData.SelectedCar));
        // Melhorias compradas na garagem.
        nitroDuration = Upgrades.NitroDuration(Upgrades.Level(UpgradeType.Nitro));
        magnetDuration = Upgrades.MagnetDuration(Upgrades.Level(UpgradeType.Magnet));
        player.transform.position = new Vector3(player.LaneX(1), 0f, 0f);
        startZ = player.transform.position.z;
        player.BoostRequested += TryNitro;
        traffic.RowPassed += OnRowPassed;
        if (biomes != null) biomes.BiomeChanged += name => ui.Popup(name, "", Color.white, 1.4f);

        State = GameState.Ready;
        player.InputEnabled = false;
        player.SetShield(false);
        player.SetNitro(false);
        ui.ShowStart(SaveData.BestScore, !SaveData.TutorialDone);
        ui.UpdateHud(0, 0);
        ui.UpdateCombo(1f, 0f);
        ui.UpdateNitro(0f, false, false);
        ui.UpdatePowerUps(0f, 0);
        AdsManager.Instance.HideBanner();
        Sfx.ApplyVolume();
        Music.PlayRace();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            if (State == GameState.Running) Pause();
            else if (State == GameState.Paused) Resume();
        }
        if (State == GameState.Ready && kb != null && kb.spaceKey.wasPressedThisFrame) StartRun();

        Sfx.I.Engine(State == GameState.Running, Mathf.InverseLerp(startSpeed, maxSpeed * nitroSpeedFactor, Speed));
        if (State != GameState.Running) return;

        float dt = Time.deltaTime;
        BaseSpeed = Mathf.Min(maxSpeed, BaseSpeed + acceleration * dt);
        float step = Speed * dt;
        player.MoveForward(step);
        points += step * Multiplier;
        traffic.Tick(dt, Speed, Difficulty);
        cameraFollow.SetSpeed01(Difficulty);
        if (biomes != null) biomes.UpdateDistance(Distance);

        UpdateCombo(dt);
        UpdateNitro(dt);
        UpdateMagnet(dt);
        ui.UpdateHud(Score, RunCoins);
    }

    public void StartRun()
    {
        if (State != GameState.Ready) return;
        SaveData.TutorialDone = true;
        State = GameState.Running;
        BaseSpeed = startSpeed;
        player.InputEnabled = true;
        ui.ShowHud();
        Sfx.I.Click();
    }

    // ---------- Raspão e combo ----------

    void OnRowPassed(TrafficSpawner.Row row, float minDx)
    {
        if (State != GameState.Running || NitroActive) return;
        // Carro na faixa vizinha + troca de faixa bem em cima da hora.
        if (minDx > player.laneWidth * 1.3f) return;
        float sinceChange = Time.time - player.LastLaneChangeTime;
        if (sinceChange > nearMissWindow) return;
        NearMiss(sinceChange <= perfectWindow);
    }

    void NearMiss(bool perfect)
    {
        Combo++;
        NearMisses++;
        comboTimer = comboWindow;
        BestMultiplier = Mathf.Max(BestMultiplier, Multiplier);
        int bonus = Mathf.RoundToInt((perfect ? nearMissPoints * 2 : nearMissPoints) * Multiplier);
        points += bonus;

        bool wasReady = Nitro01 >= 1f;
        Nitro01 = Mathf.Min(1f, Nitro01 + (perfect ? nitroPerPerfect : nitroPerNearMiss));
        if (!wasReady && Nitro01 >= 1f) ui.Popup("NITRO PRONTO!", "deslize para cima", new Color(0.4f, 0.85f, 1f), 1.2f);

        ui.Popup(perfect ? "RASPÃO PERFEITO!" : "RASPÃO!", $"+{bonus}   x{Multiplier:0.#}", perfect ? new Color(1f, 0.85f, 0.2f) : Color.white, 0.9f);
        cameraFollow.Kick(perfect ? 7f : 4f);
        Sfx.I.NearMiss(Combo);
        if (perfect) Sfx.I.Perfect();
    }

    void UpdateCombo(float dt)
    {
        if (Combo > 0)
        {
            comboTimer -= dt;
            if (comboTimer <= 0f) Combo = 0;
        }
        ui.UpdateCombo(Multiplier, Combo > 0 ? comboTimer / comboWindow : 0f);
    }

    // ---------- Nitro ----------

    public void TryNitro()
    {
        if (State != GameState.Running || NitroActive || Nitro01 < 1f) return;
        NitroActive = true;
        NitrosUsed++;
        nitroTimer = nitroDuration;
        player.SetNitro(true);
        cameraFollow.SetBoost(true);
        Sfx.I.Nitro();
        ui.Popup("NITRO!", "", new Color(0.4f, 0.85f, 1f), 0.8f);
    }

    void UpdateNitro(float dt)
    {
        if (NitroActive)
        {
            nitroTimer -= dt;
            Nitro01 = Mathf.Max(0f, nitroTimer / nitroDuration);
            if (nitroTimer <= 0f) EndNitro();
        }
        ui.UpdateNitro(Nitro01, !NitroActive && Nitro01 >= 1f, NitroActive);
    }

    void EndNitro()
    {
        NitroActive = false;
        Nitro01 = 0f;
        player.SetNitro(false);
        cameraFollow.SetBoost(false);
        // Pequena carência para não bater no carro que estava logo à frente.
        invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + 0.8f);
    }

    // ---------- Moedas e power-ups ----------

    public void CollectCoin(Coin coin)
    {
        if (State != GameState.Running) return;
        RunCoins += Map != null ? Mathf.Max(1, Map.coinValue) : 1;
        coinStreak++;
        if (!NitroActive) Nitro01 = Mathf.Min(1f, Nitro01 + nitroPerCoin);
        if (coinEffect != null)
        {
            coinEffect.transform.position = coin.transform.position;
            coinEffect.Emit(8);
        }
        traffic.ReturnCoin(coin.gameObject);
        ui.PunchCoins();
        Sfx.I.Coin(coinStreak % 12);
    }

    public void CollectPickup(Pickup pickup)
    {
        if (State != GameState.Running) return;
        switch (pickup.type)
        {
            case PickupType.Magnet:
                MagnetTime = magnetDuration;
                ui.Popup("ÍMÃ!", "puxa as moedas", new Color(1f, 0.45f, 0.4f), 0.9f);
                break;
            case PickupType.Shield:
                ShieldCharges = Upgrades.ShieldCharges(Upgrades.Level(UpgradeType.Shield));
                player.SetShield(true);
                ui.Popup("ESCUDO!", ShieldCharges == 1 ? "aguenta uma batida" : $"aguenta {ShieldCharges} batidas", new Color(0.45f, 0.75f, 1f), 0.9f);
                break;
        }
        traffic.ReturnPickup(pickup);
        Sfx.I.PowerUp();
    }

    void UpdateMagnet(float dt)
    {
        if (MagnetTime > 0f)
        {
            MagnetTime -= dt;
            Vector3 target = player.transform.position + Vector3.up;
            float pull = (Speed + 30f) * dt;
            foreach (var coin in traffic.ActiveCoins())
            {
                float dz = coin.transform.position.z - target.z;
                if (dz < -3f || dz > magnetRange) continue;
                coin.transform.position = Vector3.MoveTowards(coin.transform.position, target, pull);
            }
        }
        ui.UpdatePowerUps(Mathf.Max(0f, MagnetTime), ShieldCharges);
    }

    // ---------- Batida ----------

    public void OnPlayerHit(Obstacle obstacle)
    {
        if (State != GameState.Running) return;

        if (NitroActive)
        {
            Smash(obstacle, "BOOM!");
            return;
        }
        if (Time.time < invulnerableUntil) return;
        if (ShieldActive)
        {
            ShieldCharges--;
            player.SetShield(ShieldActive);
            invulnerableUntil = Time.time + 1f;
            Smash(obstacle, "ESCUDO SALVOU!");
            return;
        }

        Crash();
    }

    void Smash(Obstacle obstacle, string label)
    {
        int bonus = Mathf.RoundToInt(smashPoints * Multiplier);
        points += bonus;
        traffic.Knock(obstacle.gameObject, Speed);
        cameraFollow.Shake(0.2f, 0.25f);
        Sfx.I.Smash();
        ui.Popup(label, $"+{bonus}", new Color(1f, 0.55f, 0.2f), 0.7f);
        if (crashEffect != null)
        {
            crashEffect.transform.position = obstacle.transform.position + Vector3.up;
            crashEffect.Play();
        }
    }

    void Crash()
    {
        State = GameState.GameOver;
        speedBeforeCrash = BaseSpeed;
        BaseSpeed = 0f;
        Combo = 0;
        MagnetTime = 0f;
        player.Crash();
        cameraFollow.Shake(0.35f, 0.35f);
        Haptics.Crash();
        Sfx.I.Crash();
        if (crashEffect != null)
        {
            crashEffect.transform.position = player.transform.position + Vector3.up + Vector3.forward * 2f;
            crashEffect.Play();
        }

        int best = SaveData.BestScore;
        bool newBest = Score > best;
        if (Map != null) SaveData.ReportDistance(Map.id, Mathf.FloorToInt(Distance));
        if (newBest) SaveData.BestScore = Score;
        BankCoins();
        ReportProgress();
        lastNewBest = newBest;
        ShowGameOver(newBest, Mathf.Max(best, Score));
    }

    void ShowGameOver(bool newBest, int best)
    {
        ui.ShowGameOver(new GameUI.GameOverInfo
        {
            score = Score,
            best = best,
            newBest = newBest,
            distance = Mathf.FloorToInt(Distance),
            coins = RunCoins,
            nearMisses = NearMisses,
            bestMultiplier = BestMultiplier,
            canRevive = !revived && AdsManager.Instance.IsRewardedReady,
            canDouble = !doubled && RunCoins >= 10 && AdsManager.Instance.IsRewardedReady,
            missionsReady = Missions.AnyClaimable,
        });
    }

    // ---------- Botões ----------

    public void Revive()
    {
        if (State != GameState.GameOver || revived) return;
        AdsManager.Instance.ShowRewarded(DoRevive);
    }

    void DoRevive()
    {
        revived = true;
        traffic.ClearUntil(player.transform.position.z, clearDistanceOnRevive);
        BaseSpeed = Mathf.Max(startSpeed, speedBeforeCrash * 0.85f);
        invulnerableUntil = Time.time + invulnerableAfterRevive;
        player.Revive(invulnerableAfterRevive);
        State = GameState.Running;
        ui.ShowHud();
    }

    public void DoubleCoins()
    {
        if (State != GameState.GameOver || doubled || RunCoins == 0) return;
        AdsManager.Instance.ShowRewarded(() =>
        {
            doubled = true;
            RunCoins *= 2;
            BankCoins();
            ShowGameOver(lastNewBest, SaveData.BestScore);
            Sfx.I.PowerUp();
        });
    }

    public void Pause()
    {
        if (State != GameState.Running) return;
        State = GameState.Paused;
        Time.timeScale = 0f;
        ui.ShowPause(SaveData.Vibration, SaveData.Sound);
    }

    public void Resume()
    {
        if (State != GameState.Paused) return;
        State = GameState.Running;
        Time.timeScale = 1f;
        ui.ShowHud();
    }

    public void ToggleVibration()
    {
        SaveData.Vibration = !SaveData.Vibration;
        ui.ShowPause(SaveData.Vibration, SaveData.Sound);
    }

    public void ShowPrivacyOptions() => AdsManager.Instance.ShowPrivacyOptions();

    public void ToggleSound()
    {
        SaveData.Sound = !SaveData.Sound;
        Sfx.ApplyVolume();
        ui.ShowPause(SaveData.Vibration, SaveData.Sound);
    }

    public void Restart()
    {
        LeaveTo(SceneManager.GetActiveScene().name);
    }

    public void GoToGarage()
    {
        LeaveTo(GarageScene);
    }

    void LeaveTo(string scene)
    {
        Time.timeScale = 1f;
        bool fromGameOver = State == GameState.GameOver;
        BankCoins();
        if (State != GameState.Ready) ReportProgress();
        if (fromGameOver) AdsManager.Instance.OnGameOverThen(() => SceneManager.LoadScene(scene));
        else SceneManager.LoadScene(scene);
    }

    /// <summary>Guarda no save as moedas desta corrida que ainda não foram guardadas.</summary>
    void BankCoins()
    {
        if (RunCoins == bankedCoins) return;
        SaveData.Coins += RunCoins - bankedCoins;
        bankedCoins = RunCoins;
    }

    /// <summary>Envia para as missões só o que mudou desde o último envio.</summary>
    void ReportProgress()
    {
        Missions.Report(
            coinsDelta: RunCoins - reportedCoins,
            nitroDelta: NitrosUsed - reportedNitros,
            runsDelta: reportedRun ? 0 : 1,
            nearMissInRun: NearMisses,
            distanceInRun: Mathf.FloorToInt(Distance),
            comboInRun: Mathf.FloorToInt(BestMultiplier));
        reportedCoins = RunCoins;
        reportedNitros = NitrosUsed;
        reportedRun = true;
    }

    void OnApplicationPause(bool paused)
    {
        if (paused && State == GameState.Running) Pause();
        if (paused)
        {
            BankCoins();
            if (State != GameState.Ready) ReportProgress();
        }
    }

    void OnApplicationQuit()
    {
        BankCoins();
    }
}
