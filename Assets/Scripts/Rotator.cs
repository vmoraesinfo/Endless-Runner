using UnityEngine;

public class Rotator : MonoBehaviour
{
    public float speed = 10f; // Fixed: added semicolon

    void Update()
    {
        this.transform.Rotate(0, speed * Time.deltaTime, 0);
    }
}
