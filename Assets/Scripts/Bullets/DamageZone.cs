using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 화상탄/냉기탄/중력자탄 등이 생성하는 범위 장판의 런타임 동작.
/// 프리팹 루트에 CircleCollider2D(Trigger) + 이 컴포넌트를 붙여서 사용합니다.
/// 담당자가 실제 비주얼 프리팹을 만들면 Setup()으로 파라미터만 주입하면 됩니다.
/// 프리팹이 아직 없을 경우를 대비해, 프리팹 없이도 순수 로직(OverlapCircle)만으로
/// 동작하는 SpawnLogicOnly 헬퍼를 BulletEffectSO 쪽에 별도로 제공합니다.
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
public class DamageZone : MonoBehaviour
{
    private float _radius;
    private float _duration;
    private float _tickDamage;
    private float _tickInterval = 0.5f;
    private LayerMask _enemyLayerMask;
    private string _label;

    private float _elapsed;
    private float _tickTimer;

    public void Setup(float radius, float duration, float tickDamage, LayerMask enemyLayerMask, string label, float tickInterval = 0.5f)
    {
        _radius = radius;
        _duration = duration;
        _tickDamage = tickDamage;
        _enemyLayerMask = enemyLayerMask;
        _label = label;
        _tickInterval = tickInterval;

        var col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = radius;

        transform.localScale = Vector3.one;
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        _tickTimer += Time.deltaTime;

        if (_tickTimer >= _tickInterval)
        {
            _tickTimer = 0f;
            DealTickDamage();
        }

        if (_elapsed >= _duration)
        {
            Destroy(gameObject);
        }
    }

    private void DealTickDamage()
    {
        var hits = Physics2D.OverlapCircleAll(transform.position, _radius, _enemyLayerMask);
        foreach (var hit in hits)
        {
            BulletDamageDispatcher.ApplyDamage(hit, _tickDamage, _label);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _radius);
    }
}
