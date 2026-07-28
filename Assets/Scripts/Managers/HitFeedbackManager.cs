using System.Collections;
using UnityEngine;
using UnityEngine.AI;
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

    [Header("카메라 펀치")]
    [SerializeField] private float _shakeMagnitude = 0.08f;
    [SerializeField] private float _headshotShakeMagnitude = 0.16f;
    [SerializeField] private float _deathShakeMagnitude = 0.12f;
    [SerializeField] private float _shakeDuration = 0.08f;

    [Header("히트스톱")]
    [SerializeField] private float _hitStopDuration = 0.06f;
    [SerializeField] private float _headshotHitStopDuration = 0.1f;

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
        BuildPopupCanvas();
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
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        // 표시 전용(클릭 없음)이라 GraphicRaycaster/EventSystem은 필요 없다.
    }

    /// <summary>씬 전환으로 바뀌었을 수 있는 카메라/팬 컨트롤러 참조를 다시 찾는다.</summary>
    private void RefreshCameraRefs()
    {
        if (_cam == null) _cam = Camera.main;
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
            StartCoroutine(KnockbackRoutine(agent, hitDirection, force));
        }

        SpawnDamagePopup(hitPoint, Mathf.RoundToInt(damage), isHeadshot);

        float shakeMag = isHeadshot ? _headshotShakeMagnitude : _shakeMagnitude;
        StartShake(shakeMag);

        float hitStop = isHeadshot ? _headshotHitStopDuration : _hitStopDuration;
        TryHitStop(hitStop);

        if (isHeadshot) SoundManager.Instance?.PlaySfx("Headshot", 1.3f);
    }

    /// <summary>적 사망 순간의 피드백(사운드 + 약간 더 큰 카메라 펀치)을 트리거한다.</summary>
    public void TriggerEnemyDeath(Vector3 position)
    {
        RefreshCameraRefs();
        StartShake(_deathShakeMagnitude);
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

    private IEnumerator KnockbackRoutine(NavMeshAgent agent, Vector2 direction, float force)
    {
        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.zero;
        if (dir == Vector2.zero) yield break;

        float t = 0f;
        while (t < _knockbackDuration)
        {
            if (agent == null || !agent.isActiveAndEnabled) yield break;

            t += Time.deltaTime; // 히트스톱(타임스케일 정지) 동안 함께 멎었다가 풀리는 느낌을 위해 스케일 적용 시간 사용.
            float damper = 1f - Mathf.Clamp01(t / _knockbackDuration); // 점점 잦아듦
            agent.Move(dir * (force * damper * Time.deltaTime));
            yield return null;
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
/// <see cref="HitFeedbackManager"/>가 생성하는 데미지 팝업 하나의 수명(위로 떠오르며 페이드 후 자동 파괴).
/// 실시간(unscaled) 기준으로 움직여 히트스톱 중에도 자연스럽게 보인다.
/// </summary>
public class DamagePopup : MonoBehaviour
{
    private Camera _cam;
    private RectTransform _rect;
    private Text _text;

    public void Begin(Camera cam, RectTransform rect, Text text, Vector3 worldPosition, float lifetime, float riseDistance)
    {
        _cam = cam;
        _rect = rect;
        _text = text;
        StartCoroutine(RiseAndFade(worldPosition, lifetime, riseDistance));
    }

    private IEnumerator RiseAndFade(Vector3 worldPosition, float lifetime, float riseDistance)
    {
        Vector3 start = worldPosition;
        Vector3 end = start + Vector3.up * riseDistance;
        Color startColor = _text.color;

        float t = 0f;
        while (t < lifetime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / lifetime);
            Vector3 worldPos = Vector3.Lerp(start, end, k);
            if (_cam != null) _rect.position = _cam.WorldToScreenPoint(worldPos);

            Color c = startColor;
            c.a = Mathf.Lerp(startColor.a, 0f, k);
            _text.color = c;
            yield return null;
        }
        Destroy(gameObject);
    }
}
