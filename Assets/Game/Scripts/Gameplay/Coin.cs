using UnityEngine;

/// <summary>Moeda coletável. Gira sozinha; o TrafficSpawner reaproveita as instâncias.</summary>
public class Coin : MonoBehaviour
{
    public float spinSpeed = 180f;

    void Update()
    {
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
    }
}
