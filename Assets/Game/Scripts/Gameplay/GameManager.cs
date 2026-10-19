using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum GameState { Ready, Running, Paused, GameOver }

/// <summary>
/// Controla a partida: velocidade, pontuação, moedas, batida, continuar (revive), pausa e troca de cena.
/// </summary>
public class GameManager : MonoBehaviour
{
    public const string GarageScene = "CarChose";

    public static GameManager Instance { get; private set; }

    [Header("Referências")]
    public PlayerController player;
    public TrafficSpawner traffic;
    public CameraFollow cameraFollow;
    public GameUI ui;
    public ParticleSystem crashEffect;
    public ParticleSystem coinEffect;

    [Header("Velocidade (m/s)")]
    public float startSpeed = 20f;
    public float maxSpeed = 50f;
    [Tooltip("Quanto a velocidade sobe por segundo.")]
    public float acceleration = 0.45f;

    [Header("Continuar")]
    public float invulnerableAfterRevive = 2f;
    public float clearDistanceOnRevive = 60f;

    public GameState State { get; private set; }
    public float Speed { get; private set; }
    public float Difficulty => Mathf.InverseLerp(startSpeed, maxSpeed, Speed);
    public int Score => Mathf.FloorToInt(player.transform.position.z - startZ);
    public int RunCoins { get; private set; }

    float startZ;
    float speedBeforeCrash;
    float invulnerableUntil;
    bool revived;
    int bankedCoins;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        Application.targetFrameRate = 60;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        var catalog = CarCatalog.Load();
        var entry = catalog.Get(SaveData.SelectedCar);
        player.SetCar(entry.prefab);
        player.transform.position = new Vector3(player.LaneX(1), 0f, 0f);
        startZ = player.transform.position.z;

        State = GameState.Ready;
        player.InputEnabled = false;
        ui.ShowStart(SaveData.BestScore, !SaveData.TutorialDone);
        ui.UpdateHud(0, 0);
        AdsManager.Instance.HideBanner();
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

        if (State != GameState.Running) return;

        float dt = Time.deltaTime;
        Speed = Mathf.Min(maxSpeed, Speed + acceleration * dt);
        player.MoveForward(Speed * dt);
        traffic.Tick(dt, Speed, Difficulty);
        cameraFollow.SetSpeed01(Difficulty);
        ui.UpdateHud(Score, RunCoins);
    }

    public void StartRun()
    {
        if (State != GameState.Ready) return;
        SaveData.TutorialDone = true;
        State = GameState.Running;
        Speed = startSpeed;
        player.InputEnabled = true;
        ui.ShowHud();
    }

    public void CollectCoin(Coin coin)
    {
        if (State != GameState.Running) return;
        RunCoins++;
        if (coinEffect != null)
        {
            coinEffect.transform.position = coin.transform.position;
            coinEffect.Emit(8);
        }
        traffic.ReturnCoin(coin.gameObject);
        ui.PunchCoins();
    }

    public void OnPlayerHit()
    {
        if (State != GameState.Running || Time.time < invulnerableUntil) return;

        State = GameState.GameOver;
        speedBeforeCrash = Speed;
        Speed = 0f;
        player.Crash();
        cameraFollow.Shake(0.35f, 0.35f);
        Haptics.Crash();
        if (crashEffect != null)
        {
            crashEffect.transform.position = player.transform.position + Vector3.up + Vector3.forward * 2f;
            crashEffect.Play();
        }

        int best = SaveData.BestScore;
        bool newBest = Score > best;
        if (newBest) SaveData.BestScore = Score;
        BankCoins();
        bool canRevive = !revived && AdsManager.Instance.IsRewardedReady;
        ui.ShowGameOver(Score, Mathf.Max(best, Score), newBest, RunCoins, canRevive);
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
        Speed = Mathf.Max(startSpeed, speedBeforeCrash * 0.85f);
        invulnerableUntil = Time.time + invulnerableAfterRevive;
        player.Revive(invulnerableAfterRevive);
        State = GameState.Running;
        ui.ShowHud();
    }

    public void Pause()
    {
        if (State != GameState.Running) return;
        State = GameState.Paused;
        Time.timeScale = 0f;
        ui.ShowPause(SaveData.Vibration);
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
        ui.ShowPause(SaveData.Vibration);
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

    void OnApplicationPause(bool paused)
    {
        if (paused && State == GameState.Running) Pause();
        if (paused) BankCoins();
    }

    void OnApplicationQuit()
    {
        BankCoins();
    }
}
