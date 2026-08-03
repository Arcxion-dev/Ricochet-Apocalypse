using UnityEngine;

/// <summary>
/// 화상탄/냉기탄처럼 "최초로 직격한 적을 기준으로 주변에 장판(지대)을 생성"하는
/// 효과들의 공통 베이스. 최초 적중 여부를 내부 플래그로 추적합니다.
///
/// 주의: BulletEffectSO는 ScriptableObject(에셋)이므로 인스턴스 필드를 총알별
/// 상태로 쓰면 여러 총알이 같은 에셋을 공유할 때 상태가 섞입니다.
/// 그래서 "이미 적중했는지" 여부는 BulletController가 효과 타입(GetType())별로 들고 있는
/// 런타임 상태를 통해 확인합니다 (HasTriggeredZoneEffect/MarkZoneEffectTriggered) —
/// 화상+냉기처럼 서로 다른 장판 효과를 한 탄환이 함께 지녀도 둘 다 독립적으로 발동합니다.
/// </summary>
public abstract class ZoneEffectSOBase : BulletEffectSO
{
    [Header("장판 공통 설정")]
    [Tooltip("장판 판정 오브젝트로 쓸 프리팹(선택). 비워두면 판정용 오브젝트를 코드에서 만들고 " +
             "비주얼은 DamageZoneVisual이 반경에 맞춰 파티클로 생성한다. 보통은 비워두는 것이 맞다.")]
    public GameObject zonePrefab;

    [Tooltip("장판 반경(월드 유닛). 이 값 하나로 판정 범위와 파티클 비주얼 크기가 함께 결정된다.")]
    public float zoneRadius = 5f;

    [Tooltip("장판 지속 시간(초)")]
    public float zoneDuration = 3f;

    [Tooltip("장판 틱당 데미지")]
    public float zoneTickDamage = 2f;

    protected abstract string EffectLabel { get; }

    /// <summary>이 장판 효과의 속성 식별자(화상=Burn, 냉기=Frost). AttributeModule의 취약 배수/즉사 판정에 쓰인다.</summary>
    protected abstract BulletAttackAttribute AttackAttribute { get; }

public override void OnHitEnemy(BulletController bullet, Collider2D enemy)
    {
        var effectType = GetType();
        if (bullet.HasTriggeredZoneEffect(effectType)) return; // 이 효과 타입은 최초 1회만 발동
        bullet.MarkZoneEffectTriggered(effectType);

        Vector2 spawnPos = enemy.transform.position;
        GameObject zoneGO;

        if (zonePrefab != null)
        {
            zoneGO = Object.Instantiate(zonePrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            // 기본 경로: 판정용 콜라이더만 있는 빈 오브젝트를 만들고, 눈에 보이는 부분은
            // DamageZone.Setup이 붙이는 DamageZoneVisual(파티클)이 반경에 맞춰 그린다.
            zoneGO = new GameObject($"{EffectLabel}_Zone");
            zoneGO.transform.position = spawnPos;
            zoneGO.AddComponent<CircleCollider2D>();
        }

        var zone = zoneGO.GetComponent<DamageZone>();
        if (zone == null) zone = zoneGO.AddComponent<DamageZone>();
        zone.Setup(zoneRadius, zoneDuration, zoneTickDamage, bullet.EnemyLayerMask, EffectLabel, attackAttribute: AttackAttribute);

        Debug.Log($"[{EffectLabel}] {enemy.name} 최초 직격 - 위치 {spawnPos} 기준 반경 {zoneRadius} 장판 생성, {zoneDuration}초간 틱당 {zoneTickDamage} 데미지");
    }
}
