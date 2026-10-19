using UnityEngine;

public enum PickupType { Magnet, Shield }

/// <summary>Power-up na pista. Flutua e gira; o TrafficSpawner reaproveita as instâncias.</summary>
public class Pickup : MonoBehaviour
{
    public PickupType type;
    public Transform visual;

    void Update()
    {
        if (visual == null) return;
        visual.Rotate(0f, 120f * Time.deltaTime, 0f, Space.World);
        visual.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 4f) * 0.2f, 0f);
    }
}
