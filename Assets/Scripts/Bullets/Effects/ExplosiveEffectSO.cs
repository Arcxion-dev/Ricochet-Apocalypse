using UnityEngine;

/// <summary>
/// 2. 폭발탄 - 주위 적에게 폭발 대미지. 다수에게 효과적.
/// 바위(장애물)를 파괴할 수 있는 특성도 겸함.
/// </summary>
[CreateAssetMenu(fileName = "ExplosiveEffect", menuName = "Bullet/Effects/폭발탄 (Explosive)")]
public class ExplosiveEffectSO : BulletEffectSO
{
    [Tooltip("폭발 반경")]
    public float explosionRadius = 3f;

    [Tooltip("폭발 데미지")]
    public float explosionDamage = 20f;

    [Tooltip("바위 등 파괴 가능한 장애물을 부술 수 있는지")]
    public bool canDestroyRock = true;

    // 적을 직격한 순간 즉시 폭발하고 총알을 소멸시킨다. 관통 후 수명이 다한 자리에서
    // 뒤늦게 터지던(사실상 무효) 문제를 없애, "쏜 곳에서 바로 광역 폭발"하도록 한다.
    public override void OnHitEnemy(BulletController bullet, Collider2D enemy)
    {
        Detonate(bullet);
        bullet.Kill(); // 그 자리에서 소멸(관통 금지). Kill→Die→OnBulletDestroyed는 가드로 재폭발 방지.
    }

    // 적을 맞히지 못하고 벽/수명으로 소멸할 때도 그 자리에서 폭발(바위 파괴 포함)한다.
    public override void OnBulletDestroyed(BulletController bullet)
    {
        Detonate(bullet);
    }

    /// <summary>현재 총알 위치를 중심으로 광역 폭발을 1회 처리한다. 한 총알에 대해 중복 발동하지 않는다.</summary>
    private void Detonate(BulletController bullet)
    {
        var effectType = GetType();
        if (bullet.HasTriggeredZoneEffect(effectType)) return; // 이 총알은 이미 폭발함(중복 방지).
        bullet.MarkZoneEffectTriggered(effectType);

        Vector2 pos = bullet.transform.position;

        bool hasArmorPiercing = bullet.Data.HasEffect<ArmorPiercingEffectSO>();
        var enemyHits = Physics2D.OverlapCircleAll(pos, explosionRadius, bullet.EnemyLayerMask);
        foreach (var hit in enemyHits)
        {
            BulletDamageDispatcher.ApplyDamage(hit, explosionDamage, "폭발탄", bullet.Data, hasArmorPiercing);
        }

        if (canDestroyRock)
        {
            var wallHits = Physics2D.OverlapCircleAll(pos, explosionRadius, bullet.WallLayerMask);
            foreach (var hit in wallHits)
            {
                var destructible = hit.GetComponent<DestructibleObstacle>();
                if (destructible != null)
                {
                    destructible.ApplyExplosionDamage(explosionDamage);
                }
            }
        }

        Debug.Log($"[폭발탄] 위치 {pos}에서 반경 {explosionRadius} 내 적 {enemyHits.Length}기 폭발 피해 {explosionDamage} 적용");
    }
}
