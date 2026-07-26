using UnityEngine;

/// <summary>
/// 강선(라이플링) 파츠. 기본 장착 파츠이며, 강화도(레벨)를 올릴 때마다 탄환 대미지가 레벨당 +25%씩 증가한다.
/// 예: 1강 → 데미지 +25%(×1.25), 2강 → +50%(×1.5), 3강 → +75%(×1.75) ...
///
/// 강화 상태는 이 에셋의 <see cref="upgradeLevel"/> 에 저장된다. 로그라이크 한 판 동안 강화가 누적되도록
/// <see cref="PlayerShooter.UpgradeRifling"/> 가 이 값을 올리고 <see cref="PlayerShooter.RecomputeParts"/> 로 반영한다.
/// </summary>
[CreateAssetMenu(fileName = "Part_Rifling", menuName = "Weapon/Parts/강선 (Rifling, 대미지 강화)")]
public class RiflingPartSO : WeaponPartSO
{
    [Min(0)]
    [Tooltip("강선 강화도(레벨). 0=기본. 레벨당 damagePerLevel 만큼 대미지 증가.")]
    public int upgradeLevel = 0;

    [Range(0f, 2f)]
    [Tooltip("강화도 1레벨당 대미지 증가율. 0.25 = 레벨당 +25%.")]
    public float damagePerLevel = 0.25f;

    /// <summary>현재 강화도가 주는 대미지 배율(×). 예: 2강·0.25 → 1.5.</summary>
    public float DamageMultiplier => 1f + damagePerLevel * upgradeLevel;

    public override void Contribute(ref WeaponStats stats)
    {
        stats.damageMultiplier *= DamageMultiplier;
    }

    /// <summary>강화도를 1 올린다(강화 1회). 실제 반영은 호출측이 RecomputeParts를 부른다.</summary>
    public void Upgrade() => upgradeLevel++;

    /// <summary>강화도를 지정 값으로 설정한다(0 미만 방지).</summary>
    public void SetLevel(int level) => upgradeLevel = Mathf.Max(0, level);

    public override string DisplayName =>
        $"강선 +{upgradeLevel}  (데미지 +{Mathf.RoundToInt(damagePerLevel * upgradeLevel * 100f)}%)";
}
