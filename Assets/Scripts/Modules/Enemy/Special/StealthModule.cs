using UnityEngine;

/// <summary>
/// 은신 모듈: 스프라이트 알파를 낮춰 화면에 거의 보이지 않게 한다.
/// 순수 시각 효과이며 충돌/AI/피격 판정에는 영향을 주지 않는다.
/// </summary>
public class StealthModule : MonoBehaviour
{
    [Header("은신 설정")]
    [Range(0f, 1f)]
    [Tooltip("평상시 스프라이트 알파값. 0에 가까울수록 잘 안 보임.")]
    [SerializeField] private float stealthAlpha = 0.12f;

    private void Awake()
    {
        var spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null) return;

        var color = spriteRenderer.color;
        color.a = stealthAlpha;
        spriteRenderer.color = color;
    }
}
