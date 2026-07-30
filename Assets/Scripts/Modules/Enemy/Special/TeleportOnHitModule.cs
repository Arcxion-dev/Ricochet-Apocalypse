using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 텔레포트 모듈: 피격 시 일정 확률로, 플레이어에게서 일정 거리 떨어진 안전한(NavMesh 유효) 위치로 순간이동한다.
/// </summary>
[RequireComponent(typeof(Entity))]
public class TeleportOnHitModule : MonoBehaviour
{
    [Header("텔레포트 설정")]
    [Range(0f, 1f)]
    [SerializeField] private float teleportChance = 0.4f;

    [SerializeField] private float cooldown = 2f;
    [SerializeField] private float minDistanceFromPlayer = 3f;
    [SerializeField] private float maxDistanceFromPlayer = 8f;
    [SerializeField] private int maxAttempts = 8;
    [SerializeField] private float navMeshSampleRadius = 1.5f;

    private Entity entity;
    private NavMeshAgent agent;
    private Transform player;
    private float cooldownTimer;

    private void Awake()
    {
        entity = GetComponent<Entity>();
        agent = GetComponent<NavMeshAgent>();
        entity.OnDamaged += HandleDamaged;

        var playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo != null) player = playerGo.transform;
    }

    private void OnDestroy()
    {
        if (entity != null) entity.OnDamaged -= HandleDamaged;
    }

    private void Update()
    {
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
    }

    private void HandleDamaged(int amount)
    {
        if (entity.health <= 0) return;
        if (cooldownTimer > 0f) return;
        if (Random.value > teleportChance) return;
        if (agent == null) return;

        Vector2 origin = player != null ? (Vector2)player.position : (Vector2)transform.position;

        for (int i = 0; i < maxAttempts; i++)
        {
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float dist = Random.Range(minDistanceFromPlayer, maxDistanceFromPlayer);
            Vector2 candidate = origin + randomDir * dist;

            if (NavMesh.SamplePosition(candidate, out var hit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                cooldownTimer = cooldown;
                Debug.Log($"[TeleportOnHitModule] {name} 피격 텔레포트 -> {hit.position}");
                return;
            }
        }

        Debug.Log($"[TeleportOnHitModule] {name}: 유효한 텔레포트 위치를 찾지 못함");
    }
}
