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

public override void OnBulletDestroyed(BulletController bullet)
    {
        Vector2 pos = bullet.transform.position;

        var enemyHits = Physics2D.OverlapCircleAll(pos, explosionRadius, bullet.EnemyLayerMask);
        foreach (var hit in enemyHits)
        {
            BulletDamageDispatcher.ApplyDamage(hit, explosionDamage, "폭발탄");
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
