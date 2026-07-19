using UnityEngine;

/// <summary>
/// 6. 중력자탄 - 주변의 적을 끌어모은 뒤 폭발합니다.
/// </summary>
[CreateAssetMenu(fileName = "GravityEffect", menuName = "Bullet/Effects/중력자탄 (Gravity)")]
public class GravityEffectSO : BulletEffectSO
{
    [Tooltip("적을 끌어당기는 범위")]
    public float pullRadius = 4f;

    [Tooltip("끌어당기는 힘(적을 총알 쪽으로 당기는 속도)")]
    public float pullForce = 5f;

    [Tooltip("끌어모으는 지속 시간(초) - 이후 폭발")]
    public float pullDuration = 1f;

    [Tooltip("최종 폭발 데미지")]
    public float finalExplosionDamage = 25f;

    public override void OnBulletDestroyed(BulletController bullet)
    {
        Debug.Log($"[중력자탄] 위치 {bullet.transform.position} 에서 반경 {pullRadius} 내 적 {pullDuration}초간 끌어모은 뒤 데미지 {finalExplosionDamage} 폭발 (적 시스템 미구현 - 실제 인력/폭발 로직 필요)");
    }
}
