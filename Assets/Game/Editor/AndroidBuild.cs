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
    /// Gera o .aab de produção assinado para a Play Store em Builds/TrafficRush.aab.
    /// A senha NUNCA fica no projeto: vem das variáveis de ambiente
    ///   TRAFFICRUSH_KEYSTORE_PASS (obrigatória) e TRAFFICRUSH_KEY_PASS (opcional, padrão = a mesma),
    ///   TRAFFICRUSH_KEYSTORE (opcional, padrão C:/projects/trafficrush.keystore).
    /// Cada envio para a loja precisa de um versionCode maior: passe -versionCode N na linha de comando.
    /// </summary>
    public static void BuildRelease()
    {
        string keystore = Environment.GetEnvironmentVariable("TRAFFICRUSH_KEYSTORE") ?? "C:/projects/trafficrush.keystore";
        string storePass = Environment.GetEnvironmentVariable("TRAFFICRUSH_KEYSTORE_PASS");
        string keyPass = Environment.GetEnvironmentVariable("TRAFFICRUSH_KEY_PASS") ?? storePass;
        if (!File.Exists(keystore) || string.IsNullOrEmpty(storePass))
        {
            Debug.LogError($"[AndroidBuild] Keystore não encontrada ({keystore}) ou senha não definida em TRAFFICRUSH_KEYSTORE_PASS.");
            if (Application.isBatchMode) EditorApplication.Exit(2);
            return;
        }

        var args = Environment.GetCommandLineArgs();
        int idx = Array.IndexOf(args, "-versionCode");
        if (idx >= 0 && idx + 1 < args.Length && int.TryParse(args[idx + 1], out int code))
            PlayerSettings.Android.bundleVersionCode = code;

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = keystore;
        PlayerSettings.Android.keystorePass = storePass;
        PlayerSettings.Android.keyaliasName = "trafficrush";
        PlayerSettings.Android.keyaliasPass = keyPass;
        EditorUserBuildSettings.buildAppBundle = true;
        ResolveAndroidDependencies();

        const string output = "Builds/TrafficRush.aab";
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = Array.ConvertAll(EditorBuildSettings.scenes, s => s.path),
            locationPathName = output,
            target = BuildTarget.Android,
            options = BuildOptions.None, // sem Development Build → anúncios reais
        });
        var summary = report.summary;
        Debug.Log($"[AndroidBuild] Release {summary.result}: {summary.outputPath} versão {PlayerSettings.bundleVersion} ({PlayerSettings.Android.bundleVersionCode}), {summary.totalErrors} erros");

        // Não deixa a senha na memória do editor nem a configuração de assinatura no projeto.
        PlayerSettings.Android.keystorePass = "";
        PlayerSettings.Android.keyaliasPass = "";
        PlayerSettings.Android.useCustomKeystore = false;
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
