using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// 적 피격/사망 시 타격감을 담당하는 싱글턴 오케스트레이터.
/// 히트 플래시 + 넉백 + 데미지 팝업 + 카메라 펀치 + 히트스톱(초단시간 타임스케일 정지)을
/// 한 번의 호출(<see cref="TriggerEnemyHit"/>)로 묶어서 트리거한다.
///
/// - 히트스톱은 <see cref="ChargeShotEffects"/>가 이미 타임스케일을 조작 중일 때(차징 슬로우모션)는
///   발동하지 않는다(둘이 동시에 Time.timeScale을 다투지 않도록 하는 최소 규칙).
/// - 넉백은 적이 NavMeshAgent 기반 + Kinematic Rigidbody2D라(2026-07-27 확정 설계) AddForce가
///   먹히지 않아 agent.Move()로 매 프레임 살짝 밀어내는 방식으로 구현한다.
/// </summary>
public class HitFeedbackManager : MonoBehaviour
{
    public static HitFeedbackManager Instance { get; private set; }

    [Header("히트 플래시")]
    [SerializeField] private Color _flashColor = Color.white;
    [SerializeField] private Color _headshotFlashColor = new Color(1f, 0.95f, 0.2f);
    [SerializeField] private float _flashDuration = 0.07f;

    [Header("넉백")]
    [SerializeField] private float _knockbackForce = 1.4f;
    [SerializeField] private float _headshotKnockbackMultiplier = 1.4f;
    [SerializeField] private float _knockbackDuration = 0.1f;
    [Tooltip("넉백이 통과하지 못하는 레이어. 비워두면 Wall 레이어를 자동으로 사용한다.")]
    [SerializeField] private LayerMask _knockbackBlockingLayers;
    [Tooltip("넉백으로 벽에 완전히 달라붙지 않도록 남겨두는 여유 간격(월드 유닛).")]
    [SerializeField] private float _knockbackSkinWidth = 0.03f;

    [Header("카메라 펀치")]
    [SerializeField] private float _shakeMagnitude = 0.08f;
    [SerializeField] private float _headshotShakeMagnitude = 0.16f;
    [SerializeField] private float _deathShakeMagnitude = 0.12f;
    [SerializeField] private float _shakeDuration = 0.08f;

    [Header("히트스톱")]
    [SerializeField] private float _hitStopDuration = 0.06f;
    [SerializeField] private float _headshotHitStopDuration = 0.1f;

    [Header("포스트프로세싱 펀치 (색수차)")]
    [Tooltip("ChargeShotEffects가 이미 Bloom/Vignette를 쓰고 있어, 겹치지 않도록 색수차(Chromatic Aberration) 전용으로 분리.")]
    [SerializeField] private float _aberrationPeak = 0.4f;
    [SerializeField] private float _headshotAberrationPeak = 0.8f;
    [SerializeField] private float _deathAberrationPeak = 0.6f;
    [SerializeField] private float _aberrationDuration = 0.12f;

    [Header("데미지 팝업")]
    [SerializeField] private float _popupLifetime = 0.6f;
    [Tooltip("팝업이 떠오르는 거리(월드 유닛). 총알/플레이어 크기(0.4~0.5)에 맞춘 기본값.")]
    [SerializeField] private float _popupRiseDistance = 0.6f;
    [SerializeField] private Color _popupColor = Color.white;
    [SerializeField] private Color _headshotPopupColor = new Color(1f, 0.85f, 0.2f);

    private Font _font;
    private Canvas _popupCanvas;
    private Camera _cam;
    private CameraPanController _cameraPan;
    private Coroutine _shakeRoutine;

    private Volume _volume;
    private ChromaticAberration _aberration;
    private Coroutine _aberrationRoutine;

    /// <summary>넉백 스윕이 벽으로 판정할 대상 필터. Awake에서 한 번만 만든다.</summary>
    private ContactFilter2D _knockbackFilter;

    /// <summary>어느 씬에서 Play해도 존재하도록 부트스트랩(다른 매니저들과 동일 패턴).</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("HitFeedbackManager");
        go.AddComponent<HitFeedbackManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _knockbackFilter = CollideAndSlide2D.CreateFilter(
            CollideAndSlide2D.ResolveDefaultWallMask(_knockbackBlockingLayers));
        BuildPopupCanvas();
        BuildAberrationVolume();
    }

    /// <summary>
    /// 타격 펀치 전용 런타임 Volume. ChargeShotEffects는 Bloom/Vignette만 건드리므로
    /// 색수차(Chromatic Aberration)만 쓰면 두 시스템이 같은 프로퍼티를 다투지 않는다.
    /// </summary>
    private void BuildAberrationVolume()
    {
        var go = new GameObject("HitFeedback Volume");
        go.transform.SetParent(transform, false);

        _volume = go.AddComponent<Volume>();
        _volume.isGlobal = true;
        _volume.weight = 1f;
        _volume.priority = 100f;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _volume.profile = profile;

        _aberration = profile.Add<ChromaticAberration>(true);
        _aberration.intensity.overrideState = true;
        _aberration.intensity.value = 0f;
    }

    private void BuildPopupCanvas()
    {
        var canvasGO = new GameObject("HitFeedbackCanvas");
        canvasGO.transform.SetParent(transform, false);

        _popupCanvas = canvasGO.AddComponent<Canvas>();
        _popupCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _popupCanvas.sortingOrder = 500;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f; // 폭 기준(세로 고정)
        // 표시 전용(클릭 없음)이라 GraphicRaycaster/EventSystem은 필요 없다.
    }

    /// <summary>씬 전환으로 바뀌었을 수 있는 카메라/팬 컨트롤러 참조를 다시 찾는다.</summary>
    private void RefreshCameraRefs()
    {
        if (_cam == null)
        {
            _cam = Camera.main;
            // ChargeShotEffects도 켜지만, 그게 없는 씬(Shop/Title 등)에서도 색수차가 보이도록 독립적으로 보장.
            var camData = _cam != null ? _cam.GetUniversalAdditionalCameraData() : null;
            if (camData != null) camData.renderPostProcessing = true;
        }
        if (_cameraPan == null) _cameraPan = FindObjectOfType<CameraPanController>();
    }

    // ───────────────────────── 공개 API ─────────────────────────

    /// <summary>
    /// 적 피격 순간의 종합 피드백을 트리거한다. 인자가 null이어도 안전하게 가능한 부분만 실행한다.
    /// </summary>
    public void TriggerEnemyHit(SpriteRenderer sprite, NavMeshAgent agent, Vector3 hitPoint,
        Vector2 hitDirection, float damage, bool isHeadshot)
    {
        RefreshCameraRefs();

        Color flashColor = isHeadshot ? _headshotFlashColor : _flashColor;
        if (sprite != null) StartCoroutine(FlashRoutine(sprite, flashColor));

        if (agent != null)
        {
            float force = _knockbackForce * (isHeadshot ? _headshotKnockbackMultiplier : 1f);
            // force<=0이면 agent.Move()를 아예 호출하지 않는다. Move()는 0벡터로 호출해도
            // NavMeshAgent를 현재 베이크된 NavMesh 표면으로 "보정 스냅"시키는 부작용이 있어
            // (실제로 재현 확인: force와 무관하게 항상 동일한 오프셋만큼 튐), 넉백을 껐는데도
            // 움직이는 것처럼 보였다.
            if (force > 0.0001f) StartCoroutine(KnockbackRoutine(agent, hitDirection, force));
        }

        SpawnDamagePopup(hitPoint, Mathf.RoundToInt(damage), isHeadshot);

        float shakeMag = isHeadshot ? _headshotShakeMagnitude : _shakeMagnitude;
        StartShake(shakeMag);

        float hitStop = isHeadshot ? _headshotHitStopDuration : _hitStopDuration;
        TryHitStop(hitStop);

        PulseAberration(isHeadshot ? _headshotAberrationPeak : _aberrationPeak);

        if (isHeadshot) SoundManager.Instance?.PlaySfx("Headshot", 1.3f);
    }

    /// <summary>적 사망 순간의 피드백(사운드 + 약간 더 큰 카메라 펀치/색수차)을 트리거한다.</summary>
    public void TriggerEnemyDeath(Vector3 position)
    {
        RefreshCameraRefs();
        StartShake(_deathShakeMagnitude);
        PulseAberration(_deathAberrationPeak);
        SoundManager.Instance?.PlaySfx("Death");
    }

    // ───────────────────────── 히트 플래시 ─────────────────────────

    private IEnumerator FlashRoutine(SpriteRenderer sprite, Color flashColor)
    {
        Color original = sprite.color;
        sprite.color = flashColor;

        float t = 0f;
        while (t < _flashDuration)
        {
            t += Time.unscaledDeltaTime;
            if (sprite == null) yield break;
            sprite.color = Color.Lerp(flashColor, original, Mathf.Clamp01(t / _flashDuration));
            yield return null;
        }
        if (sprite != null) sprite.color = original;
    }

    // ───────────────────────── 넉백 ─────────────────────────

    /// <summary>
    /// 넉백 동안 NavMeshAgent.Move()를 쓰지 않는다 — 반복 호출 시 힘 크기와 무관하게
    /// 항상 같은 큰 거리로 "보정 스냅"되는 현상이 실측 확인됨(force=0과 force=10이 완전히
    /// 동일한 오프셋으로 튐 → NavMesh 위 재투영 부작용이 실제 힘보다 압도적으로 큼).
    /// 대신 넉백 동안만 에이전트를 잠시 꺼서 NavMesh 제약 없이 Transform을 직접, 힘에 비례해
    /// 움직이고, 끝나면 <see cref="NavMeshAgent.Warp"/>로 한 번만 깔끔하게 재동기화한다.
    ///
    /// 단, 에이전트를 끄면 NavMesh의 이동 제약도 함께 사라지기 때문에 Transform을 그냥 더하면
    /// 적이 벽을 뚫고 밀려난다. 그래서 매 스텝을 <see cref="CollideAndSlide2D"/>로 미리 쓸어보고
    /// 벽 앞에서 멈추거나 벽을 따라 미끄러지게 한다.
    /// </summary>
    private IEnumerator KnockbackRoutine(NavMeshAgent agent, Vector2 direction, float force)
    {
        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.zero;
        if (dir == Vector2.zero) yield break;

        Transform t = agent.transform;
        Collider2D body = agent.GetComponent<Collider2D>(); // 없으면 스윕 없이 예전처럼 이동
        bool wasEnabled = agent.enabled;
        if (wasEnabled) agent.enabled = false;

        float elapsed = 0f;
        while (elapsed < _knockbackDuration)
        {
            if (t == null) yield break;

            elapsed += Time.deltaTime; // 히트스톱(타임스케일 정지) 동안 함께 멎었다가 풀리는 느낌을 위해 스케일 적용 시간 사용.
            float damper = 1f - Mathf.Clamp01(elapsed / _knockbackDuration); // 점점 잦아듦

            Vector2 current = t.position;
            Vector2 step = dir * (force * damper * Time.deltaTime);
            Vector2 next = CollideAndSlide2D.Move(body, current, step, _knockbackFilter, _knockbackSkinWidth);
            t.position = new Vector3(next.x, next.y, t.position.z);

            yield return null;
        }

        if (wasEnabled && agent != null && t != null)
        {
            agent.enabled = true;
            agent.Warp(t.position); // 넉백이 끝난 위치를 기준으로 NavMesh에 한 번만 재동기화.
        }
    }

    // ───────────────────────── 카메라 펀치 ─────────────────────────

    private void StartShake(float magnitude)
    {
        if (_cameraPan == null || magnitude <= 0f) return;
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(ShakeRoutine(magnitude));
    }

    private IEnumerator ShakeRoutine(float magnitude)
    {
        float t = 0f;
        while (t < _shakeDuration)
        {
            t += Time.unscaledDeltaTime;
            float damper = 1f - Mathf.Clamp01(t / _shakeDuration);
            Vector2 rand = Random.insideUnitCircle * (magnitude * damper);
            if (_cameraPan != null) _cameraPan.ExternalShakeOffset = new Vector3(rand.x, rand.y, 0f);
            yield return null;
        }
        if (_cameraPan != null) _cameraPan.ExternalShakeOffset = Vector3.zero;
        _shakeRoutine = null;
    }

    // ───────────────────────── 포스트프로세싱 펀치 ─────────────────────────

    private void PulseAberration(float peak)
    {
        if (_aberration == null || peak <= 0f) return;
        if (_aberrationRoutine != null) StopCoroutine(_aberrationRoutine);
        _aberrationRoutine = StartCoroutine(AberrationRoutine(peak));
    }

    private IEnumerator AberrationRoutine(float peak)
    {
        _aberration.intensity.value = peak;
        float t = 0f;
        while (t < _aberrationDuration)
        {
            t += Time.unscaledDeltaTime;
            _aberration.intensity.value = Mathf.Lerp(peak, 0f, Mathf.Clamp01(t / _aberrationDuration));
            yield return null;
        }
        _aberration.intensity.value = 0f;
        _aberrationRoutine = null;
    }

    // ───────────────────────── 히트스톱 ─────────────────────────

    /// <summary>
    /// 이미 타임스케일이 1이 아니면(차징 슬로우모션 등 다른 시스템이 사용 중) 발동하지 않는다.
    /// </summary>
    private void TryHitStop(float duration)
    {
        if (duration <= 0f) return;
        if (!Mathf.Approximately(Time.timeScale, 1f)) return;
        StartCoroutine(HitStopRoutine(duration));
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }

    // ───────────────────────── 데미지 팝업 ─────────────────────────

    private void SpawnDamagePopup(Vector3 worldPosition, int amount, bool isHeadshot)
    {
        if (_popupCanvas == null || _cam == null) return;

        var go = new GameObject("DamagePopup", typeof(RectTransform));
        go.transform.SetParent(_popupCanvas.transform, false);

        var text = go.AddComponent<Text>();
        text.font = _font;
        text.text = isHeadshot ? $"{amount}!" : amount.ToString();
        text.fontSize = isHeadshot ? 32 : 24;
        text.fontStyle = isHeadshot ? FontStyle.Bold : FontStyle.Normal;
        text.color = isHeadshot ? _headshotPopupColor : _popupColor;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(160f, 40f);

        var popup = go.AddComponent<DamagePopup>();
        popup.Begin(_cam, rect, text, worldPosition, _popupLifetime, _popupRiseDistance);
    }
}

/// <summary>
/// <see cref="HitFeedbackManager"/>가 생성하는 데미지 팝업 하나의 수명.
/// 숫자가 톡 튀어나온 뒤 감속하며 떠오르고, 마지막 구간에서만 사라진다
/// (처음부터 균일하게 흐려지면 정작 읽어야 할 순간에 희미하다).
/// 실시간(unscaled) 기준으로 움직여 히트스톱 중에도 자연스럽게 보인다.
/// </summary>
public class DamagePopup : MonoBehaviour
{
    private Camera _cam;
    private RectTransform _rect;

    public void Begin(Camera cam, RectTransform rect, Text text, Vector3 worldPosition, float lifetime, float riseDistance)
    {
        _cam = cam;
        _rect = rect;

        // 스크린 좌표 추적은 월드 기준 높이를 트윈해서 얻는다(카메라가 움직여도 따라붙게).
        float height = 0f;
        Vector3 start = worldPosition;

        DOTween.To(() => height, h =>
        {
            height = h;
            if (_cam != null && _rect != null)
                _rect.position = _cam.WorldToScreenPoint(start + Vector3.up * height);
        }, riseDistance, lifetime).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject);

        // 등장: 작게 → 크게 → 제자리.
        rect.localScale = Vector3.one * 0.4f;
        rect.DOScale(1f, 0.18f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(gameObject);

        // 소멸: 수명의 뒤쪽 45%에서만 흐려진다.
        float fadeDur = lifetime * 0.45f;
        text.DOFade(0f, fadeDur)
            .SetDelay(lifetime - fadeDur)
            .SetEase(Ease.InQuad)
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(() => Destroy(gameObject));
    }
}
