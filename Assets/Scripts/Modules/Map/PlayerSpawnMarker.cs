using UnityEngine;

/// <summary>
/// 플레이어 스폰 지점 마커. 맵 에디터가 배치하며, 씬에는 하나만 존재한다.
/// <see cref="PlayerPositionModule"/>의 spawnPoint로 연결되어 스테이지 시작 시 플레이어 위치가 된다.
/// 순수 마커(런타임 로직 없음) — Scene 뷰에서 Gizmo로 위치를 표시한다.
/// </summary>
public class PlayerSpawnMarker : MonoBehaviour
{
    [SerializeField] private Color gizmoColor = new Color(0.25f, 0.8f, 1f, 0.9f);
    [SerializeField] private float radius = 0.45f;

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, radius);
        Gizmos.DrawLine(transform.position + Vector3.left * radius, transform.position + Vector3.right * radius);
        Gizmos.DrawLine(transform.position + Vector3.down * radius, transform.position + Vector3.up * radius);
    }
}
