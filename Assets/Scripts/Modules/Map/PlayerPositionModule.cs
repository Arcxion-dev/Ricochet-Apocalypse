using UnityEngine;

/// <summary>
/// 플레이어 위치 모듈: 맵마다 달라지는 플레이어 스폰 위치를 설정합니다.
/// spawnPoint를 맵 프리팹 내부의 자식 오브젝트로 두면, 맵마다 스폰 위치를 다르게 배치할 수 있습니다.
/// </summary>
public class PlayerPositionModule : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;

    public Vector3 GetSpawnPosition() => spawnPoint != null ? spawnPoint.position : transform.position;

    public void ApplyToPlayer(Transform player)
    {
        if (player == null) return;
        player.position = GetSpawnPosition();
    }

    public void SetSpawnPoint(Transform point) => spawnPoint = point;
}
