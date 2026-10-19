using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Carro do jogador: anda em 3 faixas, troca de faixa por swipe (celular) ou setas/A-D (teclado).
/// O avanço para frente é feito pelo GameManager via MoveForward.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public class PlayerController : MonoBehaviour
{
    public const int LaneCount = 3;

    [Header("Faixas")]
    public float laneWidth = 3.6f;
    public float laneChangeSpeed = 24f;
    public float maxTilt = 14f;

    [Header("Swipe")]
    [Tooltip("Distância mínima do swipe em fração da largura da tela.")]
    public float swipeThreshold = 0.05f;

    [Header("Visual")]
    public float carLength = 4.4f;

    public int Lane { get; private set; } = 1;
    public bool InputEnabled { get; set; }
    public Transform Visual => visual;

    Transform visual;
    BoxCollider box;
    Vector2 pressStart;
    bool pressing;
    bool swipeConsumed;
    float blinkUntil;

    void Awake()
    {
        box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        var rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    public void SetCar(GameObject prefab)
    {
        if (visual != null) Destroy(visual.gameObject);
        visual = CarVisual.Spawn(prefab, transform, carLength).transform;

        // Colisor um pouco menor que o carro: perdoa raspões e deixa o jogo mais justo.
        Bounds b = CarVisual.LocalBounds(visual.gameObject);
        box.center = new Vector3(0f, b.center.y, b.center.z);
        box.size = new Vector3(b.size.x * 0.8f, b.size.y * 0.9f, b.size.z * 0.85f);
    }

    public float LaneX(int lane) => (lane - (LaneCount - 1) * 0.5f) * laneWidth;

    public void ChangeLane(int direction)
    {
        if (!InputEnabled) return;
        int newLane = Mathf.Clamp(Lane + direction, 0, LaneCount - 1);
        if (newLane == Lane) return;
        Lane = newLane;
        LastLaneChangeTime = Time.time;
    }

    /// <summary>Hora (Time.time) da última troca de faixa — usada para detectar o "raspão".</summary>
    public float LastLaneChangeTime { get; private set; } = -10f;

    /// <summary>Swipe para cima / W / seta para cima.</summary>
    public event System.Action BoostRequested;

    [Header("Efeitos (opcional)")]
    public GameObject shieldBubble;
    public ParticleSystem nitroFlame;

    public void SetShield(bool on)
    {
        if (shieldBubble != null) shieldBubble.SetActive(on);
    }

    public void SetNitro(bool on)
    {
        if (nitroFlame == null) return;
        if (on) nitroFlame.Play();
        else nitroFlame.Stop();
    }

    public void MoveForward(float distance)
    {
        transform.position += Vector3.forward * distance;
    }

    void Update()
    {
        if (InputEnabled) ReadInput();

        Vector3 pos = transform.position;
        float targetX = LaneX(Lane);
        pos.x = Mathf.MoveTowards(pos.x, targetX, laneChangeSpeed * Time.deltaTime);
        transform.position = pos;

        if (visual != null)
        {
            float tilt = Mathf.Clamp((targetX - pos.x) * 10f, -maxTilt, maxTilt);
            visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.Euler(0f, tilt, -tilt * 0.3f), 15f * Time.deltaTime);
            if (Time.time < blinkUntil) visual.gameObject.SetActive(Mathf.Repeat(Time.time * 10f, 1f) > 0.35f);
            else if (!visual.gameObject.activeSelf) visual.gameObject.SetActive(true);
        }
    }

    void ReadInput()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) ChangeLane(-1);
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) ChangeLane(1);
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) BoostRequested?.Invoke();
        }

        var pointer = Pointer.current;
        if (pointer == null) return;

        if (pointer.press.wasPressedThisFrame)
        {
            pressing = true;
            swipeConsumed = false;
            pressStart = pointer.position.ReadValue();
        }
        else if (pointer.press.wasReleasedThisFrame)
        {
            pressing = false;
        }

        if (pressing && !swipeConsumed)
        {
            Vector2 delta = pointer.position.ReadValue() - pressStart;
            float threshold = swipeThreshold * Screen.width;
            if (Mathf.Abs(delta.x) > threshold && Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            {
                ChangeLane(delta.x > 0 ? 1 : -1);
                swipeConsumed = true;
            }
            else if (delta.y > threshold && delta.y > Mathf.Abs(delta.x))
            {
                BoostRequested?.Invoke();
                swipeConsumed = true;
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (GameManager.Instance == null) return;

        var coin = other.GetComponent<Coin>();
        if (coin != null)
        {
            GameManager.Instance.CollectCoin(coin);
            return;
        }

        var pickup = other.GetComponent<Pickup>();
        if (pickup != null)
        {
            GameManager.Instance.CollectPickup(pickup);
            return;
        }

        var obstacle = other.GetComponentInParent<Obstacle>();
        if (obstacle != null)
            GameManager.Instance.OnPlayerHit(obstacle);
    }

    public void Crash()
    {
        InputEnabled = false;
        pressing = false;
        if (visual != null) StartCoroutine(CrashBounce());
    }

    public void Revive(float invulnerableSeconds)
    {
        StopAllCoroutines();
        if (visual != null)
        {
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
        }
        blinkUntil = Time.time + invulnerableSeconds;
        InputEnabled = true;
    }

    IEnumerator CrashBounce()
    {
        float t = 0f;
        float spin = Random.value > 0.5f ? 1f : -1f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            float k = t / 0.6f;
            visual.localPosition = new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 1.2f, -k * 2f);
            visual.localRotation = Quaternion.Euler(-12f * Mathf.Sin(k * Mathf.PI), spin * 35f * k, spin * 8f * k);
            yield return null;
        }
    }
}
