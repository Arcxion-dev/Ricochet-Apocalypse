using UnityEngine;

/// <summary>
/// 7. 전력탄 - 근처 몬스터에게 체인 형식으로 피해를 줌 (스플래시가 아니라 순차 전이).
/// </summary>
[CreateAssetMenu(fileName = "ChainLightningEffect", menuName = "Bullet/Effects/전력탄 (ChainLightning)")]
public class ChainLightningEffectSO : BulletEffectSO
{
    [Tooltip("체인이 튈 수 있는 최대 대상 수")]
    public int maxChainCount = 3;

    [Tooltip("체인이 다음 대상을 찾는 탐색 반경")]
    public float chainSearchRadius = 5f;

    [Tooltip("체인당 데미지 감소율 (1회 전이마다 곱해짐)")]
    [Range(0f, 1f)]
    public float damageFalloffPerChain = 0.8f;

public override void OnHitEnemy(BulletController bullet, Collider2D enemy)
    {
        var visited = new System.Collections.Generic.HashSet<Collider2D> { enemy };
        Collider2D current = enemy;
        float currentDamage = bullet.Data.damage;
        int chainCount = 0;

        Debug.Log($"[전력탄] {enemy.name} 시작으로 체인 발동");

        while (chainCount < maxChainCount)
        {
            var hits = Physics2D.OverlapCircleAll(current.transform.position, chainSearchRadius, bullet.EnemyLayerMask);
            Collider2D next = null;
            float nearestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                if (visited.Contains(hit)) continue;
                float d = Vector2.Distance(current.transform.position, hit.transform.position);
                if (d < nearestDist)
                {
                    nearestDist = d;
                    next = hit;
                }
            }

            if (next == null) break;

            currentDamage *= damageFalloffPerChain;
            BulletDamageDispatcher.ApplyDamage(next, currentDamage, $"전력탄 체인 {chainCount + 1}");

            visited.Add(next);
            current = next;
            chainCount++;
        }

        Debug.Log($"[전력탄] 체인 {chainCount}회 전이 완료");
    }
}
