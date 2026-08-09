using UnityEngine;

/// <summary>
/// 조준경 파츠(재설계). 더 이상 조준선을 그리지 않는다(직선 레이저는 기본 상시 표시).
/// 대신 헤드샷(크리티컬) 명중 시의 대미지 배수를 <see cref="headshotBonus"/>만큼 증가시킨다.
/// 예: 0.2면 헤드샷 배수 ×1.2(= +20%).
/// </summary>
[CreateAssetMenu(fileName = "Part_Scope", menuName = "Weapon/Parts/조준경 (Scope, 헤드샷 배율)")]
public class ScopePartSO : WeaponPartSO
{
    [Range(0f, 1f)]
    [Tooltip("헤드샷 배수 증가 비율. 0.2 = 헤드샷 대미지 +20%(배수 ×1.2). 여러 개면 합연산된다.")]
    public float headshotBonus = 0.2f;

    public override void Contribute(ref WeaponStats stats)
    {
        stats.headshotMultiplierBonus += headshotBonus;
    }
}
