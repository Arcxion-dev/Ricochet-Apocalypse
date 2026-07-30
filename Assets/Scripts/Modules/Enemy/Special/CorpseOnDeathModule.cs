using UnityEngine;

/// <summary>
/// 시체산 모듈: 사망 시 자리에 파괴 가능한 시체 장애물을 남긴다.
/// NavMesh 경로에 반영되도록 장애물 스폰 직후 NavMesh를 재베이크한다.
/// </summary>
[RequireComponent(typeof(Entity))]
public class CorpseOnDeathModule : MonoBehaviour
{
    [Header("시체 장애물 설정")]
    [Tooltip("사망 위치에 스폰할 시체 장애물 프리팹 (DestructibleObstacle + ObstacleTypeMarker 포함).")]
    [SerializeField] private GameObject corpseObstaclePrefab;

    private Entity entity;

    private void Awake()
    {
        entity = GetComponent<Entity>();
        entity.OnDeath += HandleDeath;
    }

    private void OnDestroy()
    {
        if (entity != null) entity.OnDeath -= HandleDeath;
    }

    private void HandleDeath()
    {
        if (corpseObstaclePrefab == null)
        {
            Debug.LogWarning($"[CorpseOnDeathModule] {name}: corpseObstaclePrefab이 지정되지 않아 시체 장애물을 생성하지 못했습니다.");
            return;
        }

        Instantiate(corpseObstaclePrefab, transform.position, Quaternion.identity);

        Physics2D.SyncTransforms();
        var baker = FindFirstObjectByType<StageNavMeshBaker>();
        if (baker != null) baker.Rebake();

        Debug.Log($"[CorpseOnDeathModule] {name} 사망 - {transform.position}에 시체 장애물 생성");
    }
}
