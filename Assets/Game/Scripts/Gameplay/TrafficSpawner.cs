using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Gera "fileiras" de trânsito à frente do jogador. Cada fileira bloqueia 1 ou 2 faixas (nunca as 3)
/// e pode trazer uma linha de moedas ou um power-up numa faixa livre. Todas as fileiras andam na mesma
/// velocidade, então o espaço entre elas nunca diminui e o caminho continua sempre possível.
/// </summary>
public class TrafficSpawner : MonoBehaviour
{
    public class Row
    {
        public Transform root;
        public bool[] blocked = new bool[PlayerController.LaneCount];
        public bool passed;
        /// <summary>Comprimento ocupado pela fileira (veículo mais comprido + variação de posição).</summary>
        public float length;
        public readonly List<GameObject> cars = new List<GameObject>();
        public readonly List<GameObject> coins = new List<GameObject>();
        public readonly List<Pickup> pickups = new List<Pickup>();
        public float Z => root.position.z;
    }

    [Header("Referências")]
    public PlayerController player;
    public GameObject coinPrefab;
    public Pickup[] pickupPrefabs;

    [Header("Trânsito")]
    public float trafficSpeed = 6f;
    public float carLength = 4.4f;
    public float spawnAhead = 170f;
    public float despawnBehind = 15f;
    public float firstRowDistance = 70f;

    [Header("Dificuldade")]
    public float minGap = 18f;
    [Tooltip("Tempo (s) entre uma fileira e outra no começo e na velocidade máxima.")]
    public float reactionTimeEasy = 1.35f;
    public float reactionTimeHard = 0.75f;
    public float twoLaneChanceEasy = 0.15f;
    public float twoLaneChanceHard = 0.6f;

    [Header("Moedas e power-ups")]
    [Range(0f, 1f)] public float coinLineChance = 0.55f;
    public float coinSpacing = 2.6f;
    [Range(0f, 1f)] public float pickupChance = 0.07f;

    /// <summary>Disparado quando uma fileira passa pelo jogador. O float é a menor distância lateral até um carro dela.</summary>
    public event Action<Row, float> RowPassed;

    readonly List<Row> rows = new List<Row>();
    readonly Stack<Row> rowPool = new Stack<Row>();
    readonly Dictionary<GameObject, Stack<GameObject>> carPool = new Dictionary<GameObject, Stack<GameObject>>();
    readonly Stack<GameObject> coinPool = new Stack<GameObject>();
    readonly Dictionary<PickupType, Stack<Pickup>> pickupPool = new Dictionary<PickupType, Stack<Pickup>>();
    TrafficCatalog traffic;
    int lastFreeLane = 1;

    public IReadOnlyList<Row> Rows => rows;

    void Awake()
    {
        traffic = TrafficCatalog.Load();
    }

    /// <summary>Avança o trânsito e cria/recicla fileiras. difficulty vai de 0 (início) a 1 (velocidade máxima).</summary>
    public void Tick(float dt, float playerSpeed, float difficulty)
    {
        float move = trafficSpeed * dt;
        for (int i = 0; i < rows.Count; i++) rows[i].root.position += Vector3.forward * move;

        float playerZ = player.transform.position.z;
        float playerX = player.transform.position.x;
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.passed || row.Z > playerZ) continue;
            row.passed = true;
            float minDx = float.MaxValue;
            foreach (var car in row.cars) minDx = Mathf.Min(minDx, Mathf.Abs(car.transform.position.x - playerX));
            if (row.cars.Count > 0) RowPassed?.Invoke(row, minDx);
        }

        for (int i = rows.Count - 1; i >= 0; i--)
        {
            if (rows[i].Z < playerZ - despawnBehind) Recycle(i);
        }

        float relSpeed = Mathf.Max(playerSpeed - trafficSpeed, 6f);
        float reaction = Mathf.Lerp(reactionTimeEasy, reactionTimeHard, difficulty);
        while (rows.Count == 0 || rows[rows.Count - 1].Z < playerZ + spawnAhead)
            SpawnRow(playerZ, relSpeed, reaction, difficulty);
    }

    void SpawnRow(float playerZ, float relSpeed, float reaction, float difficulty)
    {
        Row row = rowPool.Count > 0 ? rowPool.Pop() : new Row { root = new GameObject("Row").transform };
        row.root.SetParent(transform, false);
        row.root.gameObject.SetActive(true);
        row.passed = false;

        int lanes = PlayerController.LaneCount;
        float twoChance = Mathf.Lerp(twoLaneChanceEasy, twoLaneChanceHard, difficulty);
        int blockedCount = Random.value < twoChance ? 2 : 1;

        for (int l = 0; l < lanes; l++) row.blocked[l] = false;
        // Escolhe uma faixa livre garantida; o resto pode virar bloqueio.
        int free = Random.Range(0, lanes);
        var candidates = new List<int>();
        for (int l = 0; l < lanes; l++) if (l != free) candidates.Add(l);
        for (int n = 0; n < blockedCount && candidates.Count > 0; n++)
        {
            int pick = Random.Range(0, candidates.Count);
            row.blocked[candidates[pick]] = true;
            candidates.RemoveAt(pick);
        }

        // Escolhe os veículos antes de posicionar: caminhões compridos pedem mais espaço até a fileira anterior.
        row.length = carLength;
        for (int l = 0; l < lanes; l++)
        {
            if (!row.blocked[l]) continue;
            var car = TakeCar();
            car.transform.SetParent(row.root, false);
            // Pequena variação de posição deixa o trânsito menos "em grade".
            car.transform.localPosition = new Vector3(player.LaneX(l), 0f, Random.Range(-1.5f, 1.5f));
            car.transform.localRotation = Quaternion.identity;
            car.SetActive(true);
            row.cars.Add(car);
            row.length = Mathf.Max(row.length, car.GetComponent<Obstacle>().length + 3f);
        }

        // Distância centro a centro = tempo de reação + metade de cada fileira.
        float gap;
        if (rows.Count == 0)
        {
            gap = firstRowDistance;
            row.root.position = new Vector3(0f, 0f, playerZ + firstRowDistance);
        }
        else
        {
            var prev = rows[rows.Count - 1];
            gap = Mathf.Max(minGap, relSpeed * reaction + (prev.length + row.length) * 0.5f);
            row.root.position = new Vector3(0f, 0f, prev.Z + gap);
        }
        float prevHalf = rows.Count == 0 ? 0f : rows[rows.Count - 1].length * 0.5f;

        // Faixa livre que dá para alcançar sem trocar de faixa, se possível.
        int rewardLane = row.blocked[lastFreeLane] ? free : lastFreeLane;
        if (pickupPrefabs != null && pickupPrefabs.Length > 0 && Random.value < pickupChance)
        {
            var pickup = TakePickup(pickupPrefabs[Random.Range(0, pickupPrefabs.Length)]);
            pickup.transform.SetParent(row.root, false);
            pickup.transform.localPosition = new Vector3(player.LaneX(rewardLane), 1f, -gap * 0.5f);
            pickup.gameObject.SetActive(true);
            row.pickups.Add(pickup);
        }
        else if (coinPrefab != null && Random.value < coinLineChance)
        {
            // Moedas na faixa livre, no espaço livre entre a fileira anterior e esta.
            float start = -gap + prevHalf + 1.5f;
            float end = -row.length * 0.5f - 1f;
            start = Mathf.Max(start, end - 11 * coinSpacing); // no máximo 12 moedas por linha
            for (float cz = start; cz <= end; cz += coinSpacing)
            {
                var coin = coinPool.Count > 0 ? coinPool.Pop() : Instantiate(coinPrefab);
                coin.transform.SetParent(row.root, false);
                coin.transform.localPosition = new Vector3(player.LaneX(rewardLane), 1f, cz);
                coin.SetActive(true);
                row.coins.Add(coin);
            }
        }

        lastFreeLane = free;
        rows.Add(row);
    }

    GameObject TakeCar()
    {
        var prefab = traffic.PickRandom();
        if (!carPool.TryGetValue(prefab, out var stack))
        {
            stack = new Stack<GameObject>();
            carPool[prefab] = stack;
        }
        if (stack.Count > 0)
        {
            var pooled = stack.Pop();
            pooled.GetComponent<BoxCollider>().enabled = true;
            return pooled;
        }

        var car = CarVisual.SpawnScaled(prefab, transform, traffic.scale, traffic.maxWidth);
        var obstacle = car.AddComponent<Obstacle>();
        obstacle.sourcePrefab = prefab;
        Bounds b = CarVisual.LocalBounds(car);
        obstacle.length = b.size.z;
        var box = car.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = b.center;
        box.size = new Vector3(b.size.x * 0.85f, b.size.y, b.size.z * 0.92f);
        return car;
    }

    Pickup TakePickup(Pickup prefab)
    {
        if (!pickupPool.TryGetValue(prefab.type, out var stack))
        {
            stack = new Stack<Pickup>();
            pickupPool[prefab.type] = stack;
        }
        return stack.Count > 0 ? stack.Pop() : Instantiate(prefab);
    }

    void ReturnCar(GameObject car)
    {
        car.SetActive(false);
        car.transform.SetParent(transform, false);
        carPool[car.GetComponent<Obstacle>().sourcePrefab].Push(car);
    }

    void Recycle(int index)
    {
        Row row = rows[index];
        rows.RemoveAt(index);
        foreach (var car in row.cars) ReturnCar(car);
        foreach (var coin in row.coins)
        {
            coin.SetActive(false);
            coin.transform.SetParent(transform, false);
            coinPool.Push(coin);
        }
        foreach (var p in row.pickups)
        {
            p.gameObject.SetActive(false);
            p.transform.SetParent(transform, false);
            pickupPool[p.type].Push(p);
        }
        row.cars.Clear();
        row.coins.Clear();
        row.pickups.Clear();
        row.root.gameObject.SetActive(false);
        rowPool.Push(row);
    }

    /// <summary>Remove as fileiras atrás e até `ahead` metros à frente (usado ao continuar depois de bater).</summary>
    public void ClearUntil(float playerZ, float ahead)
    {
        for (int i = rows.Count - 1; i >= 0; i--)
        {
            if (rows[i].Z < playerZ + ahead) Recycle(i);
        }
    }

    public void ReturnCoin(GameObject coin)
    {
        coin.SetActive(false);
        foreach (var row in rows)
        {
            if (row.coins.Remove(coin)) break;
        }
        coin.transform.SetParent(transform, false);
        coinPool.Push(coin);
    }

    public void ReturnPickup(Pickup pickup)
    {
        pickup.gameObject.SetActive(false);
        foreach (var row in rows)
        {
            if (row.pickups.Remove(pickup)) break;
        }
        pickup.transform.SetParent(transform, false);
        pickupPool[pickup.type].Push(pickup);
    }

    /// <summary>Moedas ativas na pista (usado pelo ímã).</summary>
    public IEnumerable<GameObject> ActiveCoins()
    {
        foreach (var row in rows)
            foreach (var coin in row.coins)
                yield return coin;
    }

    /// <summary>Arremessa o carro para longe (nitro/escudo) e devolve ao pool depois da animação.</summary>
    public void Knock(GameObject car, float playerSpeed)
    {
        foreach (var row in rows)
        {
            if (row.cars.Remove(car)) break;
        }
        car.GetComponent<BoxCollider>().enabled = false;
        car.transform.SetParent(transform, true);
        StartCoroutine(FlyAway(car, playerSpeed));
    }

    IEnumerator FlyAway(GameObject car, float playerSpeed)
    {
        float side = car.transform.position.x >= player.transform.position.x ? 1f : -1f;
        Vector3 velocity = new Vector3(side * Random.Range(10f, 16f), Random.Range(9f, 13f), playerSpeed * 0.9f);
        Vector3 spin = new Vector3(Random.Range(-400f, 400f), Random.Range(-300f, 300f), Random.Range(-500f, 500f));
        float t = 0f;
        while (t < 1.4f)
        {
            float dt = Time.deltaTime;
            t += dt;
            velocity += Vector3.down * 30f * dt;
            car.transform.position += velocity * dt;
            car.transform.Rotate(spin * dt, Space.World);
            yield return null;
        }
        car.transform.rotation = Quaternion.identity;
        ReturnCar(car);
    }
}
