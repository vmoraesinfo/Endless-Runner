using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Segunda parte do GameBuilder: pistas (Rodovia + Cidade), obras, trânsito da cidade,
/// e os carros novos da garagem. Usa os helpers privados do GameBuilder (classe parcial).
/// </summary>
public static partial class GameBuilder
{
    const string CityDir = "Assets/Low Poly Simple Urban City 3D Asset Pack/Prefabs";
    const string AwbDir = "Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs";
    const string UnityCarsDir = "Assets/Unity Technologies/CarsAssetPack/Prefabs/FBX";

    // Carros da garagem: prefab → (nome exibido, preço). Ordem = ordem na garagem (do mais barato ao mais caro).
    static readonly (string path, string name, int price)[] GarageCars =
    {
        ("Assets/Low Poly Cars/Models/Sports Racing Car.fbx", "Esportivo Neon", 0),
        ("Assets/Low Poly Cars/Models/Police Monster Truck.fbx", "Monster Polícia", 150),
        ("Assets/Low Poly Cars/Models/Standard Racing Car.fbx", "Corrida", 300),
        ("Assets/Low Poly Cars/Models/Sports Monster Truck.fbx", "Monster Esportivo", 500),
        ("Assets/Low Poly Cars/Models/Standard Police Car.fbx", "Viatura", 800),
        ("Assets/Low Poly Cars/Models/Super Police Car.fbx", "Super Viatura", 1200),
        (AwbDir + "/Hatchback Car_15.prefab", "Hatch", 1500),
        (AwbDir + "/Classic Car_9.prefab", "Clássico", 1800),
        (AwbDir + "/Pick Up_11.prefab", "Picape", 2200),
        (AwbDir + "/N Van_10.prefab", "Van", 2500),
        (AwbDir + "/N_Muscle Car_10.prefab", "Muscle", 3000),
        (AwbDir + "/Police Car N_4.prefab", "Patrulha", 3500),
        (AwbDir + "/Sport Car_39.prefab", "Esportivo GT", 4000),
        (AwbDir + "/Monster Truck_12.prefab", "Monster", 4500),
        (AwbDir + "/Military Vehicle_3.prefab", "Blindado", 5000),
        (UnityCarsDir + "/Car1.prefab", "Muscle Laranja", 6000),
        (UnityCarsDir + "/Car2.prefab", "Muscle Noturno", 7000),
        (UnityCarsDir + "/Car8.prefab", "Jipe", 8000),
        (UnityCarsDir + "/Car5.prefab", "Clássico Azul", 9000),
        (UnityCarsDir + "/Car4.prefab", "GT Turbo", 10000),
        (UnityCarsDir + "/Car3.prefab", "Hyper GT", 12000),
        (UnityCarsDir + "/Car6.prefab", "Super V12", 15000),
        (UnityCarsDir + "/Car7.prefab", "Fórmula", 20000),
    };

    static (string name, int price)? GarageInfo(GameObject prefab)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        foreach (var c in GarageCars)
            if (c.path == path) return (c.name, c.price);
        return null;
    }

    // ---------------------------------------------------------------- garagem

    /// <summary>Coloca na garagem os carros que ainda não estão lá, no mesmo ponto e tamanho do primeiro.</summary>
    static void AddGarageCars(Transform holder)
    {
        // Os carros antigos vinham sobre um disco de exposição; esconde para todos ficarem iguais.
        foreach (Transform child in holder)
            foreach (var t in child.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Ground")) t.gameObject.SetActive(false);

        var reference = holder.GetChild(0);
        Bounds refBounds = RendererBounds(reference.gameObject);
        var present = new HashSet<string>();
        foreach (Transform child in holder)
        {
            var src = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
            if (src != null) present.Add(AssetDatabase.GetAssetPath(src));
        }

        foreach (var car in GarageCars)
        {
            if (present.Contains(car.path)) continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(car.path);
            if (prefab == null) { Debug.LogWarning("[GameBuilder] Carro não encontrado: " + car.path); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
            go.name = prefab.name;
            go.transform.localPosition = reference.localPosition;
            go.transform.localRotation = reference.localRotation;
            go.transform.localScale = Vector3.one;
            Bounds b = RendererBounds(go);
            float s = refBounds.size.z / Mathf.Max(b.size.z, 0.01f);
            go.transform.localScale = Vector3.one * s;
            b = RendererBounds(go);
            go.transform.position += new Vector3(refBounds.center.x - b.center.x, refBounds.min.y - b.min.y, refBounds.center.z - b.center.z);
            go.SetActive(false);
        }

        // Mantém a ordem da lista (preço crescente).
        var order = GarageCars.Select(c => c.path).ToList();
        var children = holder.Cast<Transform>().ToList();
        children.Sort((a, c) => Index(a).CompareTo(Index(c)));
        for (int i = 0; i < children.Count; i++) children[i].SetSiblingIndex(i);

        int Index(Transform t)
        {
            var src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
            int i = src != null ? order.IndexOf(AssetDatabase.GetAssetPath(src)) : -1;
            return i < 0 ? int.MaxValue : i;
        }
    }

    /// <summary>Caixa dos renderers ativos (ignora o disco "Ground" desativado).</summary>
    static Bounds RendererBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(false);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    /// <summary>Botão que alterna a pista, logo acima do botão principal.</summary>
    static void AddMapButton(RectTransform ui, SelectCars select)
    {
        var button = Button(ui, "MapButton", "PISTA: RODOVIA", new Color(0.18f, 0.24f, 0.36f, 0.95f),
            new Vector2(0.5f, 0), new Vector2(0, 500), new Vector2(860, 110), 40, select.NextMap);
        select.mapText = button.GetComponentInChildren<TMP_Text>();
    }

    // ---------------------------------------------------------------- obras

    static GameObject CityPrefab(string relative) =>
        AssetDatabase.LoadAssetAtPath<GameObject>($"{CityDir}/{relative}.prefab");

    static GameObject Place(GameObject prefab, Transform parent, Vector3 pos, float yRot = 0f, float scale = 1f)
    {
        if (prefab == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
        go.transform.localScale = Vector3.one * scale;
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        return go;
    }

    /// <summary>Obstáculos de obra que ocupam uma faixa inteira. Já saem com Obstacle + colisor.</summary>
    static GameObject[] BuildRoadworks()
    {
        var list = new List<GameObject>();
        var barrier = CityPrefab("Props/Other/Road_Barrier");
        var cone = CityPrefab("Props/Other/Cone");
        var sign = CityPrefab("Props/Road_Signs/Road_Work_Sign_1");
        var barrel = CityPrefab("Props/Other/Fuel_Barrel");
        var fence = CityPrefab("Props/Other/Barrier");
        if (barrier == null || cone == null) { Debug.LogWarning("[GameBuilder] Props de obra da cidade não encontrados."); return list.ToArray(); }

        list.Add(SaveRoadwork("RoadworkBarrier", root =>
        {
            Place(barrier, root, new Vector3(0, 0, 0.8f), 0f, 1.25f);
            Place(cone, root, new Vector3(-1.1f, 0, -0.8f), 0f, 1.6f);
            Place(cone, root, new Vector3(1.1f, 0, -0.8f), 0f, 1.6f);
            Place(cone, root, new Vector3(0f, 0, -1.6f), 0f, 1.6f);
        }));
        list.Add(SaveRoadwork("RoadworkSign", root =>
        {
            Place(sign ?? barrier, root, new Vector3(0, 0, 0.6f), 0f, 1.4f);
            Place(barrel ?? cone, root, new Vector3(-1.1f, 0, -0.6f), 0f, 1.2f);
            Place(barrel ?? cone, root, new Vector3(1.1f, 0, -0.6f), 0f, 1.2f);
            Place(cone, root, new Vector3(0f, 0, -1.5f), 0f, 1.6f);
        }));
        if (fence != null)
        {
            list.Add(SaveRoadwork("RoadworkFence", root =>
            {
                Place(fence, root, new Vector3(0, 0, 0.5f), 0f, 0.55f);
                Place(cone, root, new Vector3(-0.9f, 0, -1f), 0f, 1.6f);
                Place(cone, root, new Vector3(0.9f, 0, -1f), 0f, 1.6f);
            }));
        }
        return list.ToArray();
    }

    static GameObject SaveRoadwork(string name, System.Action<Transform> build)
    {
        var root = new GameObject(name);
        build(root.transform);
        Bounds b = RendererBounds(root);
        var obstacle = root.AddComponent<Obstacle>();
        obstacle.length = b.size.z;
        var box = root.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, b.center.y, b.center.z);
        box.size = new Vector3(Mathf.Min(b.size.x, 2.6f), b.size.y, b.size.z);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{name}.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ---------------------------------------------------------------- cidade

    static RoadSegment BuildCitySegment(Mats m)
    {
        const float len = 24f;
        var sidewalk = Mat("Sidewalk", new Color(0.72f, 0.72f, 0.74f), null, null, null, 0.1f);
        var curb = Mat("Curb", new Color(0.55f, 0.55f, 0.58f), null, null, null, 0.1f);
        var ground = Mat("CityGround", new Color(0.42f, 0.44f, 0.47f), null, null, null, 0.05f);

        var root = new GameObject("CitySegment");
        var seg = root.AddComponent<RoadSegment>();
        seg.segmentLength = len;
        seg.decorations = new Transform[0];
        var t = root.transform;

        Primitive(PrimitiveType.Quad, "Road", t, m.road, new Vector3(0f, 0f, len / 2f), new Vector3(12f, len, 1f), new Vector3(90f, 0f, 0f));
        foreach (float side in new[] { -1f, 1f })
        {
            string s = side < 0 ? "L" : "R";
            Primitive(PrimitiveType.Cube, "Curb" + s, t, curb, new Vector3(side * 6.15f, 0.1f, len / 2f), new Vector3(0.3f, 0.2f, len));
            Primitive(PrimitiveType.Cube, "Sidewalk" + s, t, sidewalk, new Vector3(side * 8f, 0.08f, len / 2f), new Vector3(3.4f, 0.16f, len));
            Primitive(PrimitiveType.Quad, "Ground" + s, t, ground, new Vector3(side * 30f, -0.02f, len / 2f), new Vector3(40f, len, 1f), new Vector3(90f, 0f, 0f));
        }
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.Off;

        var slots = new List<Transform>();
        var buildings = Enumerable.Range(1, 9).Select(i => CityPrefab($"Buildings/Building_{i}")).Where(p => p != null).ToArray();
        // Dois lotes de prédio por lado; cada lote tem todos os prédios como variação.
        foreach (float side in new[] { -1f, 1f })
        {
            foreach (float z in new[] { 6f, 18f })
            {
                var slot = new GameObject($"Lot{(side < 0 ? "L" : "R")}{z}").transform;
                slot.SetParent(t, false);
                slot.localPosition = new Vector3(side * 10f, 0f, z);
                foreach (var b in buildings)
                {
                    // Frente (+Z do modelo) virada para a rua.
                    var go = Place(b, slot, Vector3.zero, side < 0 ? 90f : -90f);
                    Bounds bb = RendererBounds(go);
                    // Encosta a fachada no fim da calçada (x = ±10) e centraliza no lote.
                    float nearEdge = side < 0 ? bb.max.x : bb.min.x;
                    go.transform.position += new Vector3(slot.position.x - nearEdge, -bb.min.y, slot.position.z - bb.center.z);
                }
                slots.Add(slot);
            }
        }

        // Postes fixos e um objeto de calçada sorteado por lado.
        var lamp = CityPrefab("Props/Road_Signs/Light");
        var props = new[] { "Props/Other/Hydrant", "Props/Other/Trash_Small", "Props/Other/Bus Stop", "Props/Other/Tree_1",
            "Props/Other/Tree_2", "Props/Other/Hot_Dog", "Props/Other/Newspaper_Stand", "Props/Other/Trash_Big" }
            .Select(CityPrefab).Where(p => p != null).ToArray();
        foreach (float side in new[] { -1f, 1f })
        {
            Place(lamp, t, new Vector3(side * 6.9f, 0.16f, 0f), side < 0 ? 90f : -90f);
            var slot = new GameObject($"Prop{(side < 0 ? "L" : "R")}").transform;
            slot.SetParent(t, false);
            slot.localPosition = new Vector3(side * 8.4f, 0.16f, 12f);
            foreach (var p in props) Place(p, slot, Vector3.zero, side < 0 ? 90f : -90f);
            slots.Add(slot);
        }
        seg.variantSlots = slots.ToArray();
        seg.variantChance = 1f;

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/CitySegment.prefab");
        Object.DestroyImmediate(root);
        return prefab.GetComponent<RoadSegment>();
    }

    static readonly (string path, float weight)[] CityVehicles =
    {
        ("Vehicles/Cars/Car_1_1", 2f), ("Vehicles/Cars/Car_1_2", 2f), ("Vehicles/Cars/Car_1_3", 2f),
        ("Vehicles/Cars/Car_2_1", 2f), ("Vehicles/Cars/Car_2_2", 2f), ("Vehicles/Cars/Car_2_3", 2f),
        ("Vehicles/Cars/Car_3_1", 2f), ("Vehicles/Cars/Car_3_2", 2f), ("Vehicles/Cars/Car_3_3", 2f),
        ("Vehicles/Emergency_Vehicles/Taxi", 4f), ("Vehicles/Emergency_Vehicles/Police", 1f),
        ("Vehicles/Emergency_Vehicles/Ambulance", 1f), ("Vehicles/Emergency_Vehicles/Fire_Truck", 0.5f),
        ("Vehicles/Buses/Bus_1", 1f), ("Vehicles/Buses/Bus_2", 1f), ("Vehicles/Buses/Bus_3", 1f),
        ("Vehicles/Pickups/Pickup_1", 1f), ("Vehicles/Pickups/Pickup_2", 1f), ("Vehicles/Pickups/Pickup_3", 1f),
        ("Vehicles/Trucks/Truck_1", 1f), ("Vehicles/Trucks/Truck_2", 1f), ("Vehicles/Trucks/Truck_3", 1f),
    };

    static TrafficCatalog BuildCityTraffic()
    {
        var catalog = LoadOrCreate<TrafficCatalog>(ResDir + "/TrafficCatalogCity.asset");
        catalog.scale = 0.9f;
        catalog.maxWidth = 2.6f;
        catalog.vehicles.Clear();
        foreach (var (path, weight) in CityVehicles)
        {
            var prefab = CityPrefab(path);
            if (prefab != null) catalog.vehicles.Add(new TrafficCatalog.Entry { prefab = prefab, weight = weight });
        }
        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    // ---------------------------------------------------------------- pistas

    static void BuildMapCatalog(RoadSegment highway, RoadSegment city, GameObject[] roadworks)
    {
        var catalog = LoadOrCreate<MapCatalog>(ResDir + "/MapCatalog.asset");
        catalog.maps.Clear();
        catalog.maps.Add(new MapCatalog.Map
        {
            id = "rodovia", displayName = "RODOVIA", unlockDistance = 0, coinValue = 1,
            segmentPrefab = highway, biomes = new BiomeManager.Biome[0],
            traffic = AssetDatabase.LoadAssetAtPath<TrafficCatalog>(ResDir + "/TrafficCatalog.asset"),
            trafficSpeed = 6f, laneChangeChance = 0.35f, roadworksChance = 0f, roadworkPrefabs = new GameObject[0],
        });
        catalog.maps.Add(new MapCatalog.Map
        {
            id = "cidade", displayName = "CIDADE", unlockDistance = 1500, coinValue = 2,
            segmentPrefab = city,
            biomes = new[]
            {
                new BiomeManager.Biome { name = "CIDADE", sky = new Color(0.66f, 0.80f, 0.93f), ground = Color.white,
                    leaves = new Color(0.36f, 0.62f, 0.3f), leavesDark = new Color(0.2f, 0.45f, 0.28f) },
                new BiomeManager.Biome { name = "ENTARDECER", sky = new Color(0.98f, 0.66f, 0.45f), ground = Color.white,
                    leaves = new Color(0.45f, 0.55f, 0.3f), leavesDark = new Color(0.3f, 0.4f, 0.25f),
                    light = new Color(1f, 0.75f, 0.55f), lightIntensity = 1.1f, ambient = new Color(0.8f, 0.6f, 0.55f) },
                new BiomeManager.Biome { name = "CIDADE À NOITE", sky = new Color(0.08f, 0.09f, 0.2f), ground = Color.white,
                    leaves = new Color(0.1f, 0.25f, 0.18f), leavesDark = new Color(0.06f, 0.18f, 0.14f),
                    light = new Color(0.6f, 0.65f, 1f), lightIntensity = 0.5f, ambient = new Color(0.3f, 0.32f, 0.5f) },
            },
            traffic = BuildCityTraffic(),
            // Cidade congestionada: trânsito parado, obras e carros saindo da vaga com pisca.
            trafficSpeed = 0f, laneChangeChance = 0.3f, roadworksChance = 0.3f, roadworkPrefabs = roadworks,
        });
        EditorUtility.SetDirty(catalog);
    }
}
