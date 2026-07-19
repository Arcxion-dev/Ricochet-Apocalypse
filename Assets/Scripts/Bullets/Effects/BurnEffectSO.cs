using UnityEngine;

/// <summary>
/// 8. 화상탄 - 최초로 직격한 적을 기준으로 주변에 화염지대 생성.
/// </summary>
[CreateAssetMenu(fileName = "BurnEffect", menuName = "Bullet/Effects/화상탄 (Burn)")]
public class BurnEffectSO : ZoneEffectSOBase
{
    protected override string EffectLabel => "화상탄";
}
