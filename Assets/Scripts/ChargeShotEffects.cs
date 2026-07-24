using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 저격 "차징 사격"의 시청각 연출을 캡슐화하는 컴포넌트. <see cref="PlayerShooter"/>가
/// 조준을 고정(호흡)할 때 <see cref="BeginCharge"/>, 발사할 때 <see cref="Fire"/>,
/// 취소할 때 <see cref="Cancel"/>를 호출한다.
///
/// 연출 구성:
/// 1) 차징(BeginCharge): 슬로우모션 진입 + 카메라 확대(직교 크기 축소) +
///    URP 포스트프로세싱(Bloom·Vignette) 서서히 상승. (조준선 고정·랜덤 흔들림은
///    PlayerShooter의 호흡 sway가 담당한다.)
/// 2) 발사(Fire): 화면 흔들림 + 슬로우모션 해제 시 순간 <see cref="_overshootScale"/>배로
///    급가속했다가 1배로 복귀. 포스트프로세싱/확대는 원상 복귀.
/// 3) 취소(Cancel): 흔들림·오버슈트 없이 슬로우모션/확대/PP를 부드럽게 원상 복귀.
///
/// URP Volume은 런타임에 스스로 생성하므로 씬마다 Global Volume을 배치할 필요가 없다.
/// 카메라의 포스트프로세싱도 자동으로 켠다.
/// </summary>
public class ChargeShotEffects : MonoBehaviour
{
    [Header("대상 카메라 / 팬 컨트롤러")]
    [Tooltip("연출 대상 카메라. 비우면 Camera.main을 사용한다.")]
    [SerializeField] private Camera _cam;
    [Tooltip("화면 흔들림을 주입할 팬 컨트롤러. 비우면 씬에서 자동으로 찾는다.")]
    [SerializeField] private CameraPanController _pan;

    [Header("슬로우모션")]
    [Tooltip("차징 중 목표 타임스케일(0~1). 작을수록 더 느려진다.")]
    [Range(0.05f, 1f)][SerializeField] private float _slowScale = 0.35f;
    [Tooltip("차징 진입(슬로우모션·확대·PP 상승)에 걸리는 시간(초, 실시간).")]
    [SerializeField] private float _rampInTime = 0.18f;
    [Tooltip("취소/복귀(원상 복귀)에 걸리는 시간(초, 실시간).")]
    [SerializeField] private float _rampOutTime = 0.15f;

    [Header("발사 시 슬로우모션 오버슈트")]
    [Tooltip("발사 순간 잠깐 올라가는 타임스케일(급가속 느낌). 1보다 크게.")]
    [SerializeField] private float _overshootScale = 1.2f;
    [Tooltip("오버슈트까지 올라가는 시간(초).")]
    [SerializeField] private float _overshootRampTime = 0.05f;
    [Tooltip("오버슈트를 유지하는 시간(초).")]
    [SerializeField] private float _overshootHoldTime = 0.08f;
    [Tooltip("오버슈트에서 1배로 내려오는 시간(초).")]
    [SerializeField] private float _overshootReturnTime = 0.12f;

    [Header("카메라 확대 (직교 크기)")]
    [Tooltip("차징 중 줄어드는 직교 크기(확대량). 클수록 더 확대된다.")]
    [SerializeField] private float _zoomDelta = 1.2f;

    [Header("포스트프로세싱 목표 세기")]
    [Tooltip("차징 절정에서의 Bloom 세기.")]
    [SerializeField] private float _bloomTarget = 3.5f;
    [Tooltip("차징 절정에서의 Vignette 세기(0~1).")]
    [Range(0f, 1f)][SerializeField] private float _vignetteTarget = 0.35f;

    [Header("발사 화면 흔들림")]
    [Tooltip("흔들림 최대 크기(월드 유닛).")]
    [SerializeField] private float _shakeMagnitude = 0.25f;
    [Tooltip("흔들림 지속 시간(초).")]
    [SerializeField] private float _shakeDuration = 0.2f;

    private Volume _volume;
    private Bloom _bloom;
    private Vignette _vignette;

    /// <summary>무기 파츠(네뷸라이저)가 거는 슬로우 강화 배율. 1=기본, 작을수록 더 느리게.</summary>
    private float _slowScaleMultiplier = 1f;

    private float _baseFixedDelta;
    private float _baseOrthoSize;
    private bool _baseOrthoCaptured;

    private Coroutine _stateRoutine;
    private Coroutine _shakeRoutine;

    private void Awake()
    {
        if (_cam == null) _cam = Camera.main;
        if (_pan == null) _pan = FindObjectOfType<CameraPanController>();
        _baseFixedDelta = Time.fixedDeltaTime;

        EnsurePostProcessing();
        BuildVolume();
    }

    /// <summary>URP 카메라의 포스트프로세싱을 켠다(꺼져 있으면 Bloom/Vignette가 화면에 안 나온다).</summary>
    private void EnsurePostProcessing()
    {
        if (_cam == null) return;
        var camData = _cam.GetUniversalAdditionalCameraData();
        if (camData != null) camData.renderPostProcessing = true;
    }

    /// <summary>런타임 Global Volume + Bloom/Vignette 오버라이드를 생성한다(세기 0에서 시작).</summary>
    private void BuildVolume()
    {
        var go = new GameObject("ChargeShot Volume");
        go.transform.SetParent(transform, false);

        _volume = go.AddComponent<Volume>();
        _volume.isGlobal = true;
        _volume.weight = 1f;
        _volume.priority = 100f; // 씬의 다른 볼륨보다 우선

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _volume.profile = profile;

        _bloom = profile.Add<Bloom>(true);
        _bloom.intensity.overrideState = true;
        _bloom.intensity.value = 0f;

        _vignette = profile.Add<Vignette>(true);
        _vignette.intensity.overrideState = true;
        _vignette.intensity.value = 0f;
    }

    // ───────────────────────── 공개 API ─────────────────────────

    /// <summary>
    /// 무기 파츠(네뷸라이저)가 차징 슬로우를 강화/완화하는 배율을 설정한다.
    /// 목표 타임스케일에 곱해지며, 값이 작을수록 더 느려진다. 다음 <see cref="BeginCharge"/>부터 반영된다.
    /// </summary>
    public void SetSlowScaleMultiplier(float multiplier)
    {
        _slowScaleMultiplier = Mathf.Clamp(multiplier, 0.05f, 4f);
    }

    /// <summary>파츠 배율까지 반영한 실제 차징 목표 타임스케일(0~1로 클램프).</summary>
    private float EffectiveSlowScale => Mathf.Clamp(_slowScale * _slowScaleMultiplier, 0.02f, 1f);

    /// <summary>조준 고정(호흡) 진입: 슬로우모션·확대·PP 상승.</summary>
    public void BeginCharge()
    {
        CaptureBaseOrtho();
        StartState(RampCharge());
    }

    /// <summary>발사: 화면 흔들림 + 오버슈트 복귀 + PP/확대 원복.</summary>
    public void Fire()
    {
        StartShake();
        StartState(FireRelease());
    }

    /// <summary>취소(우클릭): 흔들림/오버슈트 없이 부드럽게 원상 복귀.</summary>
    public void Cancel()
    {
        StartState(RampRestore());
    }

    /// <summary>
    /// 연출을 즉시(애니메이션 없이) 중단하고 타임스케일/화면/PP를 기본값으로 되돌린다.
    /// UI 정지(타임스케일 0) 진입 등, 진행 중인 슬로우 코루틴이 타임스케일을 되돌려 놓으면
    /// 안 되는 상황에서 호출한다.
    /// </summary>
    public void ForceStop()
    {
        if (_stateRoutine != null) { StopCoroutine(_stateRoutine); _stateRoutine = null; }
        if (_shakeRoutine != null) { StopCoroutine(_shakeRoutine); _shakeRoutine = null; }
        SetShakeOffset(Vector3.zero);
        FinalizeToNormal();
    }

    // ───────────────────────── 상태 전이 코루틴 ─────────────────────────

    private void StartState(IEnumerator routine)
    {
        if (_stateRoutine != null) StopCoroutine(_stateRoutine);
        _stateRoutine = StartCoroutine(routine);
    }

    private IEnumerator RampCharge()
    {
        float targetOrtho = _baseOrthoSize - _zoomDelta;
        yield return LerpTo(_rampInTime, EffectiveSlowScale, targetOrtho, _bloomTarget, _vignetteTarget);
        _stateRoutine = null;
    }

    private IEnumerator RampRestore()
    {
        yield return LerpTo(_rampOutTime, 1f, _baseOrthoSize, 0f, 0f);
        FinalizeToNormal();
        _stateRoutine = null;
    }

    private IEnumerator FireRelease()
    {
        // PP/확대는 원상 복귀시키면서, 타임스케일은 오버슈트로 급가속했다가 1배로 내려온다.
        // 1) 오버슈트까지 급가속 + PP/확대 원복 시작.
        yield return LerpTo(_overshootRampTime, _overshootScale, _baseOrthoSize, 0f, 0f);
        // 2) 오버슈트 유지.
        if (_overshootHoldTime > 0f) yield return new WaitForSecondsRealtime(_overshootHoldTime);
        // 3) 1배로 복귀.
        yield return LerpTimeScaleOnly(_overshootReturnTime, 1f);
        FinalizeToNormal();
        _stateRoutine = null;
    }

    /// <summary>지정 시간(실시간) 동안 타임스케일/직교크기/PP를 목표값으로 보간한다.</summary>
    private IEnumerator LerpTo(float duration, float toTimeScale, float toOrtho, float toBloom, float toVignette)
    {
        float fromTimeScale = Time.timeScale;
        float fromOrtho = _cam != null ? _cam.orthographicSize : 0f;
        float fromBloom = _bloom != null ? _bloom.intensity.value : 0f;
        float fromVignette = _vignette != null ? _vignette.intensity.value : 0f;

        if (duration <= 0f)
        {
            ApplyState(toTimeScale, toOrtho, toBloom, toVignette);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            ApplyState(
                Mathf.Lerp(fromTimeScale, toTimeScale, k),
                Mathf.Lerp(fromOrtho, toOrtho, k),
                Mathf.Lerp(fromBloom, toBloom, k),
                Mathf.Lerp(fromVignette, toVignette, k));
            yield return null;
        }
        ApplyState(toTimeScale, toOrtho, toBloom, toVignette);
    }

    /// <summary>직교크기/PP는 두고 타임스케일만 보간한다(오버슈트 하강 등).</summary>
    private IEnumerator LerpTimeScaleOnly(float duration, float toTimeScale)
    {
        float fromTimeScale = Time.timeScale;
        if (duration <= 0f)
        {
            SetTimeScale(toTimeScale);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            SetTimeScale(Mathf.Lerp(fromTimeScale, toTimeScale, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        SetTimeScale(toTimeScale);
    }

    private void ApplyState(float timeScale, float ortho, float bloom, float vignette)
    {
        SetTimeScale(timeScale);
        if (_cam != null) _cam.orthographicSize = ortho;
        if (_bloom != null) _bloom.intensity.value = bloom;
        if (_vignette != null) _vignette.intensity.value = vignette;
    }

    private void SetTimeScale(float timeScale)
    {
        Time.timeScale = timeScale;
        // 물리 스텝을 타임스케일에 맞춰 슬로우모션에서도 부드럽게.
        Time.fixedDeltaTime = _baseFixedDelta * Mathf.Max(0.0001f, timeScale);
    }

    /// <summary>연출 종료 시 타임스케일/물리스텝/PP를 확실히 기본값으로 되돌린다.</summary>
    private void FinalizeToNormal()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = _baseFixedDelta;
        if (_bloom != null) _bloom.intensity.value = 0f;
        if (_vignette != null) _vignette.intensity.value = 0f;
        if (_baseOrthoCaptured && _cam != null) _cam.orthographicSize = _baseOrthoSize;
    }

    private void CaptureBaseOrtho()
    {
        // 이미 차징 중(연출 진행 중)이면 확대된 값을 기준으로 잡지 않도록 한 번만 캡처.
        if (_baseOrthoCaptured || _cam == null) return;
        _baseOrthoSize = _cam.orthographicSize;
        _baseOrthoCaptured = true;
    }

    // ───────────────────────── 화면 흔들림 ─────────────────────────

    private void StartShake()
    {
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        float t = 0f;
        while (t < _shakeDuration)
        {
            t += Time.unscaledDeltaTime;
            float damper = 1f - Mathf.Clamp01(t / _shakeDuration); // 점점 잦아듦
            Vector2 rand = Random.insideUnitCircle * (_shakeMagnitude * damper);
            SetShakeOffset(new Vector3(rand.x, rand.y, 0f));
            yield return null;
        }
        SetShakeOffset(Vector3.zero);
        _shakeRoutine = null;
    }

    private void SetShakeOffset(Vector3 offset)
    {
        if (_pan != null) _pan.ExternalShakeOffset = offset;
        else if (_cam != null) _cam.transform.localPosition += offset; // 폴백(팬 컨트롤러 없을 때)
    }

    // ───────────────────────── 안전장치 ─────────────────────────

    private void OnDisable()
    {
        // 씬 전환/비활성화 시 타임스케일이 슬로우모션인 채로 남지 않도록 강제 원복.
        if (_stateRoutine != null) { StopCoroutine(_stateRoutine); _stateRoutine = null; }
        if (_shakeRoutine != null) { StopCoroutine(_shakeRoutine); _shakeRoutine = null; }
        SetShakeOffset(Vector3.zero);
        Time.timeScale = 1f;
        Time.fixedDeltaTime = _baseFixedDelta > 0f ? _baseFixedDelta : 0.02f;
    }
}
