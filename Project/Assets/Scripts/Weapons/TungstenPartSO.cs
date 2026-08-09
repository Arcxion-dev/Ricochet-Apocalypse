using UnityEngine;

/// <summary>
/// 텅스텐 탄환 파츠. 자연(확률) 헤드샷이 명중하면, 그 다음에 맞을 상대를 "확정 헤드샷"으로 만든다.
/// 확정 헤드샷 자체는 다음 발을 다시 예약하지 않으므로 무한 연쇄되지 않는다(자연 헤드샷만 재예약).
/// 예약/소모 상태는 <see cref="PlayerShooter"/>가 무기 단위로 관리한다.
/// </summary>
[CreateAssetMenu(fileName = "Part_Tungsten", menuName = "Weapon/Parts/텅스텐 탄환 (Tungsten)")]
public class TungstenPartSO : WeaponPartSO
{
    public override void Contribute(ref WeaponStats stats)
    {
        stats.guaranteedHeadshotChain = true;
    }
}
