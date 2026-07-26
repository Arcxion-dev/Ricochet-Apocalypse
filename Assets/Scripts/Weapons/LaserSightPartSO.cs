using UnityEngine;

/// <summary>
/// 레이저(조준선) 파츠. 직선 레이저는 기본 상시 표시이므로, 이 파츠는 "업그레이드" 역할로
/// 조준 가이드 사정거리를 늘리고 벽 반사 예측(꺾이는 궤적선)을 추가한다.
/// 여러 개 끼우면 사정거리·반사 횟수가 합연산으로 늘어난다.
/// </summary>
[CreateAssetMenu(fileName = "Part_Laser", menuName = "Weapon/Parts/레이저 (Laser Sight, 사거리/반사)")]
public class LaserSightPartSO : WeaponPartSO
{
    [Tooltip("조준 가이드 사정거리 추가분(월드 유닛). 기본 사정거리와 합연산된다.")]
    public float range = 8f;

    [Tooltip("추가로 예측할 벽 반사 횟수. 클수록 더 많이 꺾인 궤적선을 보여준다.")]
    public int extraBounces = 2;

    public override void Contribute(ref WeaponStats stats)
    {
        stats.laserEnabled = true;
        stats.laserRange += range;
        stats.predictBounces += extraBounces;
    }
}
