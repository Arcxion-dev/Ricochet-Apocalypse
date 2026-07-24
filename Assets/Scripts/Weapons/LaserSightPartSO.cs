using UnityEngine;

/// <summary>
/// 레이저(조준선) 파츠. 기본 조준 가이드라인을 켜고 직선 사정거리를 제공한다.
/// 조준경과 함께 끼우면 사정거리가 합연산으로 늘어난다.
/// </summary>
[CreateAssetMenu(fileName = "Part_Laser", menuName = "Weapon/Parts/레이저 (Laser Sight)")]
public class LaserSightPartSO : WeaponPartSO
{
    [Tooltip("직선 조준 가이드 사정거리(월드 유닛). 조준경 사정거리와 합연산된다.")]
    public float range = 8f;

    public override void Contribute(ref WeaponStats stats)
    {
        stats.laserEnabled = true;
        stats.laserRange += range;
    }
}
