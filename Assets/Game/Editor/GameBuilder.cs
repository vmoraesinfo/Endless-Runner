using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Monta automaticamente materiais, prefabs, catálogo de carros, a cena da corrida (Game)
/// e a interface da garagem (CarChose). Pode ser rodado de novo a qualquer momento:
/// menu "Endless Runner/Rebuild Everything" ou via linha de comando (-executeMethod GameBuilder.BuildAll).
/// </summary>
public static partial class GameBuilder
{
    const string Root = "Assets/Game";
    const string MatDir = Root + "/Materials";
    const string PrefabDir = Root + "/Prefabs";
    const string ResDir = Root + "/Resources";
    const string GameScenePath = "Assets/Scenes/Game.unity";
    const string GarageScenePath = "Assets/Scenes/CarChose.unity";
    const string TwdDir = "Assets/TWD/FreeButtonSet";

    static readonly Color Sky = new Color(0.62f, 0.80f, 0.95f);
    static readonly Color Green = new Color(0.22f, 0.72f, 0.35f);
    static readonly Color Blue = new Color(0.20f, 0.50f, 0.90f);
    static readonly Color Orange = new Color(0.98f, 0.58f, 0.15f);
    static readonly Color Purple = new Color(0.55f, 0.32f, 0.85f);
    static readonly Color Gray = new Color(0.35f, 0.38f, 0.45f);
    static readonly Color Gold = new Color(1f, 0.80f, 0.15f);
    static readonly Color Dim = new Color(0f, 0f, 0f, 0.65f);
    static readonly Color Card = new Color(0.10f, 0.13f, 0.20f, 0.95f);
    static readonly Color Pill = new Color(0f, 0f, 0f, 0.40f);

    static TMP_FontAsset font;
    static Sprite roundSprite;

    // Preços dos carros na ordem da garagem. O primeiro é sempre grátis.
    static readonly int[] Prices = { 0, 150, 300, 500, 800, 1200, 1600, 2000 };

    [MenuItem("Endless Runner/Rebuild Everything")]
    public static void BuildAll()
    {
        EnsureFolder(MatDir);
        EnsureFolder(PrefabDir);
        EnsureFolder(ResDir);
        LoadUiAssets();

        var mats = BuildMaterials();
        BuildAdsSettings();
        BuildTrafficCatalog();
        var coin = BuildCoinPrefab(mats);
        var pickups = new[] { BuildMagnetPrefab(mats), BuildShieldPrefab(mats) };
        var segment = BuildRoadSegmentPrefab(mats);
        var citySegment = BuildCitySegment(mats);
        BuildMapCatalog(segment, citySegment, BuildRoadworks());
        BuildGarage();
        BuildGameScene(mats, coin, segment, pickups);
        ConfigureProject();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[GameBuilder] Tudo montado.");
    }

    // ---------------------------------------------------------------- utilidades

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static void LoadUiAssets()
    {
        roundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TwdDir + "/Textures/other/universal_panel_20.png");

        string fontPath = Root + "/Fonts/Poppins-Bold SDF.asset";
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
        if (font == null)
        {
            var ttf = AssetDatabase.LoadAssetAtPath<Font>(TwdDir + "/Fonts/Poppins-Bold.ttf");
            if (ttf != null)
            {
                EnsureFolder(Root + "/Fonts");
                font = TMP_FontAsset.CreateFontAsset(ttf, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                font.name = "Poppins-Bold SDF";
                AssetDatabase.CreateAsset(font, fontPath);
                font.material.name = "Poppins-Bold SDF Material";
                font.atlasTextures[0].name = "Poppins-Bold SDF Atlas";
                AssetDatabase.AddObjectToAsset(font.material, font);
                AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
                AssetDatabase.SaveAssets();
            }
        }
        if (font == null) font = TMP_Settings.defaultFontAsset;
    }

    static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material mat,
        Vector3 pos, Vector3 scale, Vector3 euler = default)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.transform.localEulerAngles = euler;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        return go;
    }

    // ---------------------------------------------------------------- materiais

    class Mats
    {
        public Material road, grass, rail, post, trunk, leaves, leavesDark, rock, coin, debris;
        public Material magnet, metal, shieldItem, bubble, flame, speedLine;
    }

    static Material Mat(string name, Color color, Texture tex = null, Vector2? tiling = null, Vector2? offset = null,
        float smooth = 0.1f, float metallic = 0f, Color? emission = null)
    {
        string path = $"{MatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.SetColor("_BaseColor", color);
        mat.SetTexture("_BaseMap", tex);
        mat.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
        mat.SetTextureOffset("_BaseMap", offset ?? Vector2.zero);
        mat.SetFloat("_Smoothness", smooth);
        mat.SetFloat("_Metallic", metallic);
        if (emission.HasValue)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission.Value);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        mat.enableInstancing = false; // URP usa o SRP Batcher
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Mats BuildMaterials()
    {
        const string tex = "Assets/KajamansRoads/Textures/";
        var m = new Mats();
        // Metade esquerda da textura de 6 faixas = 3 faixas. O offset centraliza a faixa do meio em x = 0.
        m.road = Mat("Road", Color.white, AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "Road_6lane_dark02.png"),
            new Vector2(0.5f, 1f), new Vector2(0.0155f, 0f), 0.15f);
        m.grass = Mat("Grass", new Color(0.75f, 0.9f, 0.7f), AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "GrassGreenTexture512x512.png"),
            new Vector2(5f, 3f), null, 0f);
        m.rail = Mat("GuardRail", Color.white, AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "Guardrails01.png"),
            new Vector2(4f, 1f), null, 0.5f, 0.4f);
        m.post = Mat("Post", new Color(0.45f, 0.47f, 0.5f), null, null, null, 0.3f, 0.3f);
        m.trunk = Mat("Trunk", new Color(0.42f, 0.28f, 0.17f), null, null, null, 0f);
        m.leaves = Mat("Leaves", new Color(0.30f, 0.62f, 0.25f), null, null, null, 0f);
        m.leavesDark = Mat("LeavesDark", new Color(0.16f, 0.45f, 0.26f), null, null, null, 0f);
        m.rock = Mat("Rock", new Color(0.55f, 0.55f, 0.58f), null, null, null, 0.05f);
        m.coin = Mat("Coin", Gold, null, null, null, 0.75f, 0.7f, new Color(0.45f, 0.32f, 0f));
        m.debris = Mat("Debris", new Color(0.3f, 0.3f, 0.32f), null, null, null, 0.2f);
        m.magnet = Mat("Magnet", new Color(0.9f, 0.15f, 0.15f), null, null, null, 0.6f, 0.2f, new Color(0.35f, 0f, 0f));
        m.metal = Mat("MagnetTip", new Color(0.85f, 0.87f, 0.9f), null, null, null, 0.8f, 0.9f);
        m.shieldItem = Mat("ShieldItem", new Color(0.3f, 0.65f, 1f), null, null, null, 0.8f, 0.3f, new Color(0f, 0.25f, 0.6f));
        m.bubble = Mat("ShieldBubble", new Color(0.4f, 0.75f, 1f, 0.28f), null, null, null, 0.9f, 0f, new Color(0f, 0.15f, 0.35f));
        MakeTransparent(m.bubble);
        m.flame = Mat("Flame", new Color(1f, 0.55f, 0.1f), null, null, null, 0f, 0f, new Color(2f, 0.8f, 0.1f));
        m.speedLine = Mat("SpeedLine", Color.white, null, null, null, 0f, 0f, new Color(1.5f, 1.5f, 1.5f));
        return m;
    }

    static void MakeTransparent(Material mat)
    {
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(mat);
    }

    // IDs do AdMob (admob.google.com → Apps → Endless Runner).
    const string AdMobAppId = "ca-app-pub-2144609610465059~5642913108";

    static void BuildAdsSettings()
    {
        var ads = LoadOrCreate<AdsSettings>(ResDir + "/AdsSettings.asset");
        ads.androidAppId = AdMobAppId;
        if (string.IsNullOrEmpty(ads.bannerAdUnitId) || !ads.bannerAdUnitId.StartsWith("ca-app-pub-"))
            ads.bannerAdUnitId = "ca-app-pub-2144609610465059/7836588912";
        if (string.IsNullOrEmpty(ads.interstitialAdUnitId)) ads.interstitialAdUnitId = "ca-app-pub-2144609610465059/3598880872";
        if (string.IsNullOrEmpty(ads.rewardedAdUnitId)) ads.rewardedAdUnitId = "ca-app-pub-2144609610465059/7098222315";
        EditorUtility.SetDirty(ads);

        // O plugin do Google lê o App ID do próprio asset de configurações; sem ele o app fecha ao abrir.
        // A classe é internal no plugin, então é acessada por reflexão.
        var type = System.Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Editor");
        var load = type?.GetMethod("LoadInstance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        var gma = load?.Invoke(null, null) as ScriptableObject;
        var prop = type?.GetProperty("GoogleMobileAdsAndroidAppId");
        if (gma == null || prop == null)
        {
            Debug.LogError("[GameBuilder] Plugin Google Mobile Ads não encontrado; configure o App ID em Assets > Google Mobile Ads > Settings.");
            return;
        }
        prop.SetValue(gma, AdMobAppId);
        EditorUtility.SetDirty(gma);
    }

    // Veículos do trânsito (pack "FREE Cartoon Car Pack"). Peso = chance relativa de aparecer.
    static readonly (string name, float weight)[] TrafficVehicles =
    {
        ("Hatchback", 3f), ("Taxi", 3f), ("Pickup", 2f), ("Van", 2f),
        ("Police", 1f), ("Towtruck", 1f), ("VanBig", 1f), ("Truck", 1f),
    };

    static void BuildTrafficCatalog()
    {
        var catalog = LoadOrCreate<TrafficCatalog>(ResDir + "/TrafficCatalog.asset");
        catalog.vehicles.Clear();
        foreach (var (name, weight) in TrafficVehicles)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Pack_FREE_Cars/Prefabs/{name}.prefab");
            if (prefab == null) { Debug.LogWarning($"[GameBuilder] Veículo de trânsito não encontrado: {name}"); continue; }
            catalog.vehicles.Add(new TrafficCatalog.Entry { prefab = prefab, weight = weight });
        }
        if (catalog.vehicles.Count == 0)
            Debug.LogError("[GameBuilder] Nenhum veículo de trânsito — importe o FREE Cartoon Car Pack.");
        EditorUtility.SetDirty(catalog);
    }

    // ---------------------------------------------------------------- prefabs

    static GameObject BuildCoinPrefab(Mats m)
    {
        var root = new GameObject("Coin");
        root.AddComponent<Coin>();
        var col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.9f;
        Primitive(PrimitiveType.Cylinder, "Visual", root.transform, m.coin, Vector3.zero, new Vector3(0.9f, 0.08f, 0.9f), new Vector3(90f, 0f, 0f));
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/Coin.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    static Pickup NewPickup(string name, PickupType type, out Transform visual)
    {
        var root = new GameObject(name);
        var col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.1f;
        var p = root.AddComponent<Pickup>();
        p.type = type;
        visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);
        p.visual = visual;
        return p;
    }

    static Pickup SavePickup(Pickup p)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(p.gameObject, $"{PrefabDir}/{p.name}.prefab");
        Object.DestroyImmediate(p.gameObject);
        return prefab.GetComponent<Pickup>();
    }

    /// <summary>Ímã em U: duas pernas vermelhas, base e pontas prateadas.</summary>
    static Pickup BuildMagnetPrefab(Mats m)
    {
        var p = NewPickup("PickupMagnet", PickupType.Magnet, out var v);
        Primitive(PrimitiveType.Cube, "Base", v, m.magnet, new Vector3(0, -0.45f, 0), new Vector3(1.2f, 0.35f, 0.35f));
        Primitive(PrimitiveType.Cube, "LegL", v, m.magnet, new Vector3(-0.42f, 0.05f, 0), new Vector3(0.35f, 0.8f, 0.35f));
        Primitive(PrimitiveType.Cube, "LegR", v, m.magnet, new Vector3(0.42f, 0.05f, 0), new Vector3(0.35f, 0.8f, 0.35f));
        Primitive(PrimitiveType.Cube, "TipL", v, m.metal, new Vector3(-0.42f, 0.55f, 0), new Vector3(0.36f, 0.25f, 0.36f));
        Primitive(PrimitiveType.Cube, "TipR", v, m.metal, new Vector3(0.42f, 0.55f, 0), new Vector3(0.36f, 0.25f, 0.36f));
        return SavePickup(p);
    }

    /// <summary>Escudo: cristal azul (cubo girado) com um anel.</summary>
    static Pickup BuildShieldPrefab(Mats m)
    {
        var p = NewPickup("PickupShield", PickupType.Shield, out var v);
        Primitive(PrimitiveType.Cube, "Gem", v, m.shieldItem, Vector3.zero, Vector3.one * 0.85f, new Vector3(45f, 0f, 45f));
        Primitive(PrimitiveType.Cylinder, "Ring", v, m.metal, Vector3.zero, new Vector3(1.5f, 0.04f, 1.5f));
        return SavePickup(p);
    }

    static RoadSegment BuildRoadSegmentPrefab(Mats m)
    {
        const float len = 24f;
        var root = new GameObject("RoadSegment");
        var seg = root.AddComponent<RoadSegment>();
        seg.segmentLength = len;
        var t = root.transform;

        Primitive(PrimitiveType.Quad, "Road", t, m.road, new Vector3(0f, 0f, len / 2f), new Vector3(12f, len, 1f), new Vector3(90f, 0f, 0f));
        Primitive(PrimitiveType.Quad, "GrassL", t, m.grass, new Vector3(-26f, -0.02f, len / 2f), new Vector3(40f, len, 1f), new Vector3(90f, 0f, 0f));
        Primitive(PrimitiveType.Quad, "GrassR", t, m.grass, new Vector3(26f, -0.02f, len / 2f), new Vector3(40f, len, 1f), new Vector3(90f, 0f, 0f));

        foreach (float side in new[] { -1f, 1f })
        {
            string s = side < 0 ? "L" : "R";
            Primitive(PrimitiveType.Cube, "Rail" + s, t, m.rail, new Vector3(side * 6.4f, 0.65f, len / 2f), new Vector3(0.12f, 0.35f, len));
            for (int i = 0; i < 4; i++)
                Primitive(PrimitiveType.Cube, $"Post{s}{i}", t, m.post, new Vector3(side * 6.5f, 0.4f, i * 6f + 3f), new Vector3(0.15f, 0.8f, 0.15f));
        }

        var decorRoot = new GameObject("Decor").transform;
        decorRoot.SetParent(t, false);
        var decor = new List<Transform>();
        for (int i = 0; i < 10; i++)
        {
            var d = new GameObject("Decor" + i).transform;
            d.SetParent(decorRoot, false);
            switch (i % 5)
            {
                case 0: // árvore redonda low-poly
                case 1:
                    Primitive(PrimitiveType.Cylinder, "Trunk", d, m.trunk, new Vector3(0, 1.2f, 0), new Vector3(0.45f, 1.2f, 0.45f));
                    Primitive(PrimitiveType.Cube, "Leaves1", d, m.leaves, new Vector3(0, 3.2f, 0), Vector3.one * 2.8f, new Vector3(45f, 30f, 45f));
                    Primitive(PrimitiveType.Cube, "Leaves2", d, m.leaves, new Vector3(0.4f, 4.4f, 0.2f), Vector3.one * 1.8f, new Vector3(20f, 60f, 35f));
                    break;
                case 2: // pinheiro
                case 3:
                    Primitive(PrimitiveType.Cylinder, "Trunk", d, m.trunk, new Vector3(0, 1f, 0), new Vector3(0.4f, 1f, 0.4f));
                    Primitive(PrimitiveType.Cube, "Leaves1", d, m.leavesDark, new Vector3(0, 2.6f, 0), new Vector3(2.8f, 1.6f, 2.8f), new Vector3(0, 45f, 0));
                    Primitive(PrimitiveType.Cube, "Leaves2", d, m.leavesDark, new Vector3(0, 3.9f, 0), new Vector3(2.0f, 1.4f, 2.0f), new Vector3(0, 20f, 0));
                    Primitive(PrimitiveType.Cube, "Leaves3", d, m.leavesDark, new Vector3(0, 5.0f, 0), new Vector3(1.1f, 1.2f, 1.1f), new Vector3(0, 70f, 0));
                    break;
                default: // pedra
                    Primitive(PrimitiveType.Cube, "Rock", d, m.rock, new Vector3(0, 0.3f, 0), new Vector3(1.6f, 0.9f, 1.3f), new Vector3(15f, 30f, 10f));
                    break;
            }
            decor.Add(d);
        }
        seg.decorations = decor.ToArray();

        // Nada do cenário precisa projetar sombra além das árvores; economiza no celular.
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
            if (!r.transform.IsChildOf(decorRoot)) r.shadowCastingMode = ShadowCastingMode.Off;

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/RoadSegment.prefab");
        Object.DestroyImmediate(root);
        return prefab.GetComponent<RoadSegment>();
    }

    static ParticleSystem BuildBurst(string name, Transform parent, Material mat, Color color, float size, float speed, float gravity)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startRotation3D = true;
        main.startColor = color;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 64;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh;
        r.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        r.sharedMaterial = mat;
        return ps;
    }

    // ---------------------------------------------------------------- catálogo + garagem

    static CarCatalog BuildCatalog(Transform carHolder)
    {
        var catalog = LoadOrCreate<CarCatalog>(ResDir + "/CarCatalog.asset");
        var oldPrices = new Dictionary<GameObject, int>();
        foreach (var e in catalog.cars) if (e.prefab != null) oldPrices[e.prefab] = e.price;

        catalog.cars.Clear();
        int i = 0;
        foreach (Transform child in carHolder)
        {
            var prefab = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
            if (prefab == null)
            {
                Debug.LogWarning($"[GameBuilder] {child.name} não é instância de prefab; ignorado no catálogo.");
                continue;
            }
            var info = GarageInfo(prefab);
            int price = info?.price ?? (oldPrices.TryGetValue(prefab, out var p) ? p : Prices[Mathf.Min(i, Prices.Length - 1)]);
            if (i == 0) price = 0;
            catalog.cars.Add(new CarCatalog.Entry { displayName = info?.name ?? prefab.name, prefab = prefab, price = price });
            i++;
        }
        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    static void BuildGarage()
    {
        var scene = EditorSceneManager.OpenScene(GarageScenePath, OpenSceneMode.Single);

        // Remove o antigo gerenciador de banner (script quebrado) e qualquer script faltando.
        foreach (var go in scene.GetRootGameObjects())
        {
            if (go.name == "IronSourceBannerManager") { Object.DestroyImmediate(go); continue; }
        }
        foreach (var go in scene.GetRootGameObjects())
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

        var select = Object.FindFirstObjectByType<SelectCars>(FindObjectsInactive.Include);
        AddGarageCars(select.carHolder.transform);
        BuildCatalog(select.carHolder.transform);

        // A câmera visível da garagem é a de maior depth. Foi enquadrada em paisagem;
        // em retrato precisa de FOV maior para os carros largos (monster trucks) caberem.
        Camera top = null;
        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (top == null || c.depth > top.depth) top = c;
        if (top != null) top.fieldOfView = 72f;


        FixEventSystem();

        var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        SetupScaler(canvas.gameObject);

        var old = canvas.transform.Find("GarageUI");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var ui = Stretch(new GameObject("GarageUI", typeof(RectTransform)).transform as RectTransform, canvas.transform);
        ui.gameObject.AddComponent<SafeArea>();
        ui.SetAsFirstSibling();

        // Topo: recorde à esquerda, moedas à direita, nome do carro no meio.
        select.bestText = Text(ui, "Best", "", 44, TextAlignmentOptions.Left, Color.white,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -60), new Vector2(560, 80), new Vector2(0, 1));
        select.coinsText = CoinPill(ui, new Vector2(1, 1), new Vector2(-40, -50));
        var namePill = Panel(ui, "CarNamePill", Pill, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -300), new Vector2(940, 120));
        select.carNameText = Text(namePill, "CarName", "CARRO", 64, TextAlignmentOptions.Center, Color.white,
            Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-40, 0));

        var badge = Panel(ui, "LockedBadge", new Color(0.85f, 0.2f, 0.2f, 0.95f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -410), new Vector2(360, 80));
        Text(badge, "Label", "BLOQUEADO", 40, TextAlignmentOptions.Center, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        select.lockedBadge = badge.gameObject;

        // Botão principal acima do espaço reservado para o banner.
        var action = Button(ui, "ActionButton", "JOGAR", Green, new Vector2(0.5f, 0), new Vector2(0, 330), new Vector2(640, 180), 72, select.OnActionButton);
        select.actionButton = action;
        select.actionButtonText = action.GetComponentInChildren<TMP_Text>();
        AddMapButton(ui, select);

        BuildGarageMenus(canvas.transform, ui, select);
        BuildShopUI(canvas.transform, ui, select);

        // Setas existentes: ancoradas nas laterais, na altura do carro.
        PlaceArrow(canvas.transform, "PrevButton", "arrow_left", new Vector2(0, 0.5f), new Vector2(100, -200));
        PlaceArrow(canvas.transform, "NextButton", "arrow_right", new Vector2(1, 0.5f), new Vector2(-100, -200));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    /// <summary>Botão e painel de missões + painel de recompensa diária da garagem.</summary>
    static void BuildGarageMenus(Transform canvas, RectTransform ui, SelectCars select)
    {
        foreach (var name in new[] { "MissionsPanel", "DailyPanel" })
        {
            var old = canvas.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }

        var menus = ui.gameObject.AddComponent<GarageMenus>();
        menus.garage = select;
        var top = new Vector2(0.5f, 1);
        var center = new Vector2(0.5f, 0.5f);

        var missionsButton = Button(ui, "MissionsButton", "MISSÕES", Orange, new Vector2(0, 1), new Vector2(180, -165), new Vector2(300, 100), 40, menus.OpenMissions);
        var badge = Place(NewRect("Badge"), missionsButton.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-6, -6), new Vector2(44, 44));
        var badgeImg = badge.gameObject.AddComponent<Image>();
        badgeImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        badgeImg.color = new Color(0.95f, 0.2f, 0.2f);
        badgeImg.raycastTarget = false;
        badge.gameObject.AddComponent<Pulse>().amount = 0.15f;
        menus.missionsBadge = badge.gameObject;

        // Painel de missões
        var panel = Stretch(NewRect("MissionsPanel"), canvas);
        panel.gameObject.AddComponent<Image>().color = Dim;
        var card = Panel(panel, "Card", Card, center, center, Vector2.zero, new Vector2(980, 1300));
        Text(card, "Title", "MISSÕES", 84, TextAlignmentOptions.Center, Color.white, top, top, new Vector2(0, -100), new Vector2(900, 120));
        UnityAction[] claims = { menus.Claim0, menus.Claim1, menus.Claim2 };
        menus.rows = new GarageMenus.MissionRow[Missions.ActiveCount];
        for (int i = 0; i < Missions.ActiveCount; i++)
        {
            var row = Panel(card, "Mission" + i, new Color(1f, 1f, 1f, 0.06f), top, top, new Vector2(0, -290 - i * 300), new Vector2(900, 260));
            var r = new GarageMenus.MissionRow();
            r.description = Text(row, "Description", "Missão", 36, TextAlignmentOptions.Left, Color.white,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -25), new Vector2(560, 100), new Vector2(0, 1));
            r.bar = Bar(row, "Progress", new Vector2(0, 0), new Vector2(330, 45), new Vector2(580, 34), Pill, Green);
            r.progress = Text(row, "ProgressText", "0/0", 34, TextAlignmentOptions.Left, new Color(0.8f, 0.85f, 0.95f),
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 95), new Vector2(400, 46), new Vector2(0, 0.5f));
            r.claimButton = Button(row, "Claim", "PEGAR", Green, new Vector2(1, 0.5f), new Vector2(-150, 0), new Vector2(240, 120), 38, claims[i]);
            r.claimLabel = r.claimButton.GetComponentInChildren<TMP_Text>();
            menus.rows[i] = r;
        }
        Button(card, "Close", "FECHAR", Blue, new Vector2(0.5f, 0), new Vector2(0, 110), new Vector2(500, 130), 52, menus.CloseMissions);
        menus.missionsPanel = panel.gameObject;

        // Painel de recompensa diária
        var daily = Stretch(NewRect("DailyPanel"), canvas);
        daily.gameObject.AddComponent<Image>().color = Dim;
        var dCard = Panel(daily, "Card", Card, center, center, Vector2.zero, new Vector2(820, 860));
        Text(dCard, "Title", "RECOMPENSA DIÁRIA", 64, TextAlignmentOptions.Center, Color.white, top, top, new Vector2(0, -110), new Vector2(760, 110));
        menus.dailyDayText = Text(dCard, "Day", "DIA 1 DE 7", 44, TextAlignmentOptions.Center, new Color(0.8f, 0.85f, 0.95f), top, top, new Vector2(0, -210), new Vector2(760, 70));
        var coin = Place(NewRect("CoinIcon"), dCard, center, center, new Vector2(0, 90), new Vector2(170, 170));
        var coinImg = coin.gameObject.AddComponent<Image>();
        coinImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        coinImg.color = Gold;
        coinImg.raycastTarget = false;
        coin.gameObject.AddComponent<Pulse>();
        menus.dailyRewardText = Text(dCard, "Reward", "+50", 100, TextAlignmentOptions.Center, Gold, center, center, new Vector2(0, -80), new Vector2(700, 120));
        Text(dCard, "Hint", "Volte amanhã para ganhar mais!", 34, TextAlignmentOptions.Center, new Color(0.75f, 0.8f, 0.9f),
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 215), new Vector2(760, 56));
        Button(dCard, "Claim", "PEGAR", Green, new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(500, 140), 60, menus.ClaimDaily);
        menus.dailyPanel = daily.gameObject;
    }

    static void PlaceArrow(Transform canvas, string name, string icon, Vector2 anchor, Vector2 pos)
    {
        var t = FindDeep(canvas, name) as RectTransform;
        if (t == null) return;
        t.anchorMin = t.anchorMax = anchor;
        t.pivot = new Vector2(0.5f, 0.5f);
        t.anchoredPosition = pos;
        t.sizeDelta = new Vector2(150, 150);
        t.localScale = Vector3.one;
        // Fundo escuro redondo + seta branca nítida (o ícone original era 32px escalado 9x).
        var img = t.GetComponent<Image>();
        img.sprite = roundSprite;
        img.type = Image.Type.Sliced;
        img.color = Pill;
        var oldIcon = t.Find("Icon");
        if (oldIcon != null) Object.DestroyImmediate(oldIcon.gameObject);
        Icon(t, "Icon", icon, new Vector2(0.5f, 0.5f), Vector2.zero, 90);
    }

    static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    static void FixEventSystem()
    {
        var es = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include);
        if (es == null) es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem)).GetComponent<UnityEngine.EventSystems.EventSystem>();
        var standalone = es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        if (standalone != null) Object.DestroyImmediate(standalone);
        if (es.GetComponent<InputSystemUIInputModule>() == null) es.gameObject.AddComponent<InputSystemUIInputModule>();
    }

    static void SetupScaler(GameObject canvasGo)
    {
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    // ---------------------------------------------------------------- cena da corrida

    static void BuildGameScene(Mats m, GameObject coinPrefab, RoadSegment segmentPrefab, Pickup[] pickups)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Sky;
        RenderSettings.fogStartDistance = 70f;
        RenderSettings.fogEndDistance = 175f;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.75f, 0.82f, 0.95f);
        RenderSettings.ambientEquatorColor = new Color(0.6f, 0.65f, 0.65f);
        RenderSettings.ambientGroundColor = new Color(0.35f, 0.35f, 0.3f);
        RenderSettings.skybox = null;

        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.3f;
        light.color = new Color(1f, 0.96f, 0.88f);
        light.shadows = LightShadows.Soft;
        light.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        RenderSettings.sun = light;

        var player = new GameObject("Player", typeof(Rigidbody), typeof(BoxCollider)).AddComponent<PlayerController>();

        var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Sky;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 220f;
        cam.fieldOfView = 60f;
        var follow = camGo.AddComponent<CameraFollow>();
        follow.target = player.transform;

        // Escudo (bolha) e chama do nitro presos ao carro do jogador.
        var bubble = Primitive(PrimitiveType.Sphere, "ShieldBubble", player.transform, m.bubble, new Vector3(0, 1.1f, 0), new Vector3(3.3f, 2.6f, 5.6f));
        bubble.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        bubble.SetActive(false);
        player.shieldBubble = bubble;
        var flame = BuildBurst("NitroFlame", player.transform, m.flame, Color.white, 0.35f, 14f, 0f);
        flame.transform.localPosition = new Vector3(0, 0.55f, -2.3f);
        flame.transform.localEulerAngles = new Vector3(0, 180f, 0);
        var fMain = flame.main;
        fMain.loop = true;
        fMain.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        fMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        var fEm = flame.emission;
        fEm.SetBursts(new ParticleSystem.Burst[0]);
        fEm.rateOverTime = 70f;
        var fShape = flame.shape;
        fShape.shapeType = ParticleSystemShapeType.Cone;
        fShape.angle = 12f;
        fShape.radius = 0.35f;
        player.nitroFlame = flame;

        // Linhas de velocidade na frente da câmera durante o nitro.
        var lines = BuildBurst("SpeedLines", camGo.transform, m.speedLine, Color.white, 1f, 70f, 0f);
        lines.transform.localPosition = new Vector3(0, 0, 30f);
        lines.transform.localEulerAngles = new Vector3(0, 180f, 0);
        var lMain = lines.main;
        lMain.loop = true;
        lMain.startLifetime = 0.45f;
        lMain.startSize3D = true;
        lMain.startSizeX = 0.04f;
        lMain.startSizeY = 0.04f;
        lMain.startSizeZ = new ParticleSystem.MinMaxCurve(2f, 4f);
        lMain.startRotation3D = false;
        lMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        lMain.maxParticles = 200;
        var lEm = lines.emission;
        lEm.SetBursts(new ParticleSystem.Burst[0]);
        lEm.rateOverTime = 90f;
        var lShape = lines.shape;
        lShape.shapeType = ParticleSystemShapeType.Box;
        lShape.scale = new Vector3(16f, 10f, 1f);
        var lRot = lines.rotationOverLifetime;
        lRot.enabled = false;
        var lSize = lines.sizeOverLifetime;
        lSize.enabled = false;
        lines.GetComponent<ParticleSystemRenderer>().alignment = ParticleSystemRenderSpace.Local;
        follow.speedLines = lines;
        camGo.transform.position = new Vector3(0, 6.5f, -10.5f);

        var track = new GameObject("Track").AddComponent<TrackGenerator>();
        track.segmentPrefab = segmentPrefab;
        track.player = player.transform;

        var traffic = new GameObject("Traffic").AddComponent<TrafficSpawner>();
        traffic.player = player;
        traffic.coinPrefab = coinPrefab;
        traffic.pickupPrefabs = pickups;
        traffic.blinkerMaterial = Mat("Blinker", new Color(1f, 0.6f, 0.1f), null, null, null, 0.3f, 0f, new Color(3f, 1.4f, 0.1f));

        var biomes = new GameObject("Biomes").AddComponent<BiomeManager>();
        biomes.track = track.transform;
        biomes.cam = cam;
        biomes.sun = light;
        biomes.grass = m.grass;
        biomes.leaves = m.leaves;
        biomes.leavesDark = m.leavesDark;

        var fx = new GameObject("FX").transform;
        var crash = BuildBurst("CrashFX", fx, m.debris, Color.white, 0.5f, 9f, 1.5f);
        var coinFx = BuildBurst("CoinFX", fx, m.coin, Color.white, 0.25f, 5f, 0.5f);
        var coinEmission = coinFx.emission;
        coinEmission.SetBursts(new ParticleSystem.Burst[0]);

        var gm = new GameObject("GameManager").AddComponent<GameManager>();
        gm.player = player;
        gm.traffic = traffic;
        gm.cameraFollow = follow;
        gm.crashEffect = crash;
        gm.coinEffect = coinFx;
        gm.biomes = biomes;
        gm.track = track;

        gm.ui = BuildGameUI(gm);

        EditorSceneManager.SaveScene(scene, GameScenePath);
    }

    static GameUI BuildGameUI(GameManager gm)
    {
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        SetupScaler(canvasGo);
        new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(InputSystemUIInputModule));

        var ui = canvasGo.AddComponent<GameUI>();
        var safe = Stretch(new GameObject("SafeArea", typeof(RectTransform)).transform as RectTransform, canvasGo.transform);
        safe.gameObject.AddComponent<SafeArea>();
        var top = new Vector2(0.5f, 1);
        var bottom = new Vector2(0.5f, 0);

        // HUD
        var hud = Stretch(NewRect("HUD"), safe);
        ui.hudPanel = hud.gameObject;
        IconButton(hud, "PauseButton", "pause", Pill, new Vector2(0, 1), new Vector2(100, -100), new Vector2(130, 130), gm.Pause);
        var scorePill = Panel(hud, "ScorePill", Pill, top, top, new Vector2(0, -100), new Vector2(420, 120));
        ui.scoreText = Text(scorePill, "Score", "0", 70, TextAlignmentOptions.Center, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        ui.multiplierText = Text(hud, "Multiplier", "x1", 60, TextAlignmentOptions.Center, Color.white, top, top, new Vector2(0, -200), new Vector2(300, 80));
        ui.comboBar = Bar(hud, "ComboBar", top, new Vector2(0, -250), new Vector2(300, 22), Pill, Gold);
        ui.coinsText = CoinPill(hud, new Vector2(1, 1), new Vector2(-40, -50));
        ui.powerUpText = Text(hud, "PowerUps", "", 40, TextAlignmentOptions.Right, new Color(0.6f, 0.85f, 1f),
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -180), new Vector2(420, 60), new Vector2(1, 1));

        var glow = Panel(hud, "NitroGlow", new Color(0.35f, 0.85f, 1f, 0.45f), bottom, bottom, new Vector2(0, 140), new Vector2(740, 120));
        glow.gameObject.AddComponent<Pulse>().amount = 0.04f;
        ui.nitroReadyGlow = glow.gameObject;
        ui.nitroFill = Bar(hud, "NitroBar", bottom, new Vector2(0, 140), new Vector2(700, 90), Pill, Blue);
        var nitroBack = ui.nitroFill.transform.parent;
        nitroBack.GetComponent<Image>().raycastTarget = true;
        var nitroButton = nitroBack.gameObject.AddComponent<Button>();
        nitroButton.transition = Selectable.Transition.None;
        UnityEventTools.AddPersistentListener(nitroButton.onClick, gm.TryNitro);
        ui.nitroLabel = Text(nitroBack, "Label", "NITRO", 46, TextAlignmentOptions.Center, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var popupRoot = Place(NewRect("Popups"), hud, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(1000, 300));
        var template = Text(popupRoot, "PopupTemplate", "RASPÃO!", 84, TextAlignmentOptions.Center, Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 260));
        template.enableAutoSizing = false;
        template.fontSize = 84;
        template.gameObject.SetActive(false);
        ui.popupRoot = popupRoot;

        // Início
        var start = Stretch(NewRect("StartPanel"), safe);
        ui.startPanel = start.gameObject;
        var tap = start.gameObject.AddComponent<Image>();
        tap.color = new Color(0, 0, 0, 0.001f);
        var tapButton = start.gameObject.AddComponent<Button>();
        tapButton.transition = Selectable.Transition.None;
        UnityEventTools.AddPersistentListener(tapButton.onClick, gm.StartRun);
        var tapText = Text(start, "TapText", "TOQUE PARA COMEÇAR", 76, TextAlignmentOptions.Center, Color.white,
            bottom, bottom, new Vector2(0, 1250), new Vector2(1000, 120));
        tapText.gameObject.AddComponent<Pulse>();
        ui.startBestText = Text(start, "Best", "", 46, TextAlignmentOptions.Center, Gold,
            bottom, bottom, new Vector2(0, 1150), new Vector2(900, 80));
        var hint = Panel(start, "TutorialHint", Pill, bottom, bottom, new Vector2(0, 930), new Vector2(940, 300));
        Icon(hint, "Left", "arrow_left", new Vector2(0, 0.5f), new Vector2(70, 60), 80);
        Icon(hint, "Right", "arrow_right", new Vector2(1, 0.5f), new Vector2(-70, 60), 80);
        Text(hint, "Label", "DESLIZE PARA TROCAR DE FAIXA\n<color=#FFD633>DESVIE EM CIMA DA HORA = RASPÃO</color>\n<color=#5AD8FF>DESLIZE PARA CIMA = NITRO</color>", 38,
            TextAlignmentOptions.Center, Color.white, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-180, -30));
        ui.tutorialHint = hint.gameObject;
        Button(start, "GarageButton", "GARAGEM", Blue, bottom, new Vector2(0, 170), new Vector2(420, 120), 46, gm.GoToGarage);

        // Pausa
        var pause = Stretch(NewRect("PausePanel"), safe.parent);
        ui.pausePanel = pause.gameObject;
        pause.gameObject.AddComponent<Image>().color = Dim;
        var pCard = Panel(pause, "Card", Card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 1330));
        Text(pCard, "Title", "PAUSADO", 90, TextAlignmentOptions.Center, Color.white, top, top, new Vector2(0, -110), new Vector2(760, 130));
        Button(pCard, "Resume", "CONTINUAR", Green, top, new Vector2(0, -300), new Vector2(620, 140), 56, gm.Resume);
        Button(pCard, "Restart", "REINICIAR", Blue, top, new Vector2(0, -470), new Vector2(620, 140), 56, gm.Restart);
        var sound = Button(pCard, "Sound", "SOM: LIGADO", Gray, top, new Vector2(0, -640), new Vector2(620, 140), 44, gm.ToggleSound);
        ui.soundText = sound.GetComponentInChildren<TMP_Text>();
        var vib = Button(pCard, "Vibration", "VIBRAÇÃO: LIGADA", Gray, top, new Vector2(0, -810), new Vector2(620, 140), 44, gm.ToggleVibration);
        ui.vibrationText = vib.GetComponentInChildren<TMP_Text>();
        Button(pCard, "Garage", "GARAGEM", Orange, top, new Vector2(0, -980), new Vector2(620, 140), 56, gm.GoToGarage);
        ui.privacyButton = Button(pCard, "Privacy", "PRIVACIDADE", Gray, top, new Vector2(0, -1150), new Vector2(620, 120), 40, gm.ShowPrivacyOptions).gameObject;

        // Fim de jogo
        var over = Stretch(NewRect("GameOverPanel"), safe.parent);
        ui.gameOverPanel = over.gameObject;
        over.gameObject.AddComponent<Image>().color = Dim;
        var oCard = Panel(over, "Card", Card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880, 1480));
        Text(oCard, "Title", "BATEU!", 96, TextAlignmentOptions.Center, new Color(1f, 0.4f, 0.35f), top, top, new Vector2(0, -100), new Vector2(800, 130));
        ui.goScoreText = Text(oCard, "Score", "0", 120, TextAlignmentOptions.Center, Color.white, top, top, new Vector2(0, -240), new Vector2(800, 150));
        var newBest = Panel(oCard, "NewBest", Gold, top, top, new Vector2(0, -345), new Vector2(460, 66));
        Text(newBest, "Label", "NOVO RECORDE!", 40, TextAlignmentOptions.Center, new Color(0.25f, 0.15f, 0f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        newBest.gameObject.AddComponent<Pulse>();
        ui.goNewBest = newBest.gameObject;
        ui.goBestText = Text(oCard, "Best", "RECORDE: 0", 42, TextAlignmentOptions.Center, new Color(0.8f, 0.85f, 0.95f), top, top, new Vector2(0, -425), new Vector2(800, 64));
        ui.goStatsText = Text(oCard, "Stats", "", 34, TextAlignmentOptions.Center, new Color(0.75f, 0.8f, 0.9f), top, top, new Vector2(0, -490), new Vector2(820, 60));
        ui.goCoinsText = Text(oCard, "Coins", "+0 moedas", 50, TextAlignmentOptions.Center, Gold, top, top, new Vector2(0, -565), new Vector2(800, 76));
        var revive = Button(oCard, "Revive", "CONTINUAR (VÍDEO)", Purple, top, new Vector2(0, -710), new Vector2(700, 140), 48, gm.Revive);
        revive.gameObject.AddComponent<Pulse>().amount = 0.03f;
        ui.reviveButton = revive.gameObject;
        ui.doubleButton = Button(oCard, "DoubleCoins", "DOBRAR MOEDAS (VÍDEO)", Orange, top, new Vector2(0, -870), new Vector2(700, 140), 44, gm.DoubleCoins).gameObject;
        Button(oCard, "Restart", "JOGAR DE NOVO", Green, top, new Vector2(0, -1030), new Vector2(700, 140), 56, gm.Restart);
        Button(oCard, "Garage", "GARAGEM", Blue, top, new Vector2(0, -1190), new Vector2(700, 140), 56, gm.GoToGarage);
        ui.missionsHint = Text(oCard, "MissionsHint", "MISSÃO CONCLUÍDA! RESGATE NA GARAGEM", 34, TextAlignmentOptions.Center, Gold,
            top, top, new Vector2(0, -1320), new Vector2(820, 60)).gameObject;

        pause.gameObject.SetActive(false);
        over.gameObject.SetActive(false);
        return ui;
    }

    /// <summary>Barra horizontal: fundo arredondado + imagem Filled por cima. Retorna a imagem de preenchimento.</summary>
    static Image Bar(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color bg, Color fill)
    {
        var back = Panel(parent, name, bg, anchor, anchor, pos, size);
        var f = Stretch(NewRect("Fill"), back);
        float inset = Mathf.Min(6f, size.y * 0.2f);
        f.offsetMin = new Vector2(inset, inset);
        f.offsetMax = new Vector2(-inset, -inset);
        var img = f.gameObject.AddComponent<Image>();
        img.sprite = roundSprite;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
        img.fillAmount = 0f;
        img.color = fill;
        img.raycastTarget = false;
        return img;
    }

    // ---------------------------------------------------------------- UI helpers

    static RectTransform NewRect(string name) => new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();

    static RectTransform Stretch(RectTransform rt, Transform parent)
    {
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    static RectTransform Place(RectTransform rt, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Vector2? pivot = null)
    {
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static RectTransform Panel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        var rt = Place(NewRect(name), parent, anchorMin, anchorMax, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = roundSprite;
        img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    static TMP_Text Text(Transform parent, string name, string text, float size, TextAlignmentOptions align, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 sizeDelta, Vector2? pivot = null)
    {
        var rt = Place(NewRect(name), parent, anchorMin, anchorMax, pos, sizeDelta, pivot);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.enableAutoSizing = true;
        t.fontSizeMin = size * 0.5f;
        t.fontSizeMax = size;
        t.alignment = align;
        t.color = color;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }


    static Button Button(Transform parent, string name, string label, Color color, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize, UnityAction onClick)
    {
        var rt = Place(NewRect(name), parent, anchor, anchor, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = roundSprite;
        img.type = Image.Type.Sliced;
        img.color = color;
        var b = rt.gameObject.AddComponent<Button>();
        var colors = b.colors;
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
        b.colors = colors;
        UnityEventTools.AddPersistentListener(b.onClick, onClick);
        Text(rt, "Label", label, fontSize, TextAlignmentOptions.Center, Color.white, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-40, -20));
        return b;
    }

    static Sprite IconSprite(string icon) =>
        AssetDatabase.LoadAssetAtPath<Sprite>($"{TwdDir}/Textures/icons/128x128/{icon}.png");

    static Image Icon(Transform parent, string name, string icon, Vector2 anchor, Vector2 pos, float size)
    {
        var rt = Place(NewRect(name), parent, anchor, anchor, pos, new Vector2(size, size));
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = IconSprite(icon);
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }

    static Button IconButton(Transform parent, string name, string icon, Color color, Vector2 anchor, Vector2 pos, Vector2 size, UnityAction onClick)
    {
        var rt = Place(NewRect(name), parent, anchor, anchor, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = roundSprite;
        img.type = Image.Type.Sliced;
        img.color = color;
        var b = rt.gameObject.AddComponent<Button>();
        UnityEventTools.AddPersistentListener(b.onClick, onClick);
        Icon(rt, "Icon", icon, new Vector2(0.5f, 0.5f), Vector2.zero, size.x * 0.55f);
        return b;
    }

    /// <summary>Contador de moedas: pílula escura com uma moeda dourada e o número.</summary>
    static TMP_Text CoinPill(Transform parent, Vector2 anchor, Vector2 pos)
    {
        var pill = Place(NewRect("CoinsPill"), parent, anchor, anchor, pos, new Vector2(300, 110), new Vector2(1, 1));
        var img = pill.gameObject.AddComponent<Image>();
        img.sprite = roundSprite;
        img.type = Image.Type.Sliced;
        img.color = Pill;
        img.raycastTarget = false;
        var coin = Place(NewRect("CoinIcon"), pill, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(65, 0), new Vector2(70, 70));
        var ci = coin.gameObject.AddComponent<Image>();
        ci.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        ci.color = Gold;
        ci.raycastTarget = false;
        var t = Text(pill, "Coins", "0", 60, TextAlignmentOptions.Right, Color.white, new Vector2(0, 0), new Vector2(1, 1), new Vector2(40, 0), new Vector2(-150, -10));
        return t;
    }

    // ---------------------------------------------------------------- projeto

    static void ConfigureProject()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(GarageScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true),
        };

        var android = NamedBuildTarget.Android;
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Low);
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        EditorUserBuildSettings.buildAppBundle = true;
    }
}
