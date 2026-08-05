using UnityEngine;

/// <summary>
/// 노치/펀치홀/제스처바를 피해 자식 UI를 안전영역(Screen.safeArea) 안으로 밀어넣는다.
///
/// 사용법: Canvas 바로 아래에 전체 스트레치된 빈 RectTransform("SafeArea")을 두고
/// 이 컴포넌트를 붙인 뒤, 잘리면 안 되는 UI를 전부 그 아래에 넣는다.
/// 화면 전체를 덮어야 하는 배경(BG/Veil)은 SafeArea 밖(Canvas 직속)에 두어야
/// 노치 영역까지 배경이 채워진다.
///
/// 안전영역은 anchorMin/anchorMax(정규화 좌표)로 반영하므로 자식의 앵커링은 그대로 유지된다.
///
/// 런타임 전용이다(ExecuteAlways 아님). 에디터 편집 중에 앵커를 건드리면 프리팹 인스턴스에
/// 원치 않는 오버라이드가 씬에 저장될 수 있어서, 안전영역 반영은 플레이 중에만 한다.
/// 노치 배치를 미리 보려면 Device Simulator를 켜고 플레이하면 된다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    [Header("적용할 변")]
    [SerializeField] private bool _applyLeft = true;
    [SerializeField] private bool _applyRight = true;
    [SerializeField] private bool _applyTop = true;
    [SerializeField] private bool _applyBottom = true;

    private RectTransform _rt;
    private Rect _lastSafeArea = new Rect(0f, 0f, 0f, 0f);
    private Vector2Int _lastScreen = Vector2Int.zero;
    private ScreenOrientation _lastOrientation = ScreenOrientation.AutoRotation;

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        // 캐시를 비워 다음 프레임에 무조건 한 번 재계산되게 한다.
        _lastScreen = Vector2Int.zero;
        Apply();
    }

    private void Update()
    {
        Apply();
    }

    private void Apply()
    {
        if (_rt == null) _rt = GetComponent<RectTransform>();
        if (_rt == null) return;

        int sw = Screen.width;
        int sh = Screen.height;
        if (sw <= 0 || sh <= 0) return;

        Rect safe = Screen.safeArea;
        var screen = new Vector2Int(sw, sh);
        if (safe == _lastSafeArea && screen == _lastScreen && Screen.orientation == _lastOrientation)
            return;

        _lastSafeArea = safe;
        _lastScreen = screen;
        _lastOrientation = Screen.orientation;

        Vector2 min = safe.position;
        Vector2 max = safe.position + safe.size;

        min.x /= sw;
        min.y /= sh;
        max.x /= sw;
        max.y /= sh;

        // 비정상 값(에디터 초기 프레임 등) 방어.
        if (float.IsNaN(min.x) || float.IsNaN(min.y) || float.IsNaN(max.x) || float.IsNaN(max.y)) return;
        if (max.x - min.x <= 0f || max.y - min.y <= 0f) return;

        if (!_applyLeft) min.x = 0f;
        if (!_applyBottom) min.y = 0f;
        if (!_applyRight) max.x = 1f;
        if (!_applyTop) max.y = 1f;

        _rt.anchorMin = min;
        _rt.anchorMax = max;
        _rt.offsetMin = Vector2.zero;
        _rt.offsetMax = Vector2.zero;
    }
}
