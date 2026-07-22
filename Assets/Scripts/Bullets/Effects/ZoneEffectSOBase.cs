using UnityEngine;

/// <summary>
/// 화상탄/냉기탄처럼 "최초로 직격한 적을 기준으로 주변에 장판(지대)을 생성"하는
/// 효과들의 공통 베이스. 최초 적중 여부를 내부 플래그로 추적합니다.
///
/// 주의: BulletEffectSO는 ScriptableObject(에셋)이므로 인스턴스 필드를 총알별
/// 상태로 쓰면 여러 총알이 같은 에셋을 공유할 때 상태가 섞입니다.
/// 그래서 "이미 적중했는지" 여부는 BulletController가 들고 있는 런타임 상태를
/// 통해 확인합니다 (HasTriggeredFirstHit).
/// </summary>
public abstract class ZoneEffectSOBase : BulletEffectSO
{
    [Header("장판 공통 설정")]
    [Tooltip("생성할 장판(지대) 프리팹")]
    public GameObject zonePrefab;

    [Tooltip("장판 반경")]
    public float zoneRadius = 2.5f;

    [Tooltip("장판 지속 시간(초)")]
    public float zoneDuration = 3f;

    [Tooltip("장판 틱당 데미지")]
    public float zoneTickDamage = 2f;

    protected abstract string EffectLabel { get; }

public override void OnHitEnemy(BulletController bullet, Collider2D enemy)
    {
        if (bullet.HasTriggeredFirstZoneHit) return; // 최초 1회만 발동
        bullet.HasTriggeredFirstZoneHit = true;

        Vector2 spawnPos = enemy.transform.position;
        GameObject zoneGO;

        if (zonePrefab != null)
        {
            zoneGO = Object.Instantiate(zonePrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            // 담당자가 아직 장판 프리팹을 만들지 않은 경우를 위한 임시 폴백 (콜라이더만 있는 빈 오브젝트)
            zoneGO = new GameObject($"{EffectLabel}_ZoneTemp");
            zoneGO.transform.position = spawnPos;
            zoneGO.AddComponent<CircleCollider2D>();
        }

        var zone = zoneGO.GetComponent<DamageZone>();
        if (zone == null) zone = zoneGO.AddComponent<DamageZone>();
        zone.Setup(zoneRadius, zoneDuration, zoneTickDamage, bullet.EnemyLayerMask, EffectLabel);

        Debug.Log($"[{EffectLabel}] {enemy.name} 최초 직격 - 위치 {spawnPos} 기준 반경 {zoneRadius} 장판 생성, {zoneDuration}초간 틱당 {zoneTickDamage} 데미지");
    }
}
