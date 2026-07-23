using UnityEngine;

/// <summary>
/// 헤드샷 모듈: 총알이 적중할 때마다 일정 확률로 헤드샷 판정을 내어 대미지를 배수로 늘립니다.
/// </summary>
public class HeadshotModule : MonoBehaviour
{
    [Header("헤드샷 설정")]
    [Range(0f, 1f)]
    [SerializeField] private float headshotChance = 0.3f;
    [SerializeField] private float headshotMultiplier = 2f;

    public float HeadshotChance => headshotChance;
    public float HeadshotMultiplier => headshotMultiplier;

    /// <summary>
    /// 피격 시 1회 호출. 확률 판정을 굴려서 대미지 배수를 반환합니다.
    /// </summary>
    /// <param name="isHeadshot">이번 판정이 헤드샷이었는지 여부</param>
    public float RollDamageMultiplier(out bool isHeadshot)
    {
        isHeadshot = Random.value < headshotChance;
        return isHeadshot ? headshotMultiplier : 1f;
    }

    public void SetHeadshotChance(float value) => headshotChance = Mathf.Clamp01(value);
    public void SetHeadshotMultiplier(float value) => headshotMultiplier = value;
}
