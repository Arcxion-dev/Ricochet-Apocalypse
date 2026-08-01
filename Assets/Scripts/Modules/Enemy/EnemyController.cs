using UnityEngine;
using UnityEngine.AI;

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
    private FoliageConcealmentModule foliageConcealment;
    private ShieldModule shieldModule;
    private Entity entity; // 친구가 만든 체력 시스템 (Enemy : Entity)

    // 타격감 피드백(플래시/넉백)에 쓰는 참조. 없어도(2D 스프라이트가 아니거나 NavMeshAgent가 없어도) 안전하게 건너뛴다.
    private SpriteRenderer spriteRenderer;
    private NavMeshAgent navMeshAgent;

private void Awake()
    {
        healthModule = GetComponent<HealthModule>();
        attributeModule = GetComponent<AttributeModule>();
        defenseModule = GetComponent<DefenseModule>();
        // 헤드샷(확률) 시스템은 모든 적에 적용되도록 모듈이 없으면 자동 부착한다.
        // (기본값 30%/2배는 HeadshotModule 기본; 밸런스는 이후 튜닝 가능.)
        headshotModule = GetComponent<HeadshotModule>();
        if (headshotModule == null) headshotModule = gameObject.AddComponent<HeadshotModule>();
        armorModule = GetComponent<ArmorModule>();
        traitModule = GetComponent<TraitModule>();
        speedModule = GetComponent<SpeedModule>();
        // 은신(수풀/모래바람/아지랑이) 모듈도 모든 적에 적용되도록 없으면 자동 부착한다.
        foliageConcealment = GetComponent<FoliageConcealmentModule>();
        if (foliageConcealment == null) foliageConcealment = gameObject.AddComponent<FoliageConcealmentModule>();
        shieldModule = GetComponent<ShieldModule>();
        entity = GetComponent<Entity>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        navMeshAgent = GetComponent<NavMeshAgent>();

        ApplyTraitSpeedModifier();

        // Entity가 없어 자체 HealthModule을 예비로 쓰는 경우, 사망 시 오브젝트를 파괴합니다.
        // (Entity가 있는 경우의 사망 처리는 Enemy.DecreaseHP 쪽에서 담당)
        if (entity == null && healthModule != null)
        {
            healthModule.OnDeath += HandleHealthModuleDeath;
        }

        // Entity 기반 적은 Enemy.DecreaseHP가 직접 Destroy하므로, 사망 연출(타격감)만 이벤트로 걸어둔다.
        if (entity != null)
        {
            entity.OnDeath += HandleEntityDeath;
        }
    }

    private void HandleHealthModuleDeath()
    {
        HitFeedbackManager.Instance?.TriggerEnemyDeath(transform.position);
        Destroy(gameObject); // 임시: 추후 사망 연출/드랍 등으로 교체 가능
    }

    private void HandleEntityDeath()
    {
        HitFeedbackManager.Instance?.TriggerEnemyDeath(transform.position);
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
    /// <param name="bulletData">총알 데이터. AttributeModule이 부착된 효과(철갑탄/폭발탄 등)로 취약 배수를 계산하는 데 쓴다.
    /// 총알이 아닌 광역 아이템 등 실제 BulletSO가 없는 공격이면 null을 전달(효과 없음으로 취급).</param>
    /// <param name="isArmorPiercingBullet">철갑탄 여부(ArmorModule 배수 계산용).</param>
    /// <param name="headshotMultiplierBonus">조준경 등 무기 파츠가 더하는 헤드샷 배수 추가 비율(배수 ×(1+bonus)).</param>
    /// <param name="forceHeadshot">텅스텐 확정 헤드샷 등으로 확률 판정 없이 헤드샷을 강제할지 여부.</param>
    /// <param name="hitDirection">타격 방향(넉백/이펙트 방향용). 총알이 아닌 광역 아이템 등은 생략(제로벡터 → 넉백 없음).</param>
    /// <returns>이번 명중이 헤드샷이었는지 여부(무적으로 무시된 경우 false).</returns>
public bool OnBulletHit(float baseDamage, BulletSO bulletData, bool isArmorPiercingBullet = false,
        float headshotMultiplierBonus = 0f, bool forceHeadshot = false, Vector2 hitDirection = default)
    {
        if (defenseModule != null && defenseModule.TryBlockHit())
            return false; // 무적 판정으로 피해 무시

        float finalDamage = baseDamage;

        bool isHeadshot = false;
        if (headshotModule != null)
        {
            float mult;
            if (forceHeadshot)
            {
                isHeadshot = true;
                mult = headshotModule.HeadshotMultiplier;
            }
            else
            {
                mult = headshotModule.RollDamageMultiplier(out isHeadshot);
            }

            if (isHeadshot) mult *= (1f + headshotMultiplierBonus); // 조준경 헤드샷 배율 증가
            finalDamage *= mult;
        }

        if (attributeModule != null)
        {
            finalDamage *= attributeModule.GetDamageMultiplier(bulletData);
        }

        if (armorModule != null)
        {
            finalDamage *= armorModule.GetDamageMultiplier(isArmorPiercingBullet);
        }

        if (isHeadshot)
        {
            Debug.Log($"{name}: {(forceHeadshot ? "확정 " : "")}헤드샷! 최종 대미지 {finalDamage}");
        }

        RouteDamageToHealth(finalDamage);

        HitFeedbackManager.Instance?.TriggerEnemyHit(spriteRenderer, navMeshAgent, transform.position,
            hitDirection, finalDamage, isHeadshot);

        return isHeadshot;
    }

    /// <summary>
    /// 폭발탄(반경 피해)/전력탄 체인 전이처럼 "직격과 같은 취약 배수 규칙을 쓰지만 헤드샷 판정은 없는"
    /// 대미지의 진입점. Attribute/Armor 배수가 직격과 동일하게 적용된다(단 ignoreArmor면 Armor는 건너뜀 —
    /// 장판 틱은 철갑 배수를 타지 않는다는 밸런스 규칙 때문에 이 인자가 필요).
    /// </summary>
    public void ApplyAreaDamage(float baseDamage, BulletSO bulletData, bool isArmorPiercingBullet, bool ignoreArmor = false)
    {
        if (defenseModule != null && defenseModule.TryBlockHit())
            return; // 무적 판정으로 피해 무시

        float finalDamage = baseDamage;

        if (attributeModule != null)
        {
            finalDamage *= attributeModule.GetDamageMultiplier(bulletData);
        }

        if (!ignoreArmor && armorModule != null)
        {
            finalDamage *= armorModule.GetDamageMultiplier(isArmorPiercingBullet);
        }

        RouteDamageToHealth(finalDamage);

        HitFeedbackManager.Instance?.TriggerEnemyHit(spriteRenderer, navMeshAgent, transform.position,
            Vector2.zero, finalDamage, false);
    }

    /// <summary>
    /// 장판 틱처럼 호출자(DamageZone)가 이미 취약 배수/즉사 여부까지 전부 계산해 놓은 최종 대미지를
    /// 그대로 적용하는 진입점. 여기서는 추가로 Attribute/Armor 배수를 곱하지 않는다.
    /// </summary>
    public void ApplyPrecomputedDamage(float finalDamage)
    {
        if (defenseModule != null && defenseModule.TryBlockHit())
            return; // 무적 판정으로 피해 무시

        RouteDamageToHealth(finalDamage);
    }

    /// <summary>
    /// 계산이 끝난 최종 대미지를 실제 체력에 적용하는 공용 종착점.
    /// ShieldModule이 있고 아직 깨지지 않았으면 방패가 먼저 흡수한다(초과분은 버려지고 본체로 넘어가지 않음).
    /// </summary>
    private void RouteDamageToHealth(float finalDamage)
    {
        if (shieldModule != null && !shieldModule.IsBroken)
        {
            shieldModule.AbsorbDamage(finalDamage);
            return;
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

    private void OnDestroy()
    {
        if (entity != null) entity.OnDeath -= HandleEntityDeath;
        if (healthModule != null) healthModule.OnDeath -= HandleHealthModuleDeath;
    }
}
