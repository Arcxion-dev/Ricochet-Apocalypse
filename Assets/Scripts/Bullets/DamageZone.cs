using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 화상탄/냉기탄/중력자탄 등이 생성하는 범위 장판의 런타임 동작.
/// 프리팹 루트에 CircleCollider2D(Trigger) + 이 컴포넌트를 붙여서 사용합니다.
///
/// 비주얼은 <see cref="DamageZoneVisual"/>이 Setup()에서 받은 반경 그대로 파티클 시스템을
/// 만들어 붙인다. 예전처럼 Circle 스프라이트 프리팹을 깔면 프리팹 스케일과 실제 판정 반경이
/// 따로 놀아 "보이는 크기 != 맞는 크기"가 되므로, 비주얼은 항상 반경 값에서 파생시킨다.
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
    private BulletAttackAttribute _attackAttribute;

    private float _elapsed;
    private float _tickTimer;
    private DamageZoneVisual _visual;

    public void Setup(float radius, float duration, float tickDamage, LayerMask enemyLayerMask, string label, float tickInterval = 0.5f,
        BulletAttackAttribute attackAttribute = BulletAttackAttribute.None)
    {
        _radius = radius;
        _duration = duration;
        _tickDamage = tickDamage;
        _enemyLayerMask = enemyLayerMask;
        _label = label;
        _tickInterval = tickInterval;
        _attackAttribute = attackAttribute;

        var col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = radius;

        // 스케일을 1로 고정해야 콜라이더 반경 = 월드 반경이 되고, 비주얼도 같은 값에 맞출 수 있다.
        transform.localScale = Vector3.one;

        if (_visual != null) Destroy(_visual.gameObject); // Setup 재호출 대비
        _visual = DamageZoneVisual.Create(transform, radius, attackAttribute);
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
            // 비주얼은 분리해서 남은 입자가 사라질 때까지 두고, 판정 본체만 즉시 정리한다.
            if (_visual != null) _visual.Release();
            Destroy(gameObject);
        }
    }

    /// <summary>장판 틱은 철갑 배수는 타지 않지만(밸런스 규칙), 속성 취약 배수/즉사 판정은 그대로 적용한다.</summary>
    private void DealTickDamage()
    {
        var hits = Physics2D.OverlapCircleAll(transform.position, _radius, _enemyLayerMask);
        foreach (var hit in hits)
        {
            float damage = _tickDamage;
            var attributeModule = hit.GetComponentInParent<AttributeModule>();
            if (attributeModule != null)
            {
                if (attributeModule.ShouldOneShotZoneTick(_attackAttribute))
                {
                    damage = 9999f; // 자기 속성과 일치하는 장판 - 남은 체력과 무관하게 즉사
                }
                else
                {
                    damage *= attributeModule.GetZoneTickMultiplier(_attackAttribute);
                }
            }

            BulletDamageDispatcher.ApplyDamage(hit, damage, _label, precomputed: true);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _radius);
    }
}
