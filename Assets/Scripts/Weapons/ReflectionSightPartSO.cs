using UnityEngine;

/// <summary>
/// 반사 예측기 파츠. 이 파츠를 장착해야만 조준선이 벽 반사 궤적(꺾이는 예측선)을 그린다.
/// 미장착 시 조준선은 첫 벽에서 멈추는 순수 직선이다(직선 레이저는 기본 상시 표시).
/// 여러 개 끼우면 반사 횟수·예측 사정거리가 합연산으로 늘어난다.
/// </summary>
[CreateAssetMenu(fileName = "Part_Reflect", menuName = "Weapon/Parts/반사 예측기 (Reflection Sight, 반사 궤적)")]
public class ReflectionSightPartSO : WeaponPartSO
{
    [Tooltip("예측할 벽 반사 횟수. 클수록 더 많이 꺾인 궤적선을 보여준다.")]
    public int bounces = 2;

    [Tooltip("반사 궤적 예측 사정거리 추가분(월드 유닛). 기본 사정거리와 합연산된다.")]
    public float extraRange = 6f;

    public override void Contribute(ref WeaponStats stats)
    {
        stats.reflectionEnabled = true;
        stats.predictBounces += bounces;
        stats.predictRange += extraRange;
    }
}
