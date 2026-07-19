using UnityEngine;

/// <summary>
/// 5. 유도탄 - 위치와 상관없이 지정된 목표를 향해 적중하며, 벽에는 튕깁니다.
/// 실제 타겟 탐색(적 레이어 스캔 등)은 적 시스템이 없으므로
/// BulletController.Target을 외부(발사자)가 직접 지정해주는 형태로 동작합니다.
/// 타겟이 없을 경우를 대비한 탐색 스텁만 마련해둡니다.
/// </summary>
[CreateAssetMenu(fileName = "HomingEffect", menuName = "Bullet/Effects/유도탄 (Homing)")]
public class HomingEffectSO : BulletEffectSO
{
    [Tooltip("초당 회전 가능한 최대 각도 (조준 보정 강도)")]
    public float turnSpeedDegPerSec = 180f;

    [Tooltip("타겟이 없을 때 자동 탐색을 시도할 반경")]
    public float autoAcquireRadius = 10f;

    public override void OnInit(BulletController bullet)
    {
        if (bullet.Target == null)
        {
            Debug.Log($"[유도탄] 타겟 미지정 - 반경 {autoAcquireRadius} 내 자동 탐색 필요 (적 시스템 미구현)");
        }
        else
        {
            Debug.Log($"[유도탄] 타겟 {bullet.Target.name} 유도 시작, 선회속도 {turnSpeedDegPerSec}deg/s");
        }
    }

    public override void OnTick(BulletController bullet, float deltaTime)
    {
        if (bullet.Target == null) return;

        // 목표를 향해 방향을 서서히 회전시키는 뼈대 로직.
        Vector2 toTarget = (Vector2)bullet.Target.position - (Vector2)bullet.transform.position;
        if (toTarget.sqrMagnitude < 0.0001f) return;

        float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
        float currentAngle = Mathf.Atan2(bullet.Direction.y, bullet.Direction.x) * Mathf.Rad2Deg;
        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, turnSpeedDegPerSec * deltaTime);

        Vector2 newDir = new Vector2(Mathf.Cos(newAngle * Mathf.Deg2Rad), Mathf.Sin(newAngle * Mathf.Deg2Rad));
        bullet.SetDirection(newDir);
    }
}
