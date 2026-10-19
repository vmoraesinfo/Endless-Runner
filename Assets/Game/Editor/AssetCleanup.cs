using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Lista os arquivos dos packs da Asset Store que nenhuma cena do build nem nada em Resources usa.
/// Menu "Endless Runner/Report Unused Vendor Assets" ou -executeMethod AssetCleanup.Report.
/// Gera Logs/unused_assets.txt (um caminho por linha) para revisar e apagar.
/// </summary>
public static class AssetCleanup
{
    // Só pastas de terceiros: o código e os assets do jogo ficam em Assets/Game.
    static readonly string[] VendorFolders =
    {
        "Assets/KajamansRoads", "Assets/Low Poly Cars", "Assets/_Barking_Dog",
        "Assets/TWD", "Assets/Pack_FREE_Cars", "Assets/Awbmecreations", "Assets/Unity Technologies",
        "Assets/Low Poly Simple Urban City 3D Asset Pack", "Assets/LowpolyStreetPack",
    };

    [MenuItem("Endless Runner/Report Unused Vendor Assets")]
    public static void Report()
    {
        var roots = new List<string>();
        roots.AddRange(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path));
        // Tudo em Resources entra no build mesmo sem referência; e Assets/Game é usado pelo GameBuilder.
        roots.AddRange(AssetDatabase.GetAllAssetPaths().Where(p =>
            p.StartsWith("Assets/") && (p.Contains("/Resources/") || p.StartsWith("Assets/Game/"))));

        var used = new HashSet<string>(AssetDatabase.GetDependencies(roots.ToArray(), true));
        // O GameBuilder carrega a fonte e os ícones por caminho; mantém para poder reconstruir as cenas.
        used.Add("Assets/TWD/FreeButtonSet/Fonts/Poppins-Bold.ttf");

        var unused = new List<string>();
        foreach (var folder in VendorFolders)
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            foreach (var guid in AssetDatabase.FindAssets("", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path) || used.Contains(path)) continue;
                unused.Add(path);
            }
        }
        unused = unused.Distinct().OrderBy(p => p).ToList();

        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/unused_assets.txt", unused);
        long bytes = unused.Sum(p => File.Exists(p) ? new FileInfo(p).Length : 0);
        Debug.Log($"[AssetCleanup] {unused.Count} arquivos sem uso ({bytes / (1024f * 1024f):F1} MB). Lista em Logs/unused_assets.txt");
    }
}
