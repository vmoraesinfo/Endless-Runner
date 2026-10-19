using UnityEngine;

/// <summary>
/// Instancia um prefab de carro normalizado: comprimento fixo, apoiado no chão,
/// centralizado na origem e virado para +Z. Os modelos do pack vêm em escalas diferentes.
/// </summary>
public static class CarVisual
{
    public static GameObject Spawn(GameObject prefab, Transform parent, float targetLength)
    {
        var root = new GameObject(prefab.name);
        root.transform.SetParent(parent, false);

        var model = Object.Instantiate(prefab, root.transform);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        // Os prefabs do pack vêm sobre um disco de exposição ("Ground"); na pista ele não existe.
        foreach (var t in model.GetComponentsInChildren<Transform>(true))
        {
            if (t != null && t.name.StartsWith("Ground")) Object.DestroyImmediate(t.gameObject);
        }
        foreach (var rotator in model.GetComponentsInChildren<Rotator>()) Object.Destroy(rotator);
        foreach (var col in model.GetComponentsInChildren<Collider>()) Object.Destroy(col);

        Bounds b = WorldBounds(model);
        float length = Mathf.Max(b.size.z, 0.01f);
        float scale = targetLength / length;
        model.transform.localScale *= scale;

        b = WorldBounds(model);
        Vector3 offset = root.transform.position - new Vector3(b.center.x, b.min.y, b.center.z);
        model.transform.position += offset;
        return root;
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
