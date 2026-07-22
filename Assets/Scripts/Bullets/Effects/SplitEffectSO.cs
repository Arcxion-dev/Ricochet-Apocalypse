using UnityEngine;

/// <summary>
/// 3. 분열탄 - 적중 또는 벽 튕김 시 분열 (트리거 선택 가능, Flags 조합 가능).
/// 분열 시 생성될 자식 총알 BulletSO를 지정합니다 (무한 분열 방지를 위해
/// 보통 자식 총알에는 SplitEffectSO를 넣지 않는 것을 권장).
/// </summary>
[CreateAssetMenu(fileName = "SplitEffect", menuName = "Bullet/Effects/분열탄 (Split)")]
public class SplitEffectSO : BulletEffectSO
{
    [Tooltip("분열이 발동되는 조건 (복수 선택 가능)")]
    public SplitTrigger trigger = SplitTrigger.OnEnemyHit | SplitTrigger.OnWallBounce;

    [Tooltip("분열되어 생성될 자식 총알 개수")]
    public int splitCount = 3;

    [Tooltip("분열 시 자식 총알들이 퍼지는 전체 각도(도)")]
    public float spreadAngle = 90f;

    [Tooltip("분열 후 생성될 자식 총알 BulletSO. 비워두면 자기 자신의 BulletSO를 재사용(효과 제외 권장)")]
    public BulletSO childBulletSO;

    [Tooltip("OnTimer 트리거 사용 시 분열까지 걸리는 시간(초)")]
    public float timerDuration = 1f;

public override void OnHitEnemy(BulletController bullet, Collider2D enemy)
    {
        if (bullet.IsSplitChild) return; // 무한 분열 방지
        if ((trigger & SplitTrigger.OnEnemyHit) != 0)
        {
            DoSplit(bullet, bullet.Direction);
        }
    }

public override void OnHitObstacle(BulletController bullet, Collider2D obstacle, BulletTargetType targetType)
    {
        if (bullet.IsSplitChild) return;
        if ((trigger & SplitTrigger.OnWallBounce) != 0)
        {
            DoSplit(bullet, bullet.Direction);
        }
    }

public override void OnInit(BulletController bullet)
    {
        if (bullet.IsSplitChild) return;
        if ((trigger & SplitTrigger.OnTimer) != 0)
        {
            bullet.StartCoroutine(TimerSplitRoutine(bullet));
        }
    }


private System.Collections.IEnumerator TimerSplitRoutine(BulletController bullet)
    {
        yield return new WaitForSeconds(timerDuration);
        if (bullet != null)
        {
            DoSplit(bullet, bullet.Direction);
        }
    }

    private void DoSplit(BulletController bullet, Vector2 baseDirection)
    {
        Debug.Log($"[분열탄] 분열 발동 - {splitCount}발, 퍼짐각 {spreadAngle}도");

        if (splitCount <= 0) return;

        float baseAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;
        float startAngle = baseAngle - spreadAngle * 0.5f;
        float step = splitCount > 1 ? spreadAngle / (splitCount - 1) : 0f;

        for (int i = 0; i < splitCount; i++)
        {
            float angle = splitCount > 1 ? startAngle + step * i : baseAngle;
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            bullet.SpawnChildBullet(childBulletSO, dir);
        }
    }
}
