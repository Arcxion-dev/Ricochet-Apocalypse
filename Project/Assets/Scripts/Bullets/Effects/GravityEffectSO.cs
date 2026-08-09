using UnityEngine;

/// <summary>
/// 6. 중력자탄 - 주변의 적을 끌어모은 뒤 폭발합니다.
/// </summary>
[CreateAssetMenu(fileName = "GravityEffect", menuName = "Bullet/Effects/중력자탄 (Gravity)")]
public class GravityEffectSO : BulletEffectSO
{
    [Tooltip("적을 끌어당기는 범위")]
    public float pullRadius = 4f;

    [Tooltip("끌어당기는 힘(적을 총알 쪽으로 당기는 속도)")]
    public float pullForce = 5f;

    [Tooltip("끌어모으는 지속 시간(초) - 이후 폭발")]
    public float pullDuration = 1f;

    [Tooltip("최종 폭발 데미지")]
    public float finalExplosionDamage = 25f;

    // 적을 직격한 순간 중력장을 열고 총알을 소멸시킨다(관통 후 뒤늦게 발동하던 문제 제거).
    public override void OnHitEnemy(BulletController bullet, Collider2D enemy)
    {
        SpawnWell(bullet);
        bullet.Kill(); // 그 자리에서 소멸. Kill→Die→OnBulletDestroyed는 가드로 중복 생성 방지.
    }

    // 적을 맞히지 못하고 벽/수명으로 소멸할 때도 그 자리에서 중력장을 연다.
    public override void OnBulletDestroyed(BulletController bullet)
    {
        SpawnWell(bullet);
    }

    /// <summary>현재 총알 위치에 중력장 러너를 1회 스폰한다. 한 총알에 대해 중복 발동하지 않는다.</summary>
    private void SpawnWell(BulletController bullet)
    {
        var effectType = GetType();
        if (bullet.HasTriggeredZoneEffect(effectType)) return; // 이미 발동함(중복 방지).
        bullet.MarkZoneEffectTriggered(effectType);

        Vector2 pos = bullet.transform.position;
        var runnerGO = new GameObject("GravityWellRunner_Temp");
        runnerGO.transform.position = pos;
        var runner = runnerGO.AddComponent<GravityWellRunner>();
        runner.Run(pos, pullRadius, pullForce, pullDuration, finalExplosionDamage, bullet.EnemyLayerMask);

        Debug.Log($"[중력자탄] 위치 {pos}에서 반경 {pullRadius} 내 적 {pullDuration}초간 끌어모으기 시작");
    }
}
