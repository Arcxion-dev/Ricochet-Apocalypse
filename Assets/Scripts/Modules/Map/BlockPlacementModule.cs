using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 블록 배치 모듈: 장애물의 위치를 랜덤(또는 규칙 기반)으로 배치하는 알고리즘을 담당합니다.
/// 맵마다 이 모듈의 프리팹 목록/배치 범위/개수만 다르게 설정하면 서로 다른 장애물 배치를 만들 수 있습니다.
/// </summary>
public class BlockPlacementModule : MonoBehaviour
{
    [Header("배치할 장애물 프리팹 목록")]
    [SerializeField] private List<GameObject> obstaclePrefabs = new List<GameObject>();

    [Header("배치 영역 (월드 좌표)")]
    [SerializeField] private Vector2 areaMin = new Vector2(-7f, -4f);
    [SerializeField] private Vector2 areaMax = new Vector2(7f, 4f);

    [Header("배치 개수 / 시드")]
    [SerializeField] private int obstacleCount = 10;
    [SerializeField] private int randomSeed = 0;

    private readonly List<GameObject> spawnedObstacles = new List<GameObject>();

    /// <summary>
    /// 현재 설정값을 기반으로 장애물을 새로 배치합니다.
    /// </summary>
    public void GenerateLayout()
    {
        ClearLayout();

        // TODO: 겹침 방지, 최소 이동통로 확보, 그리드 스냅 등 실제 배치 규칙 구현
        Random.InitState(randomSeed);
        for (int i = 0; i < obstacleCount; i++)
        {
            if (obstaclePrefabs.Count == 0) break;

            var prefab = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Count)];
            Vector2 pos = new Vector2(
                Random.Range(areaMin.x, areaMax.x),
                Random.Range(areaMin.y, areaMax.y));

            var instance = Instantiate(prefab, pos, Quaternion.identity, transform);
            spawnedObstacles.Add(instance);
        }
    }

    public void ClearLayout()
    {
        for (int i = spawnedObstacles.Count - 1; i >= 0; i--)
        {
            if (spawnedObstacles[i] != null)
                Destroy(spawnedObstacles[i]);
        }
        spawnedObstacles.Clear();
    }
}
