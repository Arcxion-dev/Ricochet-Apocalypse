using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using NavMeshPlus.Components;

/// <summary>
/// 통합 테스트 씬("총합 테스트") 전용 컨트롤러.
/// 좌측 하단 리셋 버튼에서 호출되어:
/// 1) 기존 장애물을 모두 제거하고 무작위로 새 배치를 생성하고,
/// 2) NavMesh를 다시 굽고,
/// 3) 적들을 원래 스폰 위치로 되돌려 이동 AI를 새 배치에서 다시 테스트할 수 있게 합니다.
/// </summary>
public class MapResetController : MonoBehaviour
{
    [Header("장애물 무작위 배치")]
    [SerializeField] private List<GameObject> obstaclePrefabs = new List<GameObject>();
    [SerializeField] private int obstacleCount = 10;
    [SerializeField] private Vector2 areaMin = new Vector2(-4f, -4.5f);
    [SerializeField] private Vector2 areaMax = new Vector2(4f, 4.5f);
    [Tooltip("장애물끼리 너무 겹치지 않도록 최소 간격.")]
    [SerializeField] private float minDistanceBetweenObstacles = 0.9f;

    [Header("참조")]
    [SerializeField] private NavMeshSurface navMeshSurface;
    [SerializeField] private List<Transform> enemies = new List<Transform>();

    private readonly List<Vector3> enemySpawnPositions = new List<Vector3>();

    private void Awake()
    {
        // 씬 시작 시점의 적 위치를 기억해뒀다가, 리셋할 때마다 그 자리로 되돌린다.
        foreach (var e in enemies)
        {
            enemySpawnPositions.Add(e != null ? e.position : Vector3.zero);
        }
    }

    /// <summary>리셋 버튼(UI Button.onClick)에서 호출합니다.</summary>
public void ResetMap()
    {
        StartCoroutine(ResetMapRoutine());
    }

    /// <summary>
    /// NavMeshPlus는 2D 물리 콜라이더를 FixedUpdate 시점에만 갱신하기 때문에,
    /// Instantiate 직후 같은 프레임에 BuildNavMesh()를 호출하면 새 장애물을 못 찾는다.
    /// 그래서 장애물 생성 후 한 번의 FixedUpdate를 기다렸다가 NavMesh를 다시 굽는다.
    /// </summary>
    private System.Collections.IEnumerator ResetMapRoutine()
    {
        ClearObstacles();
        SpawnRandomObstacles();

        yield return new WaitForFixedUpdate();
        yield return null;

        RebuildNavMesh();
        ResetEnemies();

        Debug.Log("[MapResetController] 맵을 새로 굴렸습니다.");
    }

private void ClearObstacles()
    {
        var markers = FindObjectsOfType<ObstacleTypeMarker>(true);
        foreach (var marker in markers)
        {
            if (marker != null) DestroyImmediate(marker.gameObject);
        }
    }

    private void SpawnRandomObstacles()
    {
        if (obstaclePrefabs.Count == 0)
        {
            Debug.LogWarning("[MapResetController] obstaclePrefabs가 비어 있어 장애물을 생성할 수 없습니다.");
            return;
        }

        var placedPositions = new List<Vector2>();
        int attempts = 0;
        int maxAttempts = obstacleCount * 30;

        while (placedPositions.Count < obstacleCount && attempts < maxAttempts)
        {
            attempts++;
            Vector2 candidate = new Vector2(
                Random.Range(areaMin.x, areaMax.x),
                Random.Range(areaMin.y, areaMax.y));

            if (IsTooClose(candidate, placedPositions)) continue;

            var prefab = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Count)];
            Instantiate(prefab, candidate, Quaternion.identity);
            placedPositions.Add(candidate);
        }
    }

    private bool IsTooClose(Vector2 candidate, List<Vector2> placed)
    {
        for (int i = 0; i < placed.Count; i++)
        {
            if (Vector2.Distance(placed[i], candidate) < minDistanceBetweenObstacles)
                return true;
        }
        return false;
    }

private void RebuildNavMesh()
    {
        if (navMeshSurface == null)
        {
            Debug.LogWarning("[MapResetController] navMeshSurface가 연결되지 않아 NavMesh를 다시 구울 수 없습니다.");
            return;
        }

        // NavMeshPlus 공식 가이드: 런타임에 새로 생성/이동된 2D 콜라이더의 bounds는
        // LateUpdate 전까지 갱신되지 않으므로, Bake 직전에 강제로 동기화해야 한다.
        Physics2D.SyncTransforms();
        navMeshSurface.BuildNavMesh();
    }

private void ResetEnemies()
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            var enemy = enemies[i];
            if (enemy == null) continue;

            Vector3 spawnPos = i < enemySpawnPositions.Count ? enemySpawnPositions[i] : enemy.position;
            var agent = enemy.GetComponent<NavMeshAgent>();

            if (agent != null)
            {
                agent.ResetPath();
                bool onMesh = agent.Warp(spawnPos);
                if (!onMesh)
                {
                    // 스폰 지점이 경계(erosion)에 걸려 워프에 실패하면, 근처의 유효한 지점을 찾아 재시도한다.
                    if (NavMesh.SamplePosition(spawnPos, out var hit, 1.5f, NavMesh.AllAreas))
                    {
                        agent.Warp(hit.position);
                    }
                }
            }
            else
            {
                enemy.position = spawnPos;
            }
        }
    }
}
