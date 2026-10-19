using UnityEngine;

/// <summary>
/// Piloto automático usado no teste automatizado (e útil para gravar vídeo da loja):
/// olha a próxima fileira de trânsito e troca para uma faixa livre. Com dodgeLate ele desvia
/// no último instante (gera raspões) e usa o nitro assim que fica pronto.
/// </summary>
public class AutoPilot : MonoBehaviour
{
    public PlayerController player;
    public TrafficSpawner traffic;
    [Tooltip("Probabilidade de 'errar' de propósito por fileira, para testar a batida.")]
    [Range(0f, 1f)] public float mistakeChance = 0f;
    [Tooltip("Desvia em cima da hora para provocar raspões.")]
    public bool dodgeLate = true;
    [Tooltip("Segundos antes do impacto em que começa a desviar quando dodgeLate está ligado.")]
    public float lateDodgeSeconds = 0.32f;

    TrafficSpawner.Row decidedRow;
    bool ignoreRow;

    void Update()
    {
        if (player == null || traffic == null || !player.InputEnabled) return;
        var gm = GameManager.Instance;
        if (gm != null && gm.Nitro01 >= 1f && !gm.NitroActive) gm.TryNitro();

        float pz = player.transform.position.z;
        TrafficSpawner.Row next = null;
        TrafficSpawner.Row after = null;
        foreach (var row in traffic.Rows)
        {
            if (row.Z < pz - player.carLength) continue;
            if (next == null) next = row;
            else { after = row; break; }
        }
        if (next == null) return;

        if (next != decidedRow)
        {
            decidedRow = next;
            ignoreRow = Random.value < mistakeChance;
        }
        if (ignoreRow || !next.blocked[player.Lane]) return;

        int best = -1;
        int bestScore = int.MaxValue;
        for (int l = 0; l < PlayerController.LaneCount; l++)
        {
            if (next.blocked[l]) continue;
            int score = Mathf.Abs(l - player.Lane) * 2 + (after != null && after.blocked[l] ? 1 : 0);
            if (score < bestScore) { bestScore = score; best = l; }
        }
        if (best < 0) return;

        if (dodgeLate && gm != null)
        {
            // Precisa de mais antecedência quando tem que atravessar duas faixas.
            float relSpeed = Mathf.Max(gm.Speed - traffic.trafficSpeed, 1f);
            float gap = next.Z - pz - player.carLength;
            if (gap > relSpeed * lateDodgeSeconds * Mathf.Abs(best - player.Lane)) return;
        }

        player.ChangeLane(best > player.Lane ? 1 : -1);
    }
}
