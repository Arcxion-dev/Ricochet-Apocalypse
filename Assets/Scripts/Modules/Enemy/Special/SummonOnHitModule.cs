using UnityEngine;

/// <summary>
/// 소환 모듈: 피격(비 치명타)당 자신과 플레이어 사이("바로 앞")에 일반 몹을 소환한다.
/// 별도 쿨다운/횟수 제한이 없다 — 이 몹 자체의 낮은 체력이 발동 횟수를 자연히 제한한다.
/// </summary>
[RequireComponent(typeof(Entity))]
public class SummonOnHitModule : MonoBehaviour
{
    [Header("소환 설정")]
    [Tooltip("소환할 프리팹 (기본: 일반 몹).")]
    [SerializeField] private GameObject summonPrefab;

    [Tooltip("자신 기준 플레이어 방향으로 얼마나 떨어진 위치에 소환할지.")]
    [SerializeField] private float summonOffsetDistance = 1f;

    private Entity entity;
    private Transform player;

    private void Awake()
    {
        entity = GetComponent<Entity>();
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
        if (entity.health <= 0) return; // 죽는 타격이면 소환하지 않음
        if (summonPrefab == null)
        {
            Debug.LogWarning($"[SummonOnHitModule] {name}: summonPrefab이 지정되지 않았습니다.");
            return;
        }

        Vector2 towardPlayer = player != null
            ? ((Vector2)player.position - (Vector2)transform.position).normalized
            : Vector2.up;
        Vector2 spawnPos = (Vector2)transform.position + towardPlayer * summonOffsetDistance;

        Instantiate(summonPrefab, spawnPos, Quaternion.identity);
        Debug.Log($"[SummonOnHitModule] {name} 피격 - {spawnPos}에 {summonPrefab.name} 소환");
    }
}
