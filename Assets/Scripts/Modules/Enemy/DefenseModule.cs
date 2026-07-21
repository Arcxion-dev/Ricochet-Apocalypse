using UnityEngine;

/// <summary>
/// 방어 모듈: 방어구 착용 여부에 따라 N회 무적(피격 무시) 판정을 부여합니다.
/// 무적 횟수를 모두 소진하면 이후 피격은 정상적으로 대미지가 들어갑니다.
/// </summary>
public class DefenseModule : MonoBehaviour
{
    [Header("방어구 설정")]
    [SerializeField] private bool hasArmor = false;
    [SerializeField] private int maxInvincibleHits = 0;

    private int remainingInvincibleHits;

    public bool HasArmor => hasArmor;
    public int RemainingInvincibleHits => remainingInvincibleHits;

    private void Awake()
    {
        remainingInvincibleHits = hasArmor ? maxInvincibleHits : 0;
    }

    /// <summary>
    /// 피격 시 호출. true를 반환하면 이번 피격은 무적으로 무시된 것입니다.
    /// </summary>
    public bool TryBlockHit()
    {
        if (!hasArmor || remainingInvincibleHits <= 0) return false;
        remainingInvincibleHits--;
        return true;
    }

    public void SetArmor(bool equipped, int invincibleHits)
    {
        hasArmor = equipped;
        maxInvincibleHits = invincibleHits;
        remainingInvincibleHits = equipped ? invincibleHits : 0;
    }
}
