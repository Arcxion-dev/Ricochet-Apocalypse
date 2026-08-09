using UnityEngine;

/// <summary>
/// 네뷸라이저 파츠. 조준(차징) 시 슬로우모션을 더 강하게(더 느리게) 만든다.
/// <see cref="ChargeShotEffects"/>의 목표 타임스케일에 <see cref="slowScaleMultiplier"/>를 곱해
/// 값이 더 작아지도록(=더 느리도록) 한다.
/// </summary>
[CreateAssetMenu(fileName = "Part_Nebulizer", menuName = "Weapon/Parts/네뷸라이저 (Nebulizer)")]
public class NebulizerPartSO : WeaponPartSO
{
    [Range(0.1f, 1f)]
    [Tooltip("차징 슬로우 배율. 0.6 = 기존보다 더 느리게(슬로우 강화). 작을수록 더 느리다.")]
    public float slowScaleMultiplier = 0.6f;

    public override void Contribute(ref WeaponStats stats)
    {
        stats.slowScaleMultiplier *= slowScaleMultiplier;
    }
}
