using UnityEngine;

/// <summary>
/// 적 객체의 총괄 컨트롤러.
/// 총알이 적중했을 때 BulletController가 이 컴포넌트의 OnBulletHit(...)을 호출합니다.
///
/// 체력 처리 우선순위:
/// 1) 같은 오브젝트에 친구가 만든 Entity/Enemy 컴포넌트가 있으면 그쪽 체력(entity.health)을 사용합니다.
///    (DefenseModule 무적 판정 -> AttributeModule 대미지 배수 계산 후 entity.TakeDamage(최종대미지) 호출)
/// 2) Entity가 없는 경우에만 자체 HealthModule을 예비로 사용합니다.
/// </summary>
public class EnemyController : MonoBehaviour
{
    private HealthModule healthModule;
    private AttributeModule attributeModule;
    private DefenseModule defenseModule;
    private HeadshotModule headshotModule;
    private ArmorModule armorModule;
    private TraitModule traitModule;
    private SpeedModule speedModule;
    private Entity entity; // 친구가 만든 체력 시스템 (Enemy : Entity)

private void Awake()
    {
        healthModule = GetComponent<HealthModule>();
        attributeModule = GetComponent<AttributeModule>();
        defenseModule = GetComponent<DefenseModule>();
        headshotModule = GetComponent<HeadshotModule>();
        armorModule = GetComponent<ArmorModule>();
        traitModule = GetComponent<TraitModule>();
        speedModule = GetComponent<SpeedModule>();
        entity = GetComponent<Entity>();

        ApplyTraitSpeedModifier();

        // Entity가 없어 자체 HealthModule을 예비로 쓰는 경우, 사망 시 오브젝트를 파괴합니다.
        // (Entity가 있는 경우의 사망 처리는 Enemy.DecreaseHP 쪽에서 담당)
        if (entity == null && healthModule != null)
        {
            healthModule.OnDeath += HandleHealthModuleDeath;
        }
    }

    private void HandleHealthModuleDeath()
    {
        Destroy(gameObject); // 임시: 추후 사망 연출/드랍 등으로 교체 가능
    }

/// <summary>
    /// 특성 모듈에 "신속" 특성이 있으면 이동속도 배수를 1.5배로, 없으면 기본 1배로 맞춥니다.
    /// 특성/속도 모듈 값이 바뀔 때마다 이 메서드를 다시 호출하면 재적용됩니다.
    /// </summary>
    public void ApplyTraitSpeedModifier()
    {
        if (speedModule == null) return;

        bool hasHaste = traitModule != null && traitModule.HasTrait("신속");
        speedModule.SetSpeedMultiplier(hasHaste ? 1.5f : 1f);
    }


    /// <summary>
    /// 총알이 이 적과 충돌했을 때 호출되는 진입점.
    /// </summary>
    /// <param name="baseDamage">총알의 기본 데미지 (BulletSO.damage)</param>
    /// <param name="bulletElement">총알의 속성 (아직 총알 쪽에 속성 필드가 없다면 ElementType.None 전달)</param>
public void OnBulletHit(float baseDamage, ElementType bulletElement, bool isArmorPiercingBullet = false)
    {
        if (defenseModule != null && defenseModule.TryBlockHit())
            return; // 무적 판정으로 피해 무시

        float finalDamage = baseDamage;

        bool isHeadshot = false;
        if (headshotModule != null)
        {
            finalDamage *= headshotModule.RollDamageMultiplier(out isHeadshot);
        }

        if (attributeModule != null)
        {
            finalDamage *= attributeModule.GetDamageMultiplier(bulletElement);
        }

        if (armorModule != null)
        {
            finalDamage *= armorModule.GetDamageMultiplier(isArmorPiercingBullet);
        }

        if (isHeadshot)
        {
            Debug.Log($"{name}: 헤드샷! 최종 대미지 {finalDamage}");
        }

        if (entity != null)
        {
            entity.TakeDamage(Mathf.RoundToInt(finalDamage));
        }
        else if (healthModule != null)
        {
            healthModule.TakeDamage(finalDamage);
        }
    }
}
