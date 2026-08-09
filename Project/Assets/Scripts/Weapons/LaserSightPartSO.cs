using UnityEngine;

/// <summary>
/// 레이저(조준선) 파츠. 직선 레이저는 기본 상시 표시이므로, 이 파츠는 "업그레이드" 역할로
/// 조준 가이드의 <b>직선 사정거리</b>를 늘린다. 벽 반사 궤적 예측은 별도의
/// <see cref="ReflectionSightPartSO"/>(반사 예측기) 파츠가 담당한다.
/// 여러 개 끼우면 사정거리가 합연산으로 늘어난다.
/// </summary>
[CreateAssetMenu(fileName = "Part_Laser", menuName = "Weapon/Parts/레이저 (Laser Sight, 사거리)")]
public class LaserSightPartSO : WeaponPartSO
{
    [Tooltip("조준 가이드 사정거리 추가분(월드 유닛). 기본 사정거리와 합연산된다.")]
    public float range = 8f;

    public override void Contribute(ref WeaponStats stats)
    {
        stats.laserEnabled = true;
        stats.laserRange += range;
    }
}
