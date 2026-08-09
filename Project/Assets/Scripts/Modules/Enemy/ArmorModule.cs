using UnityEngine;

/// <summary>
/// 철갑 모듈: 이 모듈이 붙은 적은 철갑탄(ArmorPiercingEffectSO 보유 총알)을 제외한
/// 모든 탄약으로부터 받는 대미지가 감소합니다.
/// </summary>
public class ArmorModule : MonoBehaviour
{
    [Header("철갑 설정")]
    [Tooltip("철갑탄이 아닌 총알에 맞았을 때 적용되는 대미지 배수")]
    [SerializeField] private float nonPiercingMultiplier = 0.5f;

    [Tooltip("철갑탄에 맞았을 때 적용되는 대미지 배수")]
    [SerializeField] private float piercingMultiplier = 1f;

    public float NonPiercingMultiplier => nonPiercingMultiplier;
    public float PiercingMultiplier => piercingMultiplier;

    public float GetDamageMultiplier(bool isArmorPiercingBullet)
    {
        return isArmorPiercingBullet ? piercingMultiplier : nonPiercingMultiplier;
    }

    public void SetMultipliers(float nonPiercing, float piercing)
    {
        nonPiercingMultiplier = nonPiercing;
        piercingMultiplier = piercing;
    }
}
