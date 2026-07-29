using UnityEngine;

/// <summary>
/// 은신 모듈: 아지랑이(HeatHaze)/모래바람(Sandstorm)/풀숲(Bush) 장애물 위에 있는 동안
/// 적을 플레이어에게 "보이지 않게"(스프라이트 렌더러 비활성) 만든다.
///
/// 설계 메모:
/// - "은신"은 렌더링만 끈다. 콜라이더는 그대로 살아 있으므로, 이 은신 장애물들이
///   총알을 <b>관통</b>시키는 특성(<see cref="BulletController.DetermineHitResult"/>)과 맞물려
///   "장애물에 총을 쏘면 관통한 총알이 은신한 적을 맞혀 피격된다"가 자연히 성립한다.
///   (요구사항: 은신 중이라도 해당 장애물을 쏴 맞히면 몬스터가 피격되어야 한다.)
/// - 은신은 <see cref="SpriteRenderer.enabled"/>를 끄는 방식이라, 피격 플래시가 색(color)만
///   바꾸는 <see cref="HitFeedbackManager"/>와 충돌하지 않는다(꺼진 렌더러는 색과 무관하게 안 보임).
/// - 적 중심점이 은신 장애물 콜라이더 안에 들어오면 은신, 벗어나면 해제한다.
///
/// EnemyController가 Awake에서 자동 부착하므로(HeadshotModule과 동일 패턴) 모든 적에 적용된다.
/// </summary>
public class StealthModule : MonoBehaviour
{
    [Header("은신 판정")]
    [Tooltip("은신 장애물이 올라가 있는 레이어. 비워두면(0) Awake에서 \"Wall\" 레이어로 자동 설정.")]
    [SerializeField] private LayerMask concealmentMask;

    [Tooltip("은신 여부를 다시 판정하는 주기(초).")]
    [SerializeField] private float checkInterval = 0.15f;

    [Tooltip("적 중심에서 은신 장애물을 탐지하는 반경. 작을수록 '완전히 들어가야' 은신.")]
    [SerializeField] private float checkRadius = 0.1f;

    private SpriteRenderer[] _renderers;
    private float _timer;
    private bool _concealed;

    // 매 판정마다 GC 할당이 없도록 재사용하는 오버랩 버퍼.
    private static readonly Collider2D[] _overlapBuffer = new Collider2D[8];

    /// <summary>현재 은신(플레이어에게 보이지 않는) 상태인지 여부.</summary>
    public bool IsConcealed => _concealed;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);

        if (concealmentMask.value == 0)
        {
            int wallLayer = LayerMask.NameToLayer("Wall");
            if (wallLayer >= 0) concealmentMask = 1 << wallLayer;
        }
    }

    private void OnDisable()
    {
        // 비활성/사망 순간에 은신 상태가 남아 다른 씬/재사용 시 안 보이는 일이 없도록 복구.
        if (_concealed) SetConcealed(false);
    }

    private void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = checkInterval;

        EvaluateConcealment();
    }

    private void EvaluateConcealment()
    {
        Vector2 center = transform.position;
        int count = Physics2D.OverlapCircleNonAlloc(center, checkRadius, _overlapBuffer, concealmentMask);

        bool nowConcealed = false;
        for (int i = 0; i < count; i++)
        {
            if (_overlapBuffer[i] == null) continue;
            if (IsConcealingType(BulletController.ResolveTargetType(_overlapBuffer[i])))
            {
                nowConcealed = true;
                break;
            }
        }

        if (nowConcealed != _concealed) SetConcealed(nowConcealed);
    }

    /// <summary>은신을 제공하는 장애물 타입(풀숲/모래바람/아지랑이)인지.</summary>
    private static bool IsConcealingType(BulletTargetType type)
        => type == BulletTargetType.Bush
        || type == BulletTargetType.Sandstorm
        || type == BulletTargetType.HeatHaze;

    private void SetConcealed(bool value)
    {
        _concealed = value;
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null) _renderers[i].enabled = !value;
        }
    }
}
