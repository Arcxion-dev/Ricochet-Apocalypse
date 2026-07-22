using UnityEngine;

/// <summary>
/// 파괴 가능한 장애물(나무, 바위 등)에 부착하는 간단한 체력/파괴 컴포넌트.
/// 원래는 장애물 담당자 영역이지만, 총알 테스트 완결성을 위해 임시로 구현합니다.
/// 담당자가 자체 시스템을 만들면 이 컴포넌트를 교체하면 됩니다.
///
/// 규칙:
/// - 나무: 총알 맞을 때마다 체력 감소, 0 이하가 되면 파괴
/// - 바위: 일반 총알에는 파괴되지 않고, 폭발탄에 의해서만 파괴됨 (ApplyExplosionDamage로 별도 처리)
/// </summary>
[RequireComponent(typeof(ObstacleTypeMarker))]
public class DestructibleObstacle : MonoBehaviour
{
    [Tooltip("체력")]
    public int health = 30;

    [Tooltip("일반 총알 피격 시 감소하는 체력량 (나무 전용, 바위는 무시)")]
    public int damagePerHit = 10;

    [Tooltip("폭발탄 등 폭발 데미지에 의해서만 파괴되는지 여부 (바위는 true)")]
    public bool onlyDestructibleByExplosion = false;

    private bool _isDestroyed;

    /// <summary>
    /// 일반 총알 피격 시 호출. 바위처럼 폭발로만 파괴되는 장애물은 이 호출을 무시합니다.
    /// </summary>
    public void ApplyBulletHit()
    {
        if (_isDestroyed) return;
        if (onlyDestructibleByExplosion)
        {
            Debug.Log($"[DestructibleObstacle] {name}은(는) 일반 총알로 파괴되지 않습니다.");
            return;
        }

        health -= damagePerHit;
        Debug.Log($"[DestructibleObstacle] {name} 피격, 남은 체력: {health}");

        if (health <= 0) DestroyObstacle();
    }

    /// <summary>
    /// 폭발탄 등 폭발 데미지 적용. 바위는 이 경로로만 파괴됩니다.
    /// </summary>
    public void ApplyExplosionDamage(float damage)
    {
        if (_isDestroyed) return;

        health -= Mathf.RoundToInt(damage);
        Debug.Log($"[DestructibleObstacle] {name} 폭발 피해 {damage}, 남은 체력: {health}");

        if (health <= 0) DestroyObstacle();
    }

    private void DestroyObstacle()
    {
        _isDestroyed = true;
        Debug.Log($"[DestructibleObstacle] {name} 파괴됨");
        Destroy(gameObject);
    }
}
