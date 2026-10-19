using System;
using UnityEngine;

/// <summary>
/// Troca a paisagem a cada `biomeLength` metros (Campo → Deserto → Neve → Noite → ...),
/// fazendo uma transição suave de céu, neblina, luz e cores do chão/árvores.
/// Trabalha em cópias dos materiais para não alterar os assets do projeto.
/// </summary>
[DefaultExecutionOrder(100)]
public class BiomeManager : MonoBehaviour
{
    [Serializable]
    public class Biome
    {
        public string name;
        public Color sky;
        public Color ground;
        public bool groundTexture;
        public Color leaves;
        public Color leavesDark;
        public Color light = Color.white;
        public float lightIntensity = 1.3f;
        public Color ambient = new Color(0.7f, 0.75f, 0.85f);
    }

    public Transform track;
    public Camera cam;
    public Light sun;
    public Material grass, leaves, leavesDark;
    public float biomeLength = 1200f;
    public float transitionSeconds = 2.5f;

    public Biome[] biomes =
    {
        new Biome { name = "CAMPO", sky = new Color(0.62f, 0.80f, 0.95f), ground = new Color(0.75f, 0.9f, 0.7f), groundTexture = true,
            leaves = new Color(0.30f, 0.62f, 0.25f), leavesDark = new Color(0.16f, 0.45f, 0.26f) },
        new Biome { name = "DESERTO", sky = new Color(0.98f, 0.80f, 0.55f), ground = new Color(0.90f, 0.74f, 0.48f),
            leaves = new Color(0.55f, 0.62f, 0.25f), leavesDark = new Color(0.35f, 0.55f, 0.30f),
            light = new Color(1f, 0.88f, 0.7f), lightIntensity = 1.45f, ambient = new Color(0.85f, 0.75f, 0.65f) },
        new Biome { name = "NEVE", sky = new Color(0.80f, 0.86f, 0.93f), ground = new Color(0.93f, 0.95f, 0.98f),
            leaves = new Color(0.85f, 0.9f, 0.95f), leavesDark = new Color(0.20f, 0.38f, 0.32f),
            light = new Color(0.9f, 0.95f, 1f), lightIntensity = 1.2f, ambient = new Color(0.75f, 0.8f, 0.9f) },
        new Biome { name = "NOITE", sky = new Color(0.07f, 0.08f, 0.20f), ground = new Color(0.12f, 0.22f, 0.16f),
            leaves = new Color(0.10f, 0.25f, 0.18f), leavesDark = new Color(0.06f, 0.18f, 0.14f),
            light = new Color(0.55f, 0.6f, 1f), lightIntensity = 0.45f, ambient = new Color(0.25f, 0.28f, 0.45f) },
    };

    public event Action<string> BiomeChanged;

    Material grassRt, leavesRt, leavesDarkRt;
    Texture grassTex;
    int current = -1;
    Biome from, to;
    float blend = 1f;

    void Start()
    {
        grassRt = new Material(grass);
        leavesRt = new Material(leaves);
        leavesDarkRt = new Material(leavesDark);
        grassTex = grass.GetTexture("_BaseMap");

        // Os segmentos já foram instanciados pelo TrackGenerator (Start dele roda antes por ordem de execução).
        foreach (var r in track.GetComponentsInChildren<Renderer>(true))
        {
            var m = r.sharedMaterial;
            if (m == grass) r.sharedMaterial = grassRt;
            else if (m == leaves) r.sharedMaterial = leavesRt;
            else if (m == leavesDark) r.sharedMaterial = leavesDarkRt;
        }

        current = 0;
        from = to = biomes[0];
        Apply(to, to, 1f);
    }

    void OnDestroy()
    {
        Destroy(grassRt);
        Destroy(leavesRt);
        Destroy(leavesDarkRt);
    }

    /// <summary>Chamado pelo GameManager com a distância percorrida na corrida.</summary>
    public void UpdateDistance(float distance)
    {
        int index = Mathf.FloorToInt(distance / biomeLength) % biomes.Length;
        if (index != current && current >= 0)
        {
            from = Snapshot();
            to = biomes[index];
            current = index;
            blend = 0f;
            BiomeChanged?.Invoke(to.name);
        }

        if (blend < 1f)
        {
            blend = Mathf.Min(1f, blend + Time.deltaTime / transitionSeconds);
            Apply(from, to, Mathf.SmoothStep(0f, 1f, blend));
        }
    }

    Biome Snapshot() => new Biome
    {
        sky = cam.backgroundColor,
        ground = grassRt.GetColor("_BaseColor"),
        groundTexture = grassRt.GetTexture("_BaseMap") != null,
        leaves = leavesRt.GetColor("_BaseColor"),
        leavesDark = leavesDarkRt.GetColor("_BaseColor"),
        light = sun.color,
        lightIntensity = sun.intensity,
        ambient = RenderSettings.ambientSkyColor,
    };

    void Apply(Biome a, Biome b, float k)
    {
        Color sky = Color.Lerp(a.sky, b.sky, k);
        cam.backgroundColor = sky;
        RenderSettings.fogColor = sky;
        grassRt.SetColor("_BaseColor", Color.Lerp(a.ground, b.ground, k));
        // A textura de grama só fica no bioma que usa; nos outros o chão é cor sólida (estilo low-poly).
        grassRt.SetTexture("_BaseMap", (k < 0.5f ? a.groundTexture : b.groundTexture) ? grassTex : null);
        leavesRt.SetColor("_BaseColor", Color.Lerp(a.leaves, b.leaves, k));
        leavesDarkRt.SetColor("_BaseColor", Color.Lerp(a.leavesDark, b.leavesDark, k));
        sun.color = Color.Lerp(a.light, b.light, k);
        sun.intensity = Mathf.Lerp(a.lightIntensity, b.lightIntensity, k);
        RenderSettings.ambientSkyColor = Color.Lerp(a.ambient, b.ambient, k);
        RenderSettings.ambientEquatorColor = Color.Lerp(a.ambient, b.ambient, k) * 0.8f;
    }
}
