using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Ferramentas para avaliar packs da Asset Store:
///  - UpgradeMaterials: converte materiais com shaders do pipeline antigo (Built-in) para URP Lit.
///  - RenderSheets: gera uma "folha de contato" PNG por pasta, com uma miniatura de cada prefab,
///    em Logs/preview/ (e um .txt com a ordem dos nomes).
/// Uso: -executeMethod VendorPreview.Run
/// </summary>
public static class VendorPreview
{
    static readonly string[] Folders =
    {
        "Assets/Awbmecreations",
        "Assets/Unity Technologies/CarsAssetPack/Prefabs/FBX",
        "Assets/Low Poly Simple Urban City 3D Asset Pack/Prefabs",
        "Assets/LowpolyStreetPack/Prefabs/Foliage",
        "Assets/LowpolyStreetPack/Prefabs/Roads/Streets",
        "Assets/LowpolyStreetPack/Prefabs/Roads/Pavement",
        "Assets/LowpolyStreetPack/Prefabs/StreetProps",
    };

    const int Tile = 200;
    const int Columns = 8;

    public static void Run()
    {
        foreach (var f in Folders) UpgradeMaterials(f.Split('/').Take(2).Aggregate((a, b) => a + "/" + b));
        RenderSheets();
    }

    [MenuItem("Endless Runner/Upgrade Vendor Materials To URP")]
    public static void UpgradeAll()
    {
        foreach (var f in Folders) UpgradeMaterials(f.Split('/').Take(2).Aggregate((a, b) => a + "/" + b));
    }

    /// <summary>Troca shaders Built-in (Standard, Legacy, Mobile...) por URP Lit, mantendo textura e cor.</summary>
    public static void UpgradeMaterials(string folder)
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null) continue;
            string name = mat.shader.name;
            if (name.StartsWith("Universal Render Pipeline") || name.StartsWith("Shader Graphs") || name.StartsWith("TextMeshPro") || name.StartsWith("Skybox")) continue;

            Texture tex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            Vector2 tiling = mat.HasProperty("_MainTex") ? mat.GetTextureScale("_MainTex") : Vector2.one;
            Vector2 offset = mat.HasProperty("_MainTex") ? mat.GetTextureOffset("_MainTex") : Vector2.zero;
            Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            Texture normal = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
            float smooth = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.2f;
            float metal = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
            // Folhas e faixas pintadas usavam transparência antiga; no URP viram recorte (alpha clip), mais barato no celular.
            bool cutout = name.Contains("Cutout") || name.Contains("Transparent") || (mat.HasProperty("_Mode") && Mathf.RoundToInt(mat.GetFloat("_Mode")) == 1);

            mat.shader = lit;
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", tiling);
            mat.SetTextureOffset("_BaseMap", offset);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", Mathf.Min(smooth, 0.5f));
            mat.SetFloat("_Metallic", metal);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (cutout)
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.EnableKeyword("_ALPHATEST_ON");
            }
            EditorUtility.SetDirty(mat);
            Debug.Log($"[VendorPreview] {path}: {name} -> URP Lit");
        }
        AssetDatabase.SaveAssets();
    }

    public static void RenderSheets()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var light = new GameObject("Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.transform.rotation = Quaternion.Euler(45f, -40f, 0f);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.62f);

        var cam = new GameObject("Cam").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.82f, 0.86f, 0.9f);
        cam.fieldOfView = 30f;
        var rt = new RenderTexture(Tile, Tile, 24);
        cam.targetTexture = rt;

        string outDir = Path.GetFullPath("Logs/preview");
        Directory.CreateDirectory(outDir);

        foreach (var folder in Folders)
        {
            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !p.Contains("/Scene") && !Path.GetFileName(p).StartsWith("ModelPack"))
                .OrderBy(p => p).ToList();
            if (prefabs.Count == 0) continue;

            int rows = Mathf.CeilToInt(prefabs.Count / (float)Columns);
            var sheet = new Texture2D(Columns * Tile, rows * Tile, TextureFormat.RGB24, false);
            var index = new List<string>();
            for (int i = 0; i < prefabs.Count; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabs[i]);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                var renderers = go.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds b = renderers[0].bounds;
                    foreach (var r in renderers) b.Encapsulate(r.bounds);
                    float radius = Mathf.Max(b.extents.magnitude, 0.01f);
                    float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                    Vector3 dir = Quaternion.Euler(25f, -140f, 0f) * Vector3.forward;
                    cam.transform.position = b.center - dir * dist;
                    cam.transform.LookAt(b.center);
                    cam.nearClipPlane = dist * 0.05f;
                    cam.farClipPlane = dist * 3f;
                    cam.Render();
                    RenderTexture.active = rt;
                    int col = i % Columns, row = rows - 1 - i / Columns;
                    sheet.ReadPixels(new Rect(0, 0, Tile, Tile), col * Tile, row * Tile);
                    RenderTexture.active = null;
                }
                var size = renderers.Length > 0 ? renderers.Aggregate(renderers[0].bounds, (acc, r) => { acc.Encapsulate(r.bounds); return acc; }).size : Vector3.zero;
                index.Add($"{i + 1:00} {prefabs[i].Substring(folder.Length + 1)}  (tam {size.x:F1} x {size.y:F1} x {size.z:F1} m)");
                Object.DestroyImmediate(go);
            }
            sheet.Apply();
            string name = folder.Replace("Assets/", "").Replace("/", "_").Replace(" ", "");
            File.WriteAllBytes(Path.Combine(outDir, name + ".png"), sheet.EncodeToPNG());
            File.WriteAllLines(Path.Combine(outDir, name + ".txt"), index);
            Debug.Log($"[VendorPreview] {folder}: {prefabs.Count} prefabs");
        }
    }
}
