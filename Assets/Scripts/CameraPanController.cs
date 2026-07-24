using UnityEngine;

/// <summary>
/// 탑뷰(직교) 각도를 고정한 채 방향키로 화면을 이동시키는 카메라 팬 컨트롤러.
/// - 카메라 회전은 건드리지 않는다(탑뷰 유지).
/// - XY 평면에서만 이동하고 Z(깊이)는 고정한다.
/// - 저격수(플레이어)는 제자리에 고정이지만, 플레이어와 별개로 전장을 둘러볼 수 있게 화면만 움직인다.
///
/// 입력은 Legacy Input Manager 기준. 방향키만 카메라를 움직인다
/// (WASD는 PlayerMovement가 플레이어 구체 이동에 사용).
/// </summary>
public class CameraPanController : MonoBehaviour
{
    [Header("이동")]
    [Tooltip("팬 속도 (월드 유닛/초).")]
    [SerializeField] private float _panSpeed = 10f;

    [Header("이동 범위 제한 (선택)")]
    [Tooltip("체크 시 아래 min/max 범위 안에서만 이동한다.")]
    [SerializeField] private bool _useBounds = false;
    [SerializeField] private Vector2 _minBounds = new Vector2(-20f, -20f);
    [SerializeField] private Vector2 _maxBounds = new Vector2(20f, 20f);

    [Header("줌 (마우스 휠)")]
    [Tooltip("휠 1노치당 변하는 직교 크기.")]
    [SerializeField] private float _zoomSpeed = 5f;
    [Tooltip("가장 확대(작은 orthographicSize).")]
    [SerializeField] private float _minZoom = 2f;
    [Tooltip("가장 축소(큰 orthographicSize).")]
    [SerializeField] private float _maxZoom = 15f;

    private Camera _cam;

    /// <summary>
    /// false면 이 프레임 팬/줌 입력을 무시한다. 저격 호흡(격발 대기) 중 화면을 완전히
    /// 고정하고 싶을 때 PlayerShooter가 잠시 꺼준다.
    /// </summary>
    public bool ControlsEnabled = true;

    /// <summary>
    /// 팬 위치 위에 얹는 외부 흔들림 오프셋(월드). <see cref="ChargeShotEffects"/>가 발사 시
    /// 매 프레임 세팅하고 끝나면 0으로 되돌린다. 팬 base 위치와 싸우지 않도록 LateUpdate에서
    /// 이전 프레임에 얹은 오프셋을 빼고 새 오프셋을 더하는 방식으로 반영한다.
    /// </summary>
    public Vector3 ExternalShakeOffset { get; set; }
    private Vector3 _appliedShake;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        if (_cam == null) _cam = Camera.main;
    }

    private void Update()
    {
        if (!ControlsEnabled) return;

        HandlePan();
        HandleZoom();
    }

    private void LateUpdate()
    {
        // 흔들림은 ControlsEnabled와 무관하게 항상 반영한다(발사 순간엔 조준 고정 중일 수 있음).
        if (ExternalShakeOffset != _appliedShake)
        {
            transform.position += ExternalShakeOffset - _appliedShake;
            _appliedShake = ExternalShakeOffset;
        }
    }

    private void HandlePan()
    {
        // 방향키만 카메라를 움직인다(WASD는 플레이어 이동 전용).
        float x = 0f;
        float y = 0f;
        if (Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
        if (Input.GetKey(KeyCode.RightArrow)) x += 1f;
        if (Input.GetKey(KeyCode.DownArrow)) y -= 1f;
        if (Input.GetKey(KeyCode.UpArrow)) y += 1f;
        if (x == 0f && y == 0f) return;

        Vector3 move = new Vector3(x, y, 0f).normalized * (_panSpeed * Time.deltaTime);
        Vector3 pos = transform.position + move;

        if (_useBounds)
        {
            pos.x = Mathf.Clamp(pos.x, _minBounds.x, _maxBounds.x);
            pos.y = Mathf.Clamp(pos.y, _minBounds.y, _maxBounds.y);
        }

        transform.position = pos; // Z와 회전(탑뷰 각도)은 그대로 유지
    }

    private void HandleZoom()
    {
        if (_cam == null || !_cam.orthographic) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll == 0f) return;

        // 휠을 위로(+)는 확대(size 감소), 아래로(-)는 축소(size 증가).
        float size = _cam.orthographicSize - scroll * _zoomSpeed;
        _cam.orthographicSize = Mathf.Clamp(size, _minZoom, _maxZoom);
    }
}
