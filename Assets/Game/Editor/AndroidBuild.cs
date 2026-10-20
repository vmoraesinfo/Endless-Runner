using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Gera o APK de teste (Development Build → anúncios de teste do Google) em Builds/EndlessRunner.apk.
/// Menu "Endless Runner/Build Android APK (teste)" ou:
///   Unity.exe -batchmode -quit -buildTarget Android -projectPath . -executeMethod AndroidBuild.BuildTestApk
/// </summary>
public static class AndroidBuild
{
    const string Output = "Builds/EndlessRunner.apk";

    [MenuItem("Endless Runner/Build Android APK (teste)")]
    public static void BuildTestApk()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        // APK instala direto no celular; o .aab (formato da Play Store) não.
        EditorUserBuildSettings.buildAppBundle = false;
        ResolveAndroidDependencies();

        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        var options = new BuildPlayerOptions
        {
            scenes = Array.ConvertAll(EditorBuildSettings.scenes, s => s.path),
            locationPathName = Output,
            target = BuildTarget.Android,
            options = BuildOptions.Development,
        };
        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        Debug.Log($"[AndroidBuild] {summary.result}: {summary.outputPath} ({summary.totalSize / (1024f * 1024f):F1} MB, {summary.totalErrors} erros)");

        // Volta ao formato da loja para não esquecer na hora de publicar.
        EditorUserBuildSettings.buildAppBundle = true;
        if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    /// <summary>
    /// Roda o Android Resolver (External Dependency Manager) para baixar o SDK do Google Mobile Ads
    /// e escrever as dependências no mainTemplate.gradle. Chamado por reflexão para não depender do pacote.
    /// </summary>
    static void ResolveAndroidDependencies()
    {
        var resolver = Type.GetType("GooglePlayServices.PlayServicesResolver, Google.JarResolver");
        var resolveSync = resolver?.GetMethod("ResolveSync", new[] { typeof(bool) });
        if (resolveSync == null)
        {
            Debug.LogWarning("[AndroidBuild] Android Resolver não encontrado; dependências do AdMob podem faltar.");
            return;
        }
        bool ok = (bool)resolveSync.Invoke(null, new object[] { true });
        Debug.Log("[AndroidBuild] Android Resolver: " + (ok ? "ok" : "falhou"));
    }
}
