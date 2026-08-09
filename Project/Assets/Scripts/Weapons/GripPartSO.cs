using UnityEngine;

/// <summary>
/// 손잡이 파츠. 조준(호흡) 시 조준선 흔들림을 줄인다.
/// <see cref="PlayerShooter"/>의 호흡 sway 진폭에 <see cref="swayMultiplier"/>를 곱한다.
/// </summary>
[CreateAssetMenu(fileName = "Part_Grip", menuName = "Weapon/Parts/손잡이 (Grip)")]
public class GripPartSO : WeaponPartSO
{
    [Range(0f, 1f)]
    [Tooltip("호흡 흔들림 진폭 배율. 0.5 = 흔들림 50% 감소. 여러 개 끼우면 곱해진다.")]
    public float swayMultiplier = 0.5f;

    public override void Contribute(ref WeaponStats stats)
    {
        stats.swayMultiplier *= swayMultiplier;
    }
}
