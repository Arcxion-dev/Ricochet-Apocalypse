using UnityEngine;

/// <summary>
/// 도망 모듈: 피격 시 플레이어 반대 방향으로 일정 거리(기본 3유닛 = "세 칸") 후퇴한다.
/// 후퇴 경로에 장애물(Wall 레이어)이 있으면 그 지점에서 멈춘다.
/// 실제 이동은 EnemyAIModule.ForceDestinationFor로 일시적으로 추격을 덮어써서 처리한다.
/// </summary>
[RequireComponent(typeof(Entity))]
public class FleeOnHitModule : MonoBehaviour
{
    [Header("도망 설정")]
    [Tooltip("한 번 피격당 후퇴하는 거리(유닛). 그리드 한 칸 = 1유닛 기준 \"세 칸\".")]
    [SerializeField] private float retreatDistance = 3f;

    [Tooltip("후퇴 경로를 막는 장애물 레이어.")]
    [SerializeField] private LayerMask wallLayerMask;

    [Tooltip("후퇴 경로 장애물 감지에 쓰는 원 반지름.")]
    [SerializeField] private float obstacleCheckRadius = 0.4f;

    [Tooltip("강제 후퇴 목적지를 유지하는 시간(초). 이 시간이 지나면 다시 정상 추격으로 복귀.")]
    [SerializeField] private float retreatHoldDuration = 1f;

    private Entity entity;
    private EnemyAIModule aiModule;
    private Transform player;

    private void Awake()
    {
        entity = GetComponent<Entity>();
        aiModule = GetComponent<EnemyAIModule>();
        entity.OnDamaged += HandleDamaged;

        var playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo != null) player = playerGo.transform;
    }

    private void OnDestroy()
    {
        if (entity != null) entity.OnDamaged -= HandleDamaged;
    }

    private void HandleDamaged(int amount)
    {
        if (entity.health <= 0) return; // 죽는 타격이면 도망 없음
        if (aiModule == null) return;

        Vector2 origin = transform.position;
        Vector2 awayDir = player != null
            ? ((Vector2)transform.position - (Vector2)player.position).normalized
            : Random.insideUnitCircle.normalized;
        if (awayDir == Vector2.zero) awayDir = Vector2.up;

        Vector2 targetPoint = origin + awayDir * retreatDistance;

        RaycastHit2D hit = Physics2D.CircleCast(origin, obstacleCheckRadius, awayDir, retreatDistance, wallLayerMask);
        if (hit.collider != null)
        {
            targetPoint = hit.centroid; // 장애물에 막히면 그 지점까지만 후퇴
        }

        aiModule.ForceDestinationFor(targetPoint, retreatHoldDuration);
        Debug.Log($"[FleeOnHitModule] {name} 피격 - {targetPoint}로 후퇴");
    }
}
