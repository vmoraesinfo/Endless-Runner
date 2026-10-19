using UnityEngine;

/// <summary>Câmera atrás e acima do carro, abre o FOV com a velocidade/nitro e treme na batida.</summary>
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 6.5f, -10.5f);
    public float lookAhead = 9f;
    public float lookHeight = 1.2f;
    [Range(0f, 1f)] public float followX = 0.55f;
    public float smoothX = 8f;
    public float baseFov = 60f;
    public float maxFov = 72f;
    public float boostFov = 12f;

    [Tooltip("Linhas de velocidade ligadas durante o nitro (opcional).")]
    public ParticleSystem speedLines;

    Camera cam;
    float x;
    float shakeTime;
    float shakeStrength;
    float speed01;
    bool boost;
    float kick;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    /// <summary>speed01: 0 = velocidade inicial, 1 = máxima.</summary>
    public void SetSpeed01(float value) => speed01 = value;

    public void SetBoost(bool on)
    {
        boost = on;
        if (speedLines == null) return;
        if (on) speedLines.Play();
        else speedLines.Stop();
    }

    /// <summary>Pequeno "soco" de FOV, usado no raspão.</summary>
    public void Kick(float amount) => kick = Mathf.Max(kick, amount);

    public void Shake(float duration, float strength)
    {
        shakeTime = duration;
        shakeStrength = strength;
    }

    void LateUpdate()
    {
        if (target == null) return;

        kick = Mathf.MoveTowards(kick, 0f, Time.deltaTime * 20f);
        if (cam != null)
        {
            float fov = Mathf.Lerp(baseFov, maxFov, speed01) + (boost ? boostFov : 0f) + kick;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 4f * Time.deltaTime);
        }

        x = Mathf.Lerp(x, target.position.x * followX, smoothX * Time.deltaTime);
        Vector3 basePos = new Vector3(x, target.position.y, target.position.z);
        Vector3 pos = basePos + offset;

        if (shakeTime > 0f)
        {
            shakeTime -= Time.unscaledDeltaTime;
            pos += Random.insideUnitSphere * shakeStrength;
        }

        transform.position = pos;
        transform.LookAt(basePos + new Vector3(0f, lookHeight, lookAhead));
    }
}
