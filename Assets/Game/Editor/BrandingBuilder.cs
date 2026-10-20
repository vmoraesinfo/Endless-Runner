using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Gera a identidade visual a partir dos modelos do jogo:
///  - Ícone do app (1024, aplicado no Player Settings) + Store/icon-512.png para a Play Store.
///  - Arte de destaque da loja (Store/feature-graphic-1024x500.png) com o nome do jogo.
/// Uso: menu "Endless Runner/Render Branding" ou -executeMethod BrandingBuilder.Render
/// </summary>
public static class BrandingBuilder
{
    const string IconAsset = "Assets/Game/Branding/AppIcon.png";
    const string StoreDir = "Store";
    const string HeroCar = "Assets/Low Poly Cars/Models/Sports Racing Car.fbx";

    static readonly Color Top = new Color(1f, 0.55f, 0.15f);    // laranja
    static readonly Color Bottom = new Color(0.42f, 0.12f, 0.62f); // roxo

    [MenuItem("Endless Runner/Render Branding")]
    public static void Render()
    {
        // Sem compilação assíncrona: senão a foto sai antes dos shaders ficarem prontos (objetos somem).
        ShaderUtil.allowAsyncCompilation = false;
        Directory.CreateDirectory(StoreDir);
        Directory.CreateDirectory(Path.GetDirectoryName(IconAsset));

        var icon = Shoot(1024, 1024, false);
        File.WriteAllBytes(IconAsset, icon.EncodeToPNG());
        File.WriteAllBytes(Path.Combine(StoreDir, "icon-512.png"), Resize(icon, 512).EncodeToPNG());
        Object.DestroyImmediate(icon);

        var feature = Shoot(1024, 500, true);
        File.WriteAllBytes(Path.Combine(StoreDir, "feature-graphic-1024x500.png"), feature.EncodeToPNG());
        Object.DestroyImmediate(feature);

        AssetDatabase.ImportAsset(IconAsset, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(IconAsset);
        importer.textureType = TextureImporterType.Default;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(IconAsset);
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { tex }, IconKind.Any);
        AssetDatabase.SaveAssets();
        Debug.Log("[Branding] Ícone e arte da loja gerados em Store/ e aplicados ao app.");
    }

    /// <summary>Monta uma cena temporária com fundo em degradê, faixas de velocidade e o carro, e fotografa.</summary>
    static Texture2D Shoot(int w, int h, bool withTitle)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.75f, 0.7f, 0.8f);

        var light = new GameObject("Sun").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.4f;
        light.transform.rotation = Quaternion.Euler(35f, 140f, 0f);

        var cam = new GameObject("Cam").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Bottom;
        cam.fieldOfView = withTitle ? 30f : 34f;

        // Carro herói, normalizado para 4,4 m e apoiado no chão.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeroCar);
        var car = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        foreach (var t in car.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("Ground")) t.gameObject.SetActive(false);
        Bounds b = Bounds(car);
        car.transform.localScale *= 4.4f / Mathf.Max(b.size.z, 0.01f);
        b = Bounds(car);
        car.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);
        car.transform.rotation = Quaternion.Euler(0f, withTitle ? -35f : -30f, 0f);

        // Câmera baixa, 3/4 de frente; no banner o carro fica à direita para o título caber à esquerda.
        // A câmera olha para -Z, então +X da cena aparece à esquerda: mirar em +X joga o carro para a direita.
        Vector3 target = new Vector3(withTitle ? 2.4f : 0f, 0.75f, 0f);
        cam.transform.position = target + new Vector3(withTitle ? 1.2f : 3.0f, withTitle ? 1.6f : 1.9f, withTitle ? 9.5f : 7.0f);
        cam.transform.LookAt(target);

        // Fundo em degradê (quad sem iluminação atrás do carro).
        var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(bg.GetComponent<Collider>());
        bg.transform.position = cam.transform.position + cam.transform.forward * 60f;
        bg.transform.rotation = cam.transform.rotation;
        bg.transform.localScale = new Vector3(120f, 80f, 1f);
        bg.GetComponent<MeshRenderer>().sharedMaterial = Unlit(GradientTexture());

        // Faixas de velocidade passando atrás do carro.
        var streak = Unlit(null, new Color(1f, 1f, 1f, 1f));
        var rnd = new System.Random(3);
        for (int i = 0; i < 14; i++)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(s.GetComponent<Collider>());
            s.GetComponent<MeshRenderer>().sharedMaterial = streak;
            float y = (float)rnd.NextDouble() * 4f + 0.2f;
            float x = (float)rnd.NextDouble() * 12f - 6f;
            s.transform.position = new Vector3(x, y, -6f - (float)rnd.NextDouble() * 10f);
            s.transform.localScale = new Vector3(0.06f, 0.06f, 4f + (float)rnd.NextDouble() * 6f);
        }

        // Chão escuro com faixa tracejada.
        var road = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(road.GetComponent<Collider>());
        road.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        road.transform.localScale = new Vector3(40f, 60f, 1f);
        road.GetComponent<MeshRenderer>().sharedMaterial = Unlit(null, new Color(0.30f, 0.16f, 0.42f));
        for (int i = -6; i <= 6; i++)
        {
            var dash = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(dash.GetComponent<Collider>());
            dash.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            dash.transform.position = new Vector3(2.2f, 0.01f, i * 4f);
            dash.transform.localScale = new Vector3(0.18f, 2f, 1f);
            dash.GetComponent<MeshRenderer>().sharedMaterial = Unlit(null, new Color(1f, 0.85f, 0.3f));
        }

        if (withTitle) AddTitle(cam, w, h);

        var rt = new RenderTexture(w, h, 24) { antiAliasing = 8 };
        cam.targetTexture = rt;
        cam.Render(); // 1ª renderização só aquece shaders/variantes
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        return tex;
    }

    static void AddTitle(Camera cam, int w, int h)
    {
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(w, h);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Game/Fonts/Poppins-Bold SDF.asset");

        TMP_Text Line(string text, float size, Color color, float y)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(60f, y);
            rt.sizeDelta = new Vector2(560f, size * 1.3f);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Left;
            t.outlineWidth = 0.2f;
            t.outlineColor = new Color32(40, 10, 60, 255);
            return t;
        }

        Line("TRAFFIC", 92, Color.white, 95);
        Line("RUSH", 112, new Color(1f, 0.86f, 0.25f), -5);
        Line("RASPÃO", 54, new Color(0.45f, 0.9f, 1f), -110);
        Canvas.ForceUpdateCanvases();
    }

    static Material Unlit(Texture tex, Color? color = null)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        if (tex != null) mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", color ?? Color.white);
        return mat;
    }

    static Texture2D GradientTexture()
    {
        var tex = new Texture2D(4, 256, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 256; y++)
        {
            var c = Color.Lerp(Bottom, Top, Mathf.SmoothStep(0f, 1f, y / 255f));
            for (int x = 0; x < 4; x++) tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(false);
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static Texture2D Resize(Texture2D src, int size)
    {
        var rt = RenderTexture.GetTemporary(size, size, 0);
        Graphics.Blit(src, rt);
        RenderTexture.active = rt;
        var dst = new Texture2D(size, size, TextureFormat.RGB24, false);
        dst.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        dst.Apply();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        return dst;
    }
}
