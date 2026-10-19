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
            case 0: // Garagem
                if (StepTime < 1f) return;
                var select = Object.FindFirstObjectByType<SelectCars>();
                if (select == null) { Fail("SelectCars não encontrado na garagem"); Finish(); return; }
                var catalog = CarCatalog.Load();
                Log($"Catálogo: {catalog.Count} carros");
                if (catalog.Count == 0) Fail("Catálogo vazio");
                BeginCapture("01_garage");
                Next();
                break;

            case 1:
                Object.FindFirstObjectByType<SelectCars>().NextCar();
                BeginCapture("02_garage_locked");
                Next();
                break;

            case 2:
                var s = Object.FindFirstObjectByType<SelectCars>();
                s.PreviousCar();
                s.OnActionButton(); // carro 0 é grátis → carrega a cena Game
                Next();
                break;

            case 3: // Tela inicial da corrida
                if (gm == null || StepTime < 3f) return;
                if (gm.State != GameState.Ready) Fail("Corrida deveria começar em Ready");
                BeginCapture("03_start");
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
                    BeginCapture("04_unexpected_crash");
                    step = 7; stepStart = Time.time;
                    return;
                }
                if (!earlyShot && StepTime > 4f) { earlyShot = true; BeginCapture("04_run_early"); return; }
                if (StepTime < 45f) return;
                Log($"45s de corrida: score {gm.Score} m, moedas {gm.RunCoins}, velocidade {gm.Speed:F1} m/s, fileiras ativas {gm.traffic.Rows.Count}");
                if (gm.Score < 500) Fail("Score baixo demais depois de 45s");
                if (gm.RunCoins == 0) Fail("Nenhuma moeda coletada em 45s");
                BeginCapture("05_run_fast");
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
                if (SaveData.Coins != gm.RunCoins) Fail("Moedas da corrida não foram guardadas");
                BeginCapture("06_gameover");
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
                BeginCapture("07_revived");
                Next();
                break;

            case 10:
                if (StepTime < 5f) return;
                if (gm.State != GameState.Running) Fail("Bateu nos 5s seguintes ao continuar");
                gm.Pause();
                if (gm.State != GameState.Paused || Time.timeScale != 0f) Fail("Pausa não parou o tempo");
                BeginCapture("08_pause");
                Next();
                break;

            case 11:
                gm.Resume();
                if (gm.State != GameState.Running) Fail("Resume falhou");
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
        cam.targetTexture = RenderTexture.GetTemporary(540, 960, 24);
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

    static void Finish()
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(ActiveKey, false);
        Log(failures.Count == 0 ? "RESULTADO: PASSOU" : $"RESULTADO: FALHOU ({failures.Count})");
        File.WriteAllText(Path.Combine(OutDir, "report.txt"), report.ToString());
        PlayerPrefs.DeleteAll();
        EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
    }
}
