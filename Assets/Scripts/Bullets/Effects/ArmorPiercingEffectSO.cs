using UnityEngine;

/// <summary>
/// 1. 철갑탄 - 벽 관통 가능, 철갑을 두른 적에게 효과적.
/// 실제 관통 여부 판정은 BulletController.OnObstacleHit 쪽에서
/// BulletSO.HasEffect&lt;ArmorPiercingEffectSO&gt;() 를 확인해서 처리합니다.
/// (적 대상 추가 데미지는 적 시스템 부재로 스텁)
/// </summary>
[CreateAssetMenu(fileName = "ArmorPiercingEffect", menuName = "Bullet/Effects/철갑탄 (ArmorPiercing)")]
public class ArmorPiercingEffectSO : BulletEffectSO
{
    [Tooltip("철갑 적 대상 데미지 배율")]
    public float armoredEnemyDamageMultiplier = 1.5f;

    public override void OnHitEnemy(BulletController bullet, Collider2D enemy)
    {
        Debug.Log($"[철갑탄] {enemy.name} 적중 - 철갑 여부 확인 및 데미지 배율({armoredEnemyDamageMultiplier}) 적용 필요 (적 시스템 미구현)");
    }

    public override void OnHitObstacle(BulletController bullet, Collider2D obstacle, BulletTargetType targetType)
    {
        Debug.Log($"[철갑탄] {targetType} 충돌 - 관통 가능 여부는 BulletController에서 처리됨");
    }
}
