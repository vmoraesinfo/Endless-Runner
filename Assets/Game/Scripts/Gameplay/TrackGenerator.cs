using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Estrada infinita: um conjunto fixo de segmentos que são reposicionados à frente
/// quando ficam para trás do jogador (sem Instantiate/Destroy durante a corrida).
/// </summary>
public class TrackGenerator : MonoBehaviour
{
    public RoadSegment segmentPrefab;
    public Transform player;
    public float segmentLength = 24f;
    public int segmentsAhead = 9;
    public int segmentsBehind = 2;

    readonly Queue<RoadSegment> segments = new Queue<RoadSegment>();
    float nextZ;

    void Start()
    {
        nextZ = player.position.z - segmentsBehind * segmentLength;
        for (int i = 0; i < segmentsAhead + segmentsBehind; i++)
        {
            var seg = Instantiate(segmentPrefab, transform);
            Place(seg);
        }
    }

    void Update()
    {
        var first = segments.Peek();
        if (first.transform.position.z + segmentLength < player.position.z - segmentsBehind * segmentLength)
        {
            segments.Dequeue();
            Place(first);
        }
    }

    void Place(RoadSegment seg)
    {
        seg.transform.position = new Vector3(0f, 0f, nextZ);
        seg.Randomize();
        nextZ += segmentLength;
        segments.Enqueue(seg);
    }
}
