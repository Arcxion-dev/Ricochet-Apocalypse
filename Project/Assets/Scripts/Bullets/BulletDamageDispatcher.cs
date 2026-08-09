using UnityEngine;

/// <summary>
/// 총알 효과들이 적에게 데미지를 주려 할 때 거치는 단일 진입점.
///
/// EnemyController가 있는 적은 그쪽 파이프라인(방어 무적 -> 속성/철갑 배수 -> 방패/체력)을 그대로 태워
/// 폭발/장판 틱 같은 광역 대미지도 직격과 동일한 규칙으로 처리되게 한다.
/// EnemyController가 없는 대상(테스트용 오브젝트 등)은 기존처럼 Entity에 raw로 적용하는 폴백을 유지한다.
/// </summary>
public static class BulletDamageDispatcher
{
    /// <param name="damage">기본(배수 적용 전) 대미지. precomputed가 true면 이미 최종 계산이 끝난 값.</param>
    /// <param name="bulletData">이 대미지를 유발한 총알 데이터(속성 취약 배수 계산용). 없으면 null.</param>
    /// <param name="isArmorPiercingBullet">철갑탄 여부(Armor 배수 계산용).</param>
    /// <param name="ignoreArmor">true면 Armor 배수를 건너뛴다(장판 틱은 철갑 배수를 타지 않는다는 밸런스 규칙).</param>
    /// <param name="precomputed">true면 damage가 이미 모든 배수/즉사 판정까지 끝난 최종값이므로 추가 배수를 곱하지 않는다(장판 틱 전용).</param>
    public static void ApplyDamage(Collider2D enemyCollider, float damage, string sourceLabel,
        BulletSO bulletData = null, bool isArmorPiercingBullet = false, bool ignoreArmor = false, bool precomputed = false)
    {
        var enemyController = enemyCollider.GetComponentInParent<EnemyController>();
        if (enemyController != null)
        {
            if (precomputed)
                enemyController.ApplyPrecomputedDamage(damage);
            else
                enemyController.ApplyAreaDamage(damage, bulletData, isArmorPiercingBullet, ignoreArmor);

            Debug.Log($"[BulletDamageDispatcher] ({sourceLabel}) {enemyCollider.name}에게 데미지 {damage} 적용 (EnemyController 경유)");
            return;
        }

        // EnemyController가 없는 대상(테스트 오브젝트 등): 기존처럼 Entity에 raw 적용.
        var entity = enemyCollider.GetComponentInParent<Entity>();
        if (entity != null)
        {
            int amount = Mathf.RoundToInt(damage);
            entity.TakeDamage(amount);
            Debug.Log($"[BulletDamageDispatcher] ({sourceLabel}) {enemyCollider.name}에게 데미지 {amount} 적용 (raw fallback)");
        }
        else
        {
            Debug.Log($"[BulletDamageDispatcher] ({sourceLabel}) {enemyCollider.name}: Entity가 없어 데미지 미적용");
        }
    }
}
