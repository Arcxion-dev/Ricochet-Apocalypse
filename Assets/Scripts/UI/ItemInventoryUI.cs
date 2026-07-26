using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 왼쪽 아래, "탄환 인벤토리(BulletInventoryUI)" 바로 위에 표시되는 "사용 아이템" HUD.
/// E키(아이템 변경 모드)로 선택을 바꾸고 F키로 사용한다(선택/사용 로직은 <see cref="PlayerShooter"/> 소유).
///
/// - 런타임에 Canvas/Text를 코드로 생성한다(별도 씬 UI 세팅 불필요).
/// - <see cref="PlayerShooter"/> 가 있는 씬(스테이지)에서만 표시되고, 없으면 자동으로 숨는다.
/// - 표시 전용(버튼 없음). 전체 인벤토리는 InventoryUI(우상단), 탄환은 BulletInventoryUI(좌하단)가 담당.
/// </summary>
public class ItemInventoryUI : MonoBehaviour
{
    [Header("배치")]
    [Tooltip("좌하단 기준 여백. 탄환 HUD 위에 얹히도록 y를 충분히 띄운다.")]
    [SerializeField] private Vector2 _margin = new Vector2(24f, 260f);
    [SerializeField] private float _panelWidth = 340f;

    private static ItemInventoryUI _instance;

    private Canvas _canvas;
    private RectTransform _contentRoot;
    private Font _font;

    private PlayerShooter _shooter;
    private float _findTimer;
    private int _lastSignature;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("ItemInventoryUI");
        go.AddComponent<ItemInventoryUI>();
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
        BuildCanvas();
    }

    private void Update()
    {
        if (_shooter == null)
        {
            _findTimer -= Time.deltaTime;
            if (_findTimer <= 0f)
            {
                _findTimer = 0.25f;
                _shooter = FindObjectOfType<PlayerShooter>();
            }
        }

        bool visible = _shooter != null;
        if (_canvas != null && _canvas.enabled != visible) _canvas.enabled = visible;

        int sig = ComputeSignature();
        if (sig != _lastSignature)
        {
            _lastSignature = sig;
            Refresh();
        }
    }

    private int ComputeSignature()
    {
        if (_shooter == null) return 0;

        int h = 17;
        h = h * 31 + _shooter.SelectedItemIndex;
        h = h * 31 + _shooter.ActiveModeLabel.GetHashCode();

        var choices = _shooter.ItemChoices;
        h = h * 31 + choices.Count;
        foreach (var c in choices)
        {
            h = h * 31 + (c.Definition != null ? c.Definition.GetInstanceID() : 0);
            h = h * 31 + c.Count;
        }
        return h;
    }

    // ───────────────────────── UI 구성 ─────────────────────────

    private void BuildCanvas()
    {
        var canvasGO = new GameObject("ItemHudCanvas");
        canvasGO.transform.SetParent(transform, false);

        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 997;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var panel = CreateVerticalPanel(_canvas.transform, "Root");
        panel.anchorMin = new Vector2(0f, 0f);
        panel.anchorMax = new Vector2(0f, 0f);
        panel.pivot = new Vector2(0f, 0f);
        panel.anchoredPosition = new Vector2(_margin.x, _margin.y);
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);

        AddText(panel, "아이템  ( E 변경 / F 사용 )", 20, new Color(0.6f, 0.95f, 1f), FontStyle.Bold);

        _contentRoot = CreateVerticalPanel(panel, "Content");
    }

    private void Refresh()
    {
        if (_contentRoot == null) return;

        for (int i = _contentRoot.childCount - 1; i >= 0; i--)
            Destroy(_contentRoot.GetChild(i).gameObject);

        if (_shooter == null) return;

        // 활성 모드 안내(탄환/아이템 변경, 아이템 조준 중).
        string mode = _shooter.ActiveModeLabel;
        if (!string.IsNullOrEmpty(mode))
            AddText(_contentRoot, $"» {mode}", 16, new Color(1f, 0.85f, 0.4f), FontStyle.Bold);

        var choices = _shooter.ItemChoices;
        if (choices.Count == 0)
        {
            AddText(_contentRoot, "(아이템 없음)", 16, new Color(0.7f, 0.7f, 0.7f));
            return;
        }

        int selected = _shooter.SelectedItemIndex;
        for (int i = 0; i < choices.Count; i++)
        {
            var c = choices[i];
            bool isSelected = i == selected;

            string name = c.Definition != null ? c.Definition.ResolvedName : "(null)";
            string kind = c.Definition != null ? c.Definition.KindLabel : "";

            string prefix = isSelected ? "▶ " : "   ";
            string row = $"{prefix}[{i + 1}] {name}  ×{c.Count}";

            Color color = isSelected ? new Color(0.6f, 0.95f, 1f) : Color.white;
            FontStyle style = isSelected ? FontStyle.Bold : FontStyle.Normal;
            AddText(_contentRoot, row, isSelected ? 20 : 18, color, style);
        }
    }

    // ───────────────────────── 위젯 생성 헬퍼 ─────────────────────────

    private RectTransform CreateVerticalPanel(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.padding = new RectOffset(12, 12, 10, 12);
        layout.childAlignment = TextAnchor.LowerLeft;
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
}
