using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 무기 파츠를 켜고 끄는 런타임 uGUI 패널. <see cref="_toggleKey"/>(기본 O)로 열고 닫는다.
/// 현재 씬의 <see cref="PlayerShooter"/>가 장착한 파츠를 나열하고, 각 항목 버튼으로
/// 활성/비활성(효과 on/off)을 토글한다. 장착 리스트에서 빼는 게 아니라 효과만 껐다 켠다.
///
/// <see cref="InventoryUI"/>와 같은 방식: 코드로 Canvas/Button/Text를 생성하며 씬 UI 세팅이 필요 없다.
/// 패널이 열려 있는 동안 <see cref="PlayerShooter"/>가 게임을 정지(타임스케일 0)시키고 조준/격발을 막는다.
/// </summary>
public class WeaponPartsUI : MonoBehaviour
{
    [Header("토글")]
    [SerializeField] private KeyCode _toggleKey = KeyCode.O;
    [SerializeField] private bool _startVisible = false;

    [Header("배치")]
    [SerializeField] private Vector2 _margin = new Vector2(24f, 24f);
    [SerializeField] private float _panelWidth = 320f;

    private static WeaponPartsUI _instance;

    /// <summary>파츠 패널이 현재 열려(보이는) 있는지. 열려 있으면 게임 정지 + 사격 입력 차단에 쓴다.</summary>
    public static bool IsOpen => _instance != null && _instance._canvas != null && _instance._canvas.enabled;

    private Canvas _canvas;
    private RectTransform _contentRoot;
    private Font _font;
    private PlayerShooter _shooter;

    /// <summary>어느 씬에서 Play해도 파츠 UI가 뜨도록 부트스트랩(별도 씬 세팅 불필요).</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("WeaponPartsUI");
        go.AddComponent<WeaponPartsUI>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureEventSystem();
        BuildCanvas();
    }

    private void Start()
    {
        SetVisible(_startVisible);
    }

    private void Update()
    {
        if (Input.GetKeyDown(_toggleKey) && _canvas != null)
        {
            SetVisible(!_canvas.enabled);
        }
    }

    // ───────────────────────── UI 구성 ─────────────────────────

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<StandaloneInputModule>(); // 레거시 Input Manager 사용.
        DontDestroyOnLoad(esGO);
    }

    private void BuildCanvas()
    {
        var canvasGO = new GameObject("WeaponPartsCanvas");
        canvasGO.transform.SetParent(transform, false);

        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 999;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f; // 폭 기준(세로 고정)

        canvasGO.AddComponent<GraphicRaycaster>();

        // 상태바/노치를 피하기 위한 안전영역 루트.
        var safeGO = new GameObject("SafeArea", typeof(RectTransform));
        safeGO.transform.SetParent(_canvas.transform, false);
        var safe = (RectTransform)safeGO.transform;
        safe.anchorMin = Vector2.zero;
        safe.anchorMax = Vector2.one;
        safe.offsetMin = Vector2.zero;
        safe.offsetMax = Vector2.zero;
        safeGO.AddComponent<SafeAreaFitter>();

        // 좌상단 고정 패널(인벤토리는 우상단이라 겹치지 않는다).
        var panel = CreateVerticalPanel(safe, "Root");
        panel.anchorMin = new Vector2(0f, 1f);
        panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 1f);
        panel.anchoredPosition = new Vector2(_margin.x, -_margin.y);
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);

        AddText(panel, $"무기 파츠  ( {_toggleKey} 키로 토글 · 클릭해서 켜고 끄기 )",
                20, new Color(1f, 0.9f, 0.5f), FontStyle.Bold);

        _contentRoot = CreateVerticalPanel(panel, "Content");
    }

    // ───────────────────────── 목록 갱신 ─────────────────────────

    /// <summary>현재 씬의 PlayerShooter를 찾아 파츠 목록/토글 버튼을 다시 만든다.</summary>
    private void Refresh()
    {
        if (_contentRoot == null) return;

        for (int i = _contentRoot.childCount - 1; i >= 0; i--)
            Destroy(_contentRoot.GetChild(i).gameObject);

        if (_shooter == null) _shooter = FindObjectOfType<PlayerShooter>();
        if (_shooter == null)
        {
            AddText(_contentRoot, "(씬에 PlayerShooter가 없습니다)", 16, Color.red);
            return;
        }

        IReadOnlyList<WeaponPartSO> parts = _shooter.EquippedParts;
        if (parts == null || parts.Count == 0)
        {
            AddText(_contentRoot, "· 장착된 파츠가 없습니다", 16, new Color(0.7f, 0.7f, 0.7f));
            return;
        }

        foreach (var part in parts)
        {
            if (part == null) continue;
            AddPartRow(part);
        }

        // 강선 강화(디버그): 현재 강화도/대미지 배율 표시 + 강화 버튼.
        AddText(_contentRoot,
            $"강선 강화도: +{_shooter.RiflingLevel}  (대미지 ×{_shooter.DamageMultiplier:0.##})",
            16, new Color(1f, 0.8f, 0.4f), FontStyle.Bold);
        var upgradeBtn = AddButton(_contentRoot, "＋ 강선 강화 (+25%)", new Color(0.20f, 0.28f, 0.40f, 0.95f));
        upgradeBtn.onClick.AddListener(() =>
        {
            _shooter.UpgradeRifling();
            Refresh(); // 강화도/배율 라벨 즉시 갱신.
        });
    }

    /// <summary>파츠 한 개의 ON/OFF 토글 버튼을 만든다.</summary>
    private void AddPartRow(WeaponPartSO part)
    {
        bool active = _shooter.IsPartActive(part);
        string state = active ? "ON " : "OFF";
        Color onColor = new Color(0.16f, 0.34f, 0.20f, 0.95f);   // 초록빛(켜짐)
        Color offColor = new Color(0.30f, 0.16f, 0.16f, 0.95f);  // 붉은빛(꺼짐)

        var button = AddButton(_contentRoot, $"[{state}]  {part.DisplayName}",
                               active ? onColor : offColor);
        button.onClick.AddListener(() =>
        {
            _shooter.TogglePart(part);
            Refresh(); // 라벨/색을 즉시 갱신.
        });
    }

    private void SetVisible(bool visible)
    {
        if (_canvas != null) _canvas.enabled = visible;
        if (visible)
        {
            _shooter = FindObjectOfType<PlayerShooter>(); // 씬이 바뀌었을 수 있으니 다시 찾는다.
            Refresh();
        }
    }

    // ───────────────────────── 위젯 생성 헬퍼 ─────────────────────────

    private RectTransform CreateVerticalPanel(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.padding = new RectOffset(12, 12, 10, 12);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = go.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return go.GetComponent<RectTransform>();
    }

    private Text AddText(Transform parent, string message, int fontSize, Color color, FontStyle style = FontStyle.Normal)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<Text>();
        text.font = _font;
        text.text = message;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = _panelWidth;
        le.preferredHeight = fontSize + 8f;

        return text;
    }

    private Button AddButton(Transform parent, string label, Color baseColor)
    {
        var go = new GameObject($"Button_{label}", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.color = baseColor;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;

        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        button.colors = colors;

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = _panelWidth;
        le.preferredHeight = 40f;

        var textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);
        var text = textGO.AddComponent<Text>();
        text.font = _font;
        text.text = label;
        text.fontSize = 20;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;

        var trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        return button;
    }
}
