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
        Debug.Log($"[전력탄] {enemy.name} 시작으로 체인 최대 {maxChainCount}회, 탐색반경 {chainSearchRadius}, 감쇠율 {damageFalloffPerChain} (적 시스템 미구현 - 실제 체인 탐색/전이 로직 필요)");
    }
}
