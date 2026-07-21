using UnityEngine;

/// <summary>
/// 반사 모듈: 접촉한 총알이 이 장애물에서 반사될지 여부를 판정합니다.
/// 실제 반사 벡터 계산은 Bullet 쪽 물리 로직에서 이 모듈의 값을 참고해 처리하세요.
/// </summary>
public class ReflectModule : MonoBehaviour
{
    [Header("반사 설정")]
    [SerializeField] private bool reflectsBullets = true;
    [SerializeField, Range(0f, 1f)] private float reflectDamageLoss = 0f; // 반사될 때 총알 위력 감쇠율

    public bool ReflectsBullets => reflectsBullets;
    public float ReflectDamageLoss => reflectDamageLoss;

    public void SetReflect(bool value) => reflectsBullets = value;
}
