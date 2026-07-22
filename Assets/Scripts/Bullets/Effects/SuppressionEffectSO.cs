using UnityEngine;

/// <summary>
/// 4. 저지탄 - 상대의 이동을 일시적으로 저지(슬로우/스턴 등)합니다.
/// </summary>
[CreateAssetMenu(fileName = "SuppressionEffect", menuName = "Bullet/Effects/저지탄 (Suppression)")]
public class SuppressionEffectSO : BulletEffectSO
{
    [Tooltip("이동 저지 지속 시간(초)")]
    public float suppressDuration = 1.5f;

    [Range(0f, 1f)]
    [Tooltip("이동속도 감소율 (1 = 완전 정지)")]
    public float slowRatio = 1f;

public override void OnHitEnemy(BulletController bullet, Collider2D enemy)
    {
        var suppressible = enemy.GetComponent<ISuppressible>();
        if (suppressible != null)
        {
            suppressible.ApplySuppression(suppressDuration, slowRatio);
            Debug.Log($"[저지탄] {enemy.name} 이동 저지 {slowRatio * 100}%, {suppressDuration}초간 적용됨");
        }
        else
        {
            Debug.Log($"[저지탄] {enemy.name}에 ISuppressible 구현체 없음 - 이동 저지 미적용 (적 이동 시스템 미구현)");
        }
    }
}
