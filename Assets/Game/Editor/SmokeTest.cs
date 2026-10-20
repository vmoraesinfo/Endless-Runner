using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Teste de fumaça de ponta a ponta, rodado pela linha de comando:
///   Unity.exe -batchmode -projectPath . -executeMethod SmokeTest.Run -logFile Logs/smoke.log
/// Abre a garagem, entra na corrida, joga com o AutoPilot, força uma batida, testa continuar e pausa,
/// salva screenshots em Logs/smoke/ e encerra a Unity com código 0 (passou) ou 1 (falhou).
/// </summary>
[InitializeOnLoad]
public static class SmokeTest
{
    const string ActiveKey = "SmokeTest.Active";
    static readonly string OutDir = Path.GetFullPath("Logs/smoke");

    static int step;
    static float stepStart;
    static float realStart;
    static readonly List<string> failures = new List<string>();
    static readonly StringBuilder report = new StringBuilder();
    static Dictionary<Canvas, RenderMode> pendingCapture;
    static string pendingCaptureName;
    static AutoPilot pilot;
    static bool earlyShot;
    static bool garageLockedShot;
    static bool nitroShot;
    static bool cityShot;
    static int tourStep;
    static int shopStep;
    static int coinsBeforeRun;
    static readonly int[] TourCars = { 6, 10, 14, 15, 18, 22 };

    static int CurrentCarIndex(SelectCars s) =>
        (int)typeof(SelectCars).GetField("carIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(s);
    static Camera captureCam;

    static SmokeTest()
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    public static void Run()
    {
        PlayerPrefs.DeleteAll();
        SessionState.SetBool(ActiveKey, true);
        Directory.CreateDirectory(OutDir);
        EditorSceneManager.OpenScene("Assets/Scenes/CarChose.unity");
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.EnterPlaymode();
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode) return;
        Time.captureFramerate = 30;
        step = 0;
        stepStart = Time.time;
        realStart = (float)EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
    }

    static void Log(string msg)
    {
        Debug.Log("[SmokeTest] " + msg);
        report.AppendLine(msg);
    }

    static void Fail(string msg)
    {
        failures.Add(msg);
        Log("FALHA: " + msg);
    }

    static void Next()
    {
        step++;
        stepStart = Time.time;
    }

    static float StepTime => Time.time - stepStart;

    static void Tick()
    {
        if (pendingCapture != null)
        {
            FinishCapture();
            return;
        }

        if (EditorApplication.timeSinceStartup - realStart > 600)
        {
            Fail("timeout de 10 minutos (tempo real)");
            Finish();
            return;
        }

        var gm = GameManager.Instance;
        switch (step)
        {
            case 0: // Garagem: recompensa diária aparece na primeira visita
                if (StepTime < 1f) return;
                var select = Object.FindFirstObjectByType<SelectCars>();
                if (select == null) { Fail("SelectCars não encontrado na garagem"); Finish(); return; }
                var catalog = CarCatalog.Load();
                Log($"Catálogo: {catalog.Count} carros");
                if (catalog.Count == 0) Fail("Catálogo vazio");
                var menus = Object.FindFirstObjectByType<GarageMenus>();
                if (menus == null) { Fail("GarageMenus não encontrado"); Finish(); return; }
                if (!menus.dailyPanel.activeSelf) Fail("Recompensa diária não apareceu na primeira visita");
                BeginCapture("01_daily");
                Next();
                break;

            case 1: // Resgata a diária e abre as missões
                var m1 = Object.FindFirstObjectByType<GarageMenus>();
                m1.ClaimDaily();
                if (SaveData.Coins != 50) Fail($"Recompensa diária deveria dar 50 moedas (deu {SaveData.Coins})");
                if (DailyReward.Available) Fail("Recompensa diária continua disponível depois de resgatar");
                m1.OpenMissions();
                if (!m1.missionsPanel.activeSelf) Fail("Painel de missões não abriu");
                Log("Missões: " + string.Join(" | ", System.Linq.Enumerable.Select(Missions.Active, x => Missions.Describe(x))));
                BeginCapture("02_missions");
                Next();
                break;

            case 2:
                var s = Object.FindFirstObjectByType<SelectCars>();
                if (!garageLockedShot)
                {
                    garageLockedShot = true;
                    Object.FindFirstObjectByType<GarageMenus>().CloseMissions();
                    s.NextCar();
                    BeginCapture("03_garage_locked");
                    return;
                }
                // Passeio pela garagem: um carro de cada pack novo (confere tamanho e orientação).
                if (tourStep < TourCars.Length)
                {
                    int target = TourCars[tourStep];
                    while (CurrentCarIndex(s) != target) s.NextCar();
                    BeginCapture($"03_garage_car{target:00}");
                    tourStep++;
                    return;
                }
                while (CurrentCarIndex(s) != 0) s.NextCar();

                // Loja: melhorias e pintura.
                var shop = Object.FindFirstObjectByType<GarageMenus>();
                if (shopStep == 0)
                {
                    SaveData.Coins += 5000;
                    shop.OpenUpgrades();
                    shop.BuyNitro();
                    shop.BuyShield();
                    shop.BuyShield();
                    if (Upgrades.Level(UpgradeType.Nitro) != 1) Fail("Compra da melhoria de nitro falhou");
                    if (Upgrades.Level(UpgradeType.Shield) != 2) Fail("Compra da melhoria de escudo falhou");
                    if (SaveData.Coins != 50 + 5000 - 200 - 200 - 500) Fail($"Preço das melhorias errado (moedas {SaveData.Coins})");
                    BeginCapture("03b_upgrades");
                    shopStep = 1;
                    return;
                }
                if (shopStep == 1)
                {
                    shop.CloseUpgrades();
                    var picker = Object.FindFirstObjectByType<PaintPicker>();
                    picker.Pick(2); // azul
                    if (Paints.Selected(0) != 2) Fail("Pintura comprada não ficou selecionada");
                    BeginCapture("03c_paint_blue");
                    shopStep = 2;
                    return;
                }
                if (shopStep == 2)
                {
                    // Um carro de outro pack (paleta em textura) para conferir a recoloração.
                    while (CurrentCarIndex(s) != 6) s.NextCar();
                    SaveData.UnlockCar(6);
                    s.RefreshUI();
                    Object.FindFirstObjectByType<PaintPicker>().Refresh();
                    Object.FindFirstObjectByType<PaintPicker>().Pick(1); // vermelho
                    BeginCapture("03d_paint_red_hatch");
                    shopStep = 3;
                    return;
                }
                while (CurrentCarIndex(s) != 0) s.NextCar();
                coinsBeforeRun = SaveData.Coins;
                s.OnActionButton(); // carro 0 é grátis → carrega a cena Game
                Next();
                break;

            case 3: // Tela inicial da corrida
                if (gm == null || StepTime < 3f) return;
                if (gm.State != GameState.Ready) Fail("Corrida deveria começar em Ready");
                if (!Mathf.Approximately(gm.nitroDuration, Upgrades.NitroDuration(1))) Fail($"Melhoria de nitro não aplicada (duração {gm.nitroDuration})");
                BeginCapture("04_start");
                Next();
                break;

            case 4:
                pilot = gm.gameObject.AddComponent<AutoPilot>();
                pilot.player = gm.player;
                pilot.traffic = gm.traffic;
                gm.StartRun();
                if (gm.State != GameState.Running) Fail("StartRun não iniciou a corrida");
                Next();
                break;

            case 5: // Joga 45 s com piloto automático perfeito: não pode bater se o trânsito for justo.
                if (gm.State == GameState.GameOver)
                {
                    Fail($"AutoPilot bateu aos {StepTime:F1}s (score {gm.Score}) — padrão de trânsito pode ser impossível");
                    BeginCapture("05_unexpected_crash");
                    step = 7; stepStart = Time.time;
                    return;
                }
                if (!earlyShot && StepTime > 4f) { earlyShot = true; BeginCapture("05_run_early"); return; }
                if (!nitroShot && gm.NitroActive) { nitroShot = true; BeginCapture("06_nitro"); return; }
                if (StepTime < 45f) return;
                Log($"45s de corrida: {gm.Score} pts, {gm.Distance:F0} m, moedas {gm.RunCoins}, raspões {gm.NearMisses}, nitros {gm.NitrosUsed}, combo máx x{gm.BestMultiplier:0.#}, velocidade {gm.Speed:F1} m/s");
                if (gm.NearMisses == 0) Fail("Nenhum raspão em 45s desviando em cima da hora");
                if (gm.NitrosUsed == 0) Fail("Nitro nunca foi usado em 45s");
                if (gm.Score < 500) Fail("Score baixo demais depois de 45s");
                if (gm.RunCoins == 0) Fail("Nenhuma moeda coletada em 45s");
                BeginCapture("07_run_fast");
                Next();
                break;

            case 6: // Força a batida
                pilot.mistakeChance = 1f;
                Next();
                break;

            case 7:
                if (gm.State != GameState.GameOver)
                {
                    if (StepTime > 30f) { Fail("Não bateu nem errando de propósito"); Finish(); }
                    return;
                }
                if (StepTime < 0.8f) return; // espera a animação da batida
                Log($"Bateu: score {gm.Score}, recorde salvo {SaveData.BestScore}, moedas salvas {SaveData.Coins}");
                if (SaveData.BestScore != gm.Score) Fail("Recorde não foi salvo");
                if (SaveData.Coins != coinsBeforeRun + gm.RunCoins) Fail("Moedas da corrida não foram guardadas");
                bool anyProgress = false;
                foreach (var mi in Missions.Active) if (mi.progress > 0) anyProgress = true;
                if (!anyProgress) Fail("Nenhuma missão recebeu progresso");
                BeginCapture("08_gameover");
                Next();
                break;

            case 8: // Continuar (no editor o anúncio é simulado)
                pilot.mistakeChance = 0f;
                gm.Revive();
                if (gm.State != GameState.Running) Fail("Revive não voltou para Running");
                Next();
                break;

            case 9:
                if (StepTime < 1f) return;
                if (gm.State != GameState.Running) Fail("Bateu logo depois de continuar (invulnerabilidade/limpeza)");
                BeginCapture("09_revived");
                Next();
                break;

            case 10:
                if (StepTime < 5f) return;
                if (gm.State != GameState.Running) Fail("Bateu nos 5s seguintes ao continuar");
                gm.Pause();
                if (gm.State != GameState.Paused || Time.timeScale != 0f) Fail("Pausa não parou o tempo");
                BeginCapture("10_pause");
                Next();
                break;

            case 11:
                gm.Resume();
                if (gm.State != GameState.Running) Fail("Resume falhou");
                Log($"Rodovia: {gm.traffic.LaneChangesStarted} carros trocaram de faixa (pisca), distância salva {SaveData.BestDistance("rodovia")} m");
                if (gm.traffic.LaneChangesStarted == 0) Fail("Nenhum carro trocou de faixa na Rodovia");
                // Libera a Cidade e recarrega a corrida nela.
                var maps = MapCatalog.Load();
                if (maps == null || maps.Count < 2) { Fail("MapCatalog sem a pista Cidade"); Finish(); return; }
                if (maps.IsUnlocked(1) != (SaveData.BestDistance("rodovia") >= maps.Get(1).unlockDistance)) Fail("Regra de desbloqueio da Cidade inconsistente");
                SaveData.ReportDistance("rodovia", 99999);
                SaveData.SelectedMap = 1;
                UnityEngine.SceneManagement.SceneManager.LoadScene("Game");
                Next();
                break;

            case 12: // Cidade: tela inicial
                if (gm == null || gm.Map == null || gm.Map.id != "cidade" || StepTime < 3f) return;
                BeginCapture("11_city_start");
                Next();
                break;

            case 13:
                pilot = gm.gameObject.AddComponent<AutoPilot>();
                pilot.player = gm.player;
                pilot.traffic = gm.traffic;
                gm.StartRun();
                cityShot = false;
                Next();
                break;

            case 14: // 40 s na Cidade sem bater (obras + trânsito parado + pisca)
                if (gm.State == GameState.GameOver)
                {
                    Fail($"AutoPilot bateu na Cidade aos {StepTime:F1}s ({gm.Distance:F0} m)");
                    BeginCapture("13_city_crash");
                    step = 15;
                    return;
                }
                if (!cityShot && StepTime > 10f) { cityShot = true; BeginCapture("12_city_run"); return; }
                if (StepTime < 40f) return;
                Log($"Cidade 40s: {gm.Distance:F0} m, moedas {gm.RunCoins} (valem x{gm.Map.coinValue}), obras {gm.traffic.RoadworksSpawned}, piscas {gm.traffic.LaneChangesStarted}, raspões {gm.NearMisses}");
                if (gm.traffic.RoadworksSpawned == 0) Fail("Nenhuma obra apareceu na Cidade");
                BeginCapture("13_city_late");
                step = 15;
                break;

            case 15:
                Finish();
                break;
        }
    }

    // Overlay canvases não aparecem em camera.Render(); muda para ScreenSpaceCamera por um frame.
    static void BeginCapture(string name)
    {
        // A câmera que aparece por cima é a de maior depth (a garagem tem duas).
        Camera cam = null;
        foreach (var c in Camera.allCameras)
            if (cam == null || c.depth > cam.depth) cam = c;
        if (cam == null) { Fail("Sem câmera para screenshot " + name); return; }
        captureCam = cam;
        pendingCaptureName = name;
        pendingCapture = new Dictionary<Canvas, RenderMode>();
        // Modo loja (-storeShots): 1080x1920, o formato aceito pela Play Store. Normal: proporção de celular atual.
        cam.targetTexture = StoreMode ? RenderTexture.GetTemporary(1080, 1920, 24) : RenderTexture.GetTemporary(540, 1170, 24);
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas) continue;
            pendingCapture[c] = c.renderMode;
            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = cam;
            c.planeDistance = 1f;
        }
    }

    static void FinishCapture()
    {
        var cam = captureCam;
        var rt = cam.targetTexture;
        Canvas.ForceUpdateCanvases();
        cam.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(Path.Combine(OutDir, pendingCaptureName + ".png"), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        foreach (var kv in pendingCapture)
            if (kv.Key != null) kv.Key.renderMode = kv.Value;
        Log("screenshot " + pendingCaptureName);
        pendingCapture = null;
    }

    static bool StoreMode => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-storeShots") >= 0;

    // Telas escolhidas para a página da loja, na ordem em que aparecem lá.
    static readonly string[] StoreShots = { "06_nitro", "12_city_run", "03c_paint_blue", "03b_upgrades", "05_run_early", "08_gameover" };

    static void CopyStoreShots()
    {
        string dir = Path.GetFullPath("Store/screenshots");
        Directory.CreateDirectory(dir);
        for (int i = 0; i < StoreShots.Length; i++)
        {
            string src = Path.Combine(OutDir, StoreShots[i] + ".png");
            if (File.Exists(src)) File.Copy(src, Path.Combine(dir, $"{i + 1:00}_{StoreShots[i]}.png"), true);
        }
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(ActiveKey, false);
        Log(failures.Count == 0 ? "RESULTADO: PASSOU" : $"RESULTADO: FALHOU ({failures.Count})");
        File.WriteAllText(Path.Combine(OutDir, "report.txt"), report.ToString());
        if (StoreMode) CopyStoreShots();
        PlayerPrefs.DeleteAll();
        EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
    }
}
