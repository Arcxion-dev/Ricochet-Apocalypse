using UnityEngine;

/// <summary>
/// 조준경 파츠. 조준 가이드라인이 벽 반사를 예측해 꺾이는 궤적선을 그리게 한다(가이드선 증가).
/// 레이저 파츠와 함께 끼우면 사정거리가 합연산으로 늘어나 "함께 쓰면 더 좋게" 동작한다.
/// </summary>
[CreateAssetMenu(fileName = "Part_Scope", menuName = "Weapon/Parts/조준경 (Scope)")]
public class ScopePartSO : WeaponPartSO
{
    [Tooltip("추가로 예측할 벽 반사 횟수. 클수록 더 많이 꺾인 궤적선을 보여준다.")]
    public int extraBounces = 2;

    [Tooltip("궤적 예측 사정거리 추가분(월드 유닛). 레이저 사정거리와 합연산된다.")]
    public float extraRange = 6f;

    public override void Contribute(ref WeaponStats stats)
    {
        stats.laserEnabled = true; // 조준경만 껴도 가이드라인은 보이도록.
        stats.predictBounces += extraBounces;
        stats.predictRange += extraRange;
    }
}
