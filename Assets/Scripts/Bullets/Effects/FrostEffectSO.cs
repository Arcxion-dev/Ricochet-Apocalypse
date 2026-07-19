using UnityEngine;

/// <summary>
/// 9. 냉기탄 - 최초로 직격한 적을 기준으로 주변에 냉기지대 생성.
/// </summary>
[CreateAssetMenu(fileName = "FrostEffect", menuName = "Bullet/Effects/냉기탄 (Frost)")]
public class FrostEffectSO : ZoneEffectSOBase
{
    protected override string EffectLabel => "냉기탄";
}
