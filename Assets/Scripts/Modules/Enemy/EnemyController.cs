using UnityEngine;

/// <summary>
/// 적 객체의 총괄 컨트롤러.
/// 총알 객체가 만들어지면, 총알 스크립트의 충돌 처리에서 OnBulletHit(...)을 호출하도록 연결하세요.
/// </summary>
[RequireComponent(typeof(HealthModule))]
public class EnemyController : MonoBehaviour
{
    private HealthModule healthModule;
    private AttributeModule attributeModule;
    private DefenseModule defenseModule;

    private void Awake()
    {
        healthModule = GetComponent<HealthModule>();
        attributeModule = GetComponent<AttributeModule>();
        defenseModule = GetComponent<DefenseModule>();
    }

    /// <summary>
    /// 총알이 이 적과 충돌했을 때 호출될 예정인 진입점 (추후 Bullet 스크립트에서 연결).
    /// 방어 모듈의 무적 판정 -> 속성 모듈의 대미지 배수 -> 체력 모듈 순으로 처리됩니다.
    /// </summary>
    public void OnBulletHit(float baseDamage, ElementType bulletElement)
    {
        if (defenseModule != null && defenseModule.TryBlockHit())
            return; // 무적 판정으로 피해 무시

        float multiplier = attributeModule != null ? attributeModule.GetDamageMultiplier(bulletElement) : 1f;
        healthModule.TakeDamage(baseDamage * multiplier);
    }
}
