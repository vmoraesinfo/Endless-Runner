using UnityEngine;

/// <summary>
/// Instancia um prefab de carro normalizado: comprimento fixo, apoiado no chão,
/// centralizado na origem e virado para +Z. Os modelos do pack vêm em escalas diferentes.
/// </summary>
public static class CarVisual
{
    /// <summary>Carro com comprimento fixo (carros do jogador, que vêm de packs com escalas diferentes).</summary>
    public static GameObject Spawn(GameObject prefab, Transform parent, float targetLength)
    {
        var root = Instantiate(prefab, parent, out var model);
        Bounds b = WorldBounds(model);
        model.transform.localScale *= targetLength / Mathf.Max(b.size.z, 0.01f);
        Ground(root, model);
        return root;
    }

    /// <summary>Veículo com escala fixa, mantendo a proporção real (trânsito), limitado a uma largura máxima.</summary>
    public static GameObject SpawnScaled(GameObject prefab, Transform parent, float scale, float maxWidth)
    {
        var root = Instantiate(prefab, parent, out var model);
        Bounds b = WorldBounds(model);
        float width = b.size.x * scale;
        if (width > maxWidth) scale *= maxWidth / width;
        model.transform.localScale *= scale;
        Ground(root, model);
        return root;
    }

    static GameObject Instantiate(GameObject prefab, Transform parent, out GameObject model)
    {
        var root = new GameObject(prefab.name);
        root.transform.SetParent(parent, false);

        model = Object.Instantiate(prefab, root.transform);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        // Os prefabs do pack Low Poly Cars vêm sobre um disco de exposição ("Ground"); na pista ele não existe.
        foreach (var t in model.GetComponentsInChildren<Transform>(true))
        {
            if (t != null && t.name.StartsWith("Ground")) Object.DestroyImmediate(t.gameObject);
        }
        foreach (var rotator in model.GetComponentsInChildren<Rotator>()) Object.Destroy(rotator);
        foreach (var col in model.GetComponentsInChildren<Collider>()) Object.Destroy(col);
        foreach (var rb in model.GetComponentsInChildren<Rigidbody>()) Object.Destroy(rb);
        return root;
    }

    /// <summary>Apoia o modelo no chão e centraliza na origem do root.</summary>
    static void Ground(GameObject root, GameObject model)
    {
        Bounds b = WorldBounds(model);
        Vector3 offset = root.transform.position - new Vector3(b.center.x, b.min.y, b.center.z);
        model.transform.position += offset;
    }

    /// <summary>Caixa local (em relação a root) que envolve todos os renderers.</summary>
    public static Bounds LocalBounds(GameObject root)
    {
        Bounds world = WorldBounds(root);
        Vector3 center = root.transform.InverseTransformPoint(world.center);
        Vector3 lossy = root.transform.lossyScale;
        Vector3 size = new Vector3(world.size.x / lossy.x, world.size.y / lossy.y, world.size.z / lossy.z);
        return new Bounds(center, size);
    }

    static Bounds WorldBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }
}
