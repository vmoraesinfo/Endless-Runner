using UnityEngine;

/// <summary>Pisca-pisca laranja nos cantos de um veículo do trânsito (avisa a troca de faixa).</summary>
public class Blinker : MonoBehaviour
{
    public GameObject left;
    public GameObject right;
    int side;

    /// <summary>Cria as duas luzes (frente e traseira de cada lado) a partir da caixa do veículo.</summary>
    public static Blinker Attach(GameObject car, Bounds localBounds, Material mat)
    {
        var b = car.AddComponent<Blinker>();
        b.left = MakeSide(car.transform, localBounds, mat, -1f);
        b.right = MakeSide(car.transform, localBounds, mat, 1f);
        b.SetSide(0);
        return b;
    }

    static GameObject MakeSide(Transform parent, Bounds lb, Material mat, float sign)
    {
        var root = new GameObject(sign < 0 ? "BlinkerL" : "BlinkerR");
        root.transform.SetParent(parent, false);
        float x = lb.center.x + sign * (lb.extents.x + 0.02f);
        float y = lb.min.y + lb.size.y * 0.45f;
        foreach (float z in new[] { lb.max.z - 0.35f, lb.min.z + 0.35f })
        {
            var light = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(light.GetComponent<Collider>());
            light.transform.SetParent(root.transform, false);
            light.transform.localPosition = new Vector3(x, y, z);
            light.transform.localScale = new Vector3(0.12f, 0.22f, 0.35f);
            var r = light.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        return root;
    }

    /// <summary>-1 = esquerda, 1 = direita, 0 = apagado.</summary>
    public void SetSide(int s)
    {
        side = s;
        if (s == 0)
        {
            left.SetActive(false);
            right.SetActive(false);
        }
    }

    void Update()
    {
        if (side == 0) return;
        bool on = Mathf.Repeat(Time.time * 3f, 1f) < 0.55f;
        left.SetActive(side < 0 && on);
        right.SetActive(side > 0 && on);
    }
}
