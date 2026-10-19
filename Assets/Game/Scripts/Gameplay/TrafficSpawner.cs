using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gera "fileiras" de trânsito à frente do jogador. Cada fileira bloqueia 1 ou 2 faixas (nunca as 3)
/// e pode trazer uma linha de moedas numa faixa livre. Todas as fileiras andam na mesma velocidade,
/// então o espaço entre elas nunca diminui e o caminho continua sempre possível.
/// </summary>
public class TrafficSpawner : MonoBehaviour
{
    public class Row
    {
        public Transform root;
        public bool[] blocked = new bool[PlayerController.LaneCount];
        public readonly List<GameObject> cars = new List<GameObject>();
        public readonly List<GameObject> coins = new List<GameObject>();
        public float Z => root.position.z;
    }

    [Header("Referências")]
    public PlayerController player;
    public GameObject coinPrefab;

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

    [Header("Moedas")]
    [Range(0f, 1f)] public float coinLineChance = 0.55f;
    public float coinSpacing = 2.6f;

    readonly List<Row> rows = new List<Row>();
    readonly Stack<Row> rowPool = new Stack<Row>();
    readonly Dictionary<GameObject, Stack<GameObject>> carPool = new Dictionary<GameObject, Stack<GameObject>>();
    readonly Stack<GameObject> coinPool = new Stack<GameObject>();
    CarCatalog catalog;
    int lastFreeLane = 1;

    public IReadOnlyList<Row> Rows => rows;

    void Awake()
    {
        catalog = CarCatalog.Load();
    }

    /// <summary>Avança o trânsito e cria/recicla fileiras. difficulty vai de 0 (início) a 1 (velocidade máxima).</summary>
    public void Tick(float dt, float playerSpeed, float difficulty)
    {
        float move = trafficSpeed * dt;
        for (int i = 0; i < rows.Count; i++) rows[i].root.position += Vector3.forward * move;

        float playerZ = player.transform.position.z;
        for (int i = rows.Count - 1; i >= 0; i--)
        {
            if (rows[i].Z < playerZ - despawnBehind) Recycle(i);
        }

        float relSpeed = Mathf.Max(playerSpeed - trafficSpeed, 6f);
        float nextZ = rows.Count > 0 ? rows[rows.Count - 1].Z : playerZ + firstRowDistance;
        while (nextZ < playerZ + spawnAhead)
        {
            float reaction = Mathf.Lerp(reactionTimeEasy, reactionTimeHard, difficulty);
            float gap = Mathf.Max(minGap, relSpeed * reaction + carLength);
            float z = rows.Count > 0 ? nextZ + gap : nextZ;
            SpawnRow(z, gap, difficulty);
            nextZ = z;
        }
    }

    void SpawnRow(float z, float gap, float difficulty)
    {
        Row row = rowPool.Count > 0 ? rowPool.Pop() : new Row { root = new GameObject("Row").transform };
        row.root.SetParent(transform, false);
        row.root.position = new Vector3(0f, 0f, z);
        row.root.gameObject.SetActive(true);

        int lanes = PlayerController.LaneCount;
        float twoChance = Mathf.Lerp(twoLaneChanceEasy, twoLaneChanceHard, difficulty);
        int blockedCount = Random.value < twoChance ? 2 : 1;

        for (int l = 0; l < lanes; l++) row.blocked[l] = false;
        // Escolhe faixas livres aleatórias; o resto vira bloqueio.
        int free = Random.Range(0, lanes);
        var candidates = new List<int>();
        for (int l = 0; l < lanes; l++) if (l != free) candidates.Add(l);
        for (int n = 0; n < blockedCount && candidates.Count > 0; n++)
        {
            int pick = Random.Range(0, candidates.Count);
            row.blocked[candidates[pick]] = true;
            candidates.RemoveAt(pick);
        }

        for (int l = 0; l < lanes; l++)
        {
            if (!row.blocked[l]) continue;
            var car = TakeCar();
            car.transform.SetParent(row.root, false);
            // Pequena variação de posição deixa o trânsito menos "em grade".
            car.transform.localPosition = new Vector3(player.LaneX(l), 0f, Random.Range(-1.5f, 1.5f));
            car.SetActive(true);
            row.cars.Add(car);
        }

        if (coinPrefab != null && Random.value < coinLineChance)
        {
            // Moedas na faixa livre, no espaço entre a fileira anterior e esta.
            int coinLane = row.blocked[lastFreeLane] ? free : lastFreeLane;
            float start = -gap + carLength * 1.2f;
            float end = -carLength * 0.9f;
            for (float cz = start; cz <= end; cz += coinSpacing)
            {
                var coin = coinPool.Count > 0 ? coinPool.Pop() : Instantiate(coinPrefab);
                coin.transform.SetParent(row.root, false);
                coin.transform.localPosition = new Vector3(player.LaneX(coinLane), 1f, cz);
                coin.SetActive(true);
                row.coins.Add(coin);
            }
        }

        lastFreeLane = free;
        rows.Add(row);
    }

    GameObject TakeCar()
    {
        var entry = catalog.Get(Random.Range(0, catalog.Count));
        if (!carPool.TryGetValue(entry.prefab, out var stack))
        {
            stack = new Stack<GameObject>();
            carPool[entry.prefab] = stack;
        }
        if (stack.Count > 0) return stack.Pop();

        var car = CarVisual.Spawn(entry.prefab, transform, carLength);
        var obstacle = car.AddComponent<Obstacle>();
        obstacle.sourcePrefab = entry.prefab;
        Bounds b = CarVisual.LocalBounds(car);
        var box = car.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = b.center;
        box.size = new Vector3(b.size.x * 0.85f, b.size.y, b.size.z * 0.9f);
        return car;
    }

    void Recycle(int index)
    {
        Row row = rows[index];
        rows.RemoveAt(index);
        foreach (var car in row.cars)
        {
            car.SetActive(false);
            car.transform.SetParent(transform, false);
            carPool[car.GetComponent<Obstacle>().sourcePrefab].Push(car);
        }
        foreach (var coin in row.coins)
        {
            coin.SetActive(false);
            coin.transform.SetParent(transform, false);
            coinPool.Push(coin);
        }
        row.cars.Clear();
        row.coins.Clear();
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
}
