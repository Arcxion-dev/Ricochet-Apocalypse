using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 블록 배치 모듈: 장애물의 위치를 랜덤(또는 규칙 기반)으로 배치하는 알고리즘을 담당합니다.
/// </summary>
public class BlockPlacementModule : MonoBehaviour
{
    [Header("배치할 장애물 프리팹 목록")]
    [SerializeField] private List<GameObject> obstaclePrefabs = new List<GameObject>();

    [Header("배치 영역 (월드 좌표)")]
    [SerializeField] private Vector2 areaMin = new Vector2(-4.5f, -6f);
    [SerializeField] private Vector2 areaMax = new Vector2(4.5f, 6f);

    [Header("배치 개수 / 시드")]
    [SerializeField] private int obstacleCount = 10;
    [SerializeField] private int randomSeed = 0;

    private readonly List<GameObject> spawnedObstacles = new List<GameObject>();

    public void GenerateLayout()
    {
        ClearLayout();

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
