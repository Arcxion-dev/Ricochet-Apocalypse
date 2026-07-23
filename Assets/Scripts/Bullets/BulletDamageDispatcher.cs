using UnityEngine;

/// <summary>
/// 총알 효과들이 적에게 데미지를 주려 할 때 거치는 단일 진입점.
///
/// 현재 Enemy(Entity)의 DecreaseHP는 protected라 외부에서 호출할 public API가
/// 아직 없고, 다른 담당자와의 충돌을 피하기 위해 이 부분만 의도적으로 로그 스텁으로 남깁니다.
/// 나중에 Enemy 쪽에 TakeDamage(int) 같은 public 메서드가 추가되면
/// 이 클래스의 ApplyDamage 내부 한 곳만 실제 호출로 교체하면 전체 효과들이 자동으로 연동됩니다.
/// </summary>
public static class BulletDamageDispatcher
{
    public static void ApplyDamage(Collider2D enemyCollider, float damage, string sourceLabel)
    {
        // TODO: Enemy에 public TakeDamage(int amount)가 추가되면 아래 로그 대신 실제 호출로 교체
        // var enemy = enemyCollider.GetComponent<Enemy>();
        // enemy?.TakeDamage(Mathf.RoundToInt(damage));
        Debug.Log($"[BulletDamageDispatcher] ({sourceLabel}) {enemyCollider.name}에게 데미지 {damage} 적용 필요 (Enemy.TakeDamage public API 대기 중)");
    }
}
