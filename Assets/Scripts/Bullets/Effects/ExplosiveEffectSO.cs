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
        Debug.Log($"[폭발탄] 위치 {bullet.transform.position}에서 반경 {explosionRadius} 내 폭발, 데미지 {explosionDamage} (적/파괴가능 장애물 시스템 미구현 - 실제 범위 판정 필요)");
    }
}
