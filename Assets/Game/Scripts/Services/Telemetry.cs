using System.Collections.Generic;
using Firebase;
using Firebase.Analytics;
using Firebase.Crashlytics;
using Firebase.Extensions;
using UnityEngine;

/// <summary>
/// Métricas (Firebase Analytics) e relatório de travamentos (Crashlytics).
/// Inicializa na primeira chamada; eventos enviados antes de o Firebase ficar pronto são guardados e
/// enviados depois. No teste automatizado (batchmode) nada é enviado.
///
/// Eventos principais (aparecem em Firebase → Analytics → Eventos):
///  run_start, run_end, revive, double_coins, car_bought, upgrade_bought, paint_bought,
///  mission_claimed, daily_claimed, map_unlocked.
/// Retenção (D1/D7) o Firebase calcula sozinho a partir de first_open/session_start.
/// </summary>
public static class Telemetry
{
    static bool started;
    static bool ready;
    static readonly List<(string name, Parameter[] ps)> queue = new List<(string, Parameter[])>();

    public static void Init()
    {
        if (started || Application.isBatchMode) return;
        started = true;
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result != DependencyStatus.Available)
            {
                Debug.LogWarning("[Telemetry] Firebase indisponível: " + task.Result);
                return;
            }
            _ = FirebaseApp.DefaultInstance;
            Crashlytics.ReportUncaughtExceptionsAsFatal = true;
            FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
            ready = true;
            foreach (var (name, ps) in queue) FirebaseAnalytics.LogEvent(name, ps);
            queue.Clear();
        });
    }

    /// <summary>Registra um evento. Valores aceitos: string, int/long, float/double, bool.</summary>
    public static void Log(string name, params (string key, object value)[] values)
    {
        if (Application.isBatchMode) return;
        Init();
        var ps = new Parameter[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            var (k, v) = values[i];
            ps[i] = v switch
            {
                int n => new Parameter(k, n),
                long n => new Parameter(k, n),
                float f => new Parameter(k, f),
                double d => new Parameter(k, d),
                bool b => new Parameter(k, b ? 1L : 0L),
                _ => new Parameter(k, v?.ToString() ?? ""),
            };
        }
        if (ready) FirebaseAnalytics.LogEvent(name, ps);
        else if (queue.Count < 50) queue.Add((name, ps));
    }

    /// <summary>Anota um erro não fatal no Crashlytics (ex.: algo que não deveria acontecer, mas o jogo seguiu).</summary>
    public static void NonFatal(System.Exception e)
    {
        if (ready) Crashlytics.LogException(e);
    }
}
