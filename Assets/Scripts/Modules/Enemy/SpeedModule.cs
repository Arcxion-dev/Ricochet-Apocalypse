using UnityEngine;

/// <summary>
/// 속도 모듈: 기본 속도와 배수를 분리해서, 배수만 바꿔 속도를 조절할 수 있게 합니다.
/// </summary>
public class SpeedModule : MonoBehaviour
{
    [Header("속도 설정")]
    [SerializeField] private float baseSpeed = 3f;
    [SerializeField] private float speedMultiplier = 1f;

    public float BaseSpeed => baseSpeed;
    public float SpeedMultiplier => speedMultiplier;
    public float CurrentSpeed => baseSpeed * speedMultiplier;

    public void SetBaseSpeed(float value) => baseSpeed = value;
    public void SetSpeedMultiplier(float value) => speedMultiplier = value;
}
