using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Repinta um carro em tempo de execução sem tocar nos assets.
/// - Materiais de cor sólida: troca o matiz das cores saturadas (lataria) e preserva preto/branco/cinza
///   (pneus, vidros, frisos).
/// - Materiais com textura-paleta: gera uma cópia da textura com o matiz trocado só nos pixels coloridos.
/// - Carro sem nenhuma cor saturada (ex.: viatura preto e branco): pinta o material claro dominante.
/// Guarda os materiais originais para poder voltar à pintura de fábrica.
/// </summary>
public class CarPaint : MonoBehaviour
{
    const float SaturatedMin = 0.28f;

    Renderer[] renderers;
    Material[][] originals;
    readonly List<Object> created = new List<Object>();
    static readonly Dictionary<(Texture, int), Texture2D> textureCache = new Dictionary<(Texture, int), Texture2D>();

    public static void Apply(GameObject car, int paintIndex)
    {
        if (car == null) return;
        var paint = car.GetComponent<CarPaint>();
        if (paint == null) paint = car.AddComponent<CarPaint>();
        paint.Paint(paintIndex);
    }

    void Paint(int paintIndex)
    {
        if (renderers == null)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            originals = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++) originals[i] = renderers[i].sharedMaterials;
        }
        Cleanup();
        for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterials = originals[i];
        if (paintIndex <= 0 || paintIndex >= Paints.All.Length) return;

        Color target = Paints.All[paintIndex].color;
        Color.RGBToHSV(target, out float targetHue, out float targetSat, out _);
        bool anySaturated = HasSaturatedColor();
        Material body = anySaturated ? null : DominantLightMaterial();

        var replaced = new Dictionary<Material, Material>();
        for (int i = 0; i < renderers.Length; i++)
        {
            var mats = (Material[])originals[i].Clone();
            for (int m = 0; m < mats.Length; m++)
            {
                var src = mats[m];
                if (src == null) continue;
                if (!replaced.TryGetValue(src, out var dst))
                {
                    dst = Recolor(src, src == body, targetHue, targetSat, target);
                    replaced[src] = dst;
                }
                mats[m] = dst;
            }
            renderers[i].sharedMaterials = mats;
        }
    }

    Material Recolor(Material src, bool isBody, float hue, float sat, Color target)
    {
        string colorProp = src.HasProperty("_BaseColor") ? "_BaseColor" : src.HasProperty("_Color") ? "_Color" : null;
        string texProp = src.HasProperty("_BaseMap") ? "_BaseMap" : src.HasProperty("_MainTex") ? "_MainTex" : null;
        Texture tex = texProp != null ? src.GetTexture(texProp) : null;
        Color color = colorProp != null ? src.GetColor(colorProp) : Color.white;

        Material dst = null;
        Material Copy() { if (dst == null) { dst = new Material(src); created.Add(dst); } return dst; }

        if (isBody)
        {
            Color.RGBToHSV(color, out _, out _, out float v);
            var c = Color.HSVToRGB(hue, sat, Mathf.Max(v, 0.6f));
            c.a = color.a;
            Copy().SetColor(colorProp, c);
            return dst;
        }

        Color.RGBToHSV(color, out float h, out float s, out float val);
        if (colorProp != null && s >= SaturatedMin)
        {
            var c = Color.HSVToRGB(hue, Mathf.Max(s, sat * 0.85f), val);
            c.a = color.a;
            Copy().SetColor(colorProp, c);
        }
        if (tex != null)
        {
            var recolored = RecolorTexture(tex, hue, sat);
            if (recolored != null) Copy().SetTexture(texProp, recolored);
        }
        return dst ?? src;
    }

    /// <summary>Cópia da textura com o matiz trocado nos pixels saturados (funciona mesmo sem Read/Write habilitado).</summary>
    static Texture2D RecolorTexture(Texture tex, float hue, float sat)
    {
        int key = Mathf.RoundToInt(hue * 360f);
        if (textureCache.TryGetValue((tex, key), out var cached) && cached != null) return cached;
        if (tex.width > 1024 || tex.height > 1024) return null; // só paletas pequenas; texturas grandes ficam como estão

        var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(tex, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false, false);
        copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        var px = copy.GetPixels32();
        for (int i = 0; i < px.Length; i++)
        {
            Color c = px[i];
            Color.RGBToHSV(c, out _, out float s, out float v);
            if (s < SaturatedMin || v < 0.15f) continue;
            var n = Color.HSVToRGB(hue, Mathf.Max(s, sat * 0.85f), v);
            n.a = c.a;
            px[i] = n;
        }
        copy.SetPixels32(px);
        copy.filterMode = tex.filterMode;
        copy.wrapMode = tex.wrapMode;
        copy.Apply(false, true);
        textureCache[(tex, key)] = copy;
        return copy;
    }

    bool HasSaturatedColor()
    {
        foreach (var mats in originals)
            foreach (var m in mats)
            {
                if (m == null) continue;
                if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) return true;
                string prop = m.HasProperty("_BaseColor") ? "_BaseColor" : m.HasProperty("_Color") ? "_Color" : null;
                if (prop == null) continue;
                Color.RGBToHSV(m.GetColor(prop), out _, out float s, out float v);
                if (s >= SaturatedMin && v > 0.2f) return true;
            }
        return false;
    }

    /// <summary>Material claro que cobre mais área (a lataria de um carro branco/cinza).</summary>
    Material DominantLightMaterial()
    {
        var area = new Dictionary<Material, float>();
        for (int i = 0; i < renderers.Length; i++)
        {
            float size = renderers[i].bounds.size.sqrMagnitude / Mathf.Max(1, originals[i].Length);
            foreach (var m in originals[i])
            {
                if (m == null) continue;
                string prop = m.HasProperty("_BaseColor") ? "_BaseColor" : m.HasProperty("_Color") ? "_Color" : null;
                if (prop == null) continue;
                Color.RGBToHSV(m.GetColor(prop), out _, out _, out float v);
                if (v < 0.35f) continue;
                area.TryGetValue(m, out float a);
                area[m] = a + size;
            }
        }
        Material best = null;
        float bestArea = 0f;
        foreach (var kv in area)
            if (kv.Value > bestArea) { bestArea = kv.Value; best = kv.Key; }
        return best;
    }

    void Cleanup()
    {
        foreach (var o in created) if (o != null) Destroy(o);
        created.Clear();
    }

    void OnDestroy() => Cleanup();
}
