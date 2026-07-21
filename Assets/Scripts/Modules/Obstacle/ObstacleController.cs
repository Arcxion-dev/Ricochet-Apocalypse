using UnityEngine;

/// <summary>
/// 장애물 객체의 총괄 컨트롤러.
/// 총알 객체가 만들어지면, 총알 스크립트의 충돌 처리에서 OnBulletHit(...)을 호출하도록 연결하세요.
/// </summary>
[RequireComponent(typeof(HealthModule))]
public class ObstacleController : MonoBehaviour
{
    private HealthModule healthModule;
    private ReflectModule reflectModule;
    private DeathModule deathModule;
    private InteractModule interactModule;

    private void Awake()
    {
        healthModule = GetComponent<HealthModule>();
        reflectModule = GetComponent<ReflectModule>();
        deathModule = GetComponent<DeathModule>();
        interactModule = GetComponent<InteractModule>();
    }

    /// <summary>
    /// 총알이 이 장애물과 충돌했을 때 호출될 예정인 진입점 (추후 Bullet 스크립트에서 연결).
    /// 사망(즉시 게임오버) 판정 -> 조작(특수기능) 판정 -> 체력 감소 순으로 처리됩니다.
    /// 반사 여부는 Bullet 쪽 물리 로직에서 reflectModule.ReflectsBullets 값을 참고해 처리하세요.
    /// </summary>
    public void OnBulletHit(float damage)
    {
        if (deathModule != null && deathModule.TriggersGameOverOnHit)
        {
            deathModule.TriggerGameOver();
            return;
        }

        interactModule?.TriggerInteraction();
        healthModule?.TakeDamage(damage);
    }

    public bool ShouldReflect => reflectModule != null && reflectModule.ReflectsBullets;
}
