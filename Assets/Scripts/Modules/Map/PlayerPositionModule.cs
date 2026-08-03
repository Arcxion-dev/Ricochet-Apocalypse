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

        Vector3 spawnPosition = GetSpawnPosition();

        // 플레이어는 Kinematic Rigidbody2D로 움직이므로 Transform만 옮기면 물리 포즈와 어긋난다.
        // PlayerMovement가 있으면 둘을 함께 맞춰주는 Teleport를 쓴다.
        var movement = player.GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.Teleport(spawnPosition);
            return;
        }

        player.position = spawnPosition;
    }

    public void SetSpawnPoint(Transform point) => spawnPoint = point;
}
