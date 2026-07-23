using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 왼쪽 아래에 "탄환 인벤토리"만 따로 보여주는 HUD.
/// 숫자키 1~5로 선택한 탄환을 강조 표시한다(선택/발사 로직은 <see cref="PlayerShooter"/>가 소유).
///
/// - 런타임에 Canvas/Text를 코드로 생성한다(별도 씬 UI 세팅 불필요).
/// - <see cref="PlayerShooter"/> 가 있는 씬(스테이지)에서만 표시되고, 없으면 자동으로 숨는다.
/// - 표시 전용(버튼 없음)이라 EventSystem/Raycaster가 필요 없다. 전체 인벤토리는 InventoryUI(우상단)가 담당.
/// </summary>
public class BulletInventoryUI : MonoBehaviour
{
    [Header("배치")]
    [SerializeField] private Vector2 _margin = new Vector2(24f, 24f);
    [SerializeField] private float _panelWidth = 340f;

    private static BulletInventoryUI _instance;

    private Canvas _canvas;
    private RectTransform _contentRoot;
    private Font _font;

    private PlayerShooter _shooter;
    private float _findTimer;
    private int _lastSignature;

    /// <summary>첫 씬 로드 후 HUD가 없으면 하나 만든다(씬 편집 없이 어느 씬에서 Play해도 표시).</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("BulletInventoryUI");
        go.AddComponent<BulletInventoryUI>();
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
        // 스테이지 씬으로 바뀌면 PlayerShooter를 다시 찾는다(씬 전환에도 유지되는 HUD).
        if (_shooter == null)
        {
            _findTimer -= Time.deltaTime;
            if (_findTimer <= 0f)
            {
                _findTimer = 0.25f;
                _shooter = FindObjectOfType<PlayerShooter>();
            }
        }

        bool visible = _shooter != null && _shooter.Choices.Count > 0;
        if (_canvas != null && _canvas.enabled != visible) _canvas.enabled = visible;

        // 선택/목록이 바뀌었을 때만 다시 그린다(가벼운 시그니처 비교).
        int sig = ComputeSignature();
        if (sig != _lastSignature)
        {
            _lastSignature = sig;
            Refresh();
        }
    }

    /// <summary>선택 인덱스와 탄환 목록(정의·수량)을 합친 변경 감지용 해시.</summary>
    private int ComputeSignature()
    {
        if (_shooter == null) return 0;

        int h = 17;
        h = h * 31 + _shooter.SelectedIndex;

        var choices = _shooter.Choices;
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
        var canvasGO = new GameObject("BulletHudCanvas");
        canvasGO.transform.SetParent(transform, false);

        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 997;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 좌하단 고정 루트 패널(반투명 배경 + 세로 배치).
        var panel = CreateVerticalPanel(_canvas.transform, "Root");
        panel.anchorMin = new Vector2(0f, 0f);
        panel.anchorMax = new Vector2(0f, 0f);
        panel.pivot = new Vector2(0f, 0f);
        panel.anchoredPosition = new Vector2(_margin.x, _margin.y);
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);

        AddText(panel, "탄환  ( 1~5 선택 )", 20, new Color(1f, 0.9f, 0.5f), FontStyle.Bold);

        _contentRoot = CreateVerticalPanel(panel, "Content");
    }

    private void Refresh()
    {
        if (_contentRoot == null) return;

        for (int i = _contentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(_contentRoot.GetChild(i).gameObject);
        }

        if (_shooter == null) return;

        var choices = _shooter.Choices;
        if (choices.Count == 0)
        {
            AddText(_contentRoot, "(탄환 없음)", 16, new Color(0.7f, 0.7f, 0.7f));
            return;
        }

        int selected = _shooter.SelectedIndex;
        for (int i = 0; i < choices.Count; i++)
        {
            var c = choices[i];
            bool isSelected = i == selected;

            string label = c.Definition != null ? c.Definition.ResolvedName : "(null)";
            if (c.Definition is BulletItemDefinition bullet)
            {
                var abilities = bullet.GetAbilityLabels();
                if (abilities.Count > 0) label += $"  [{string.Join(", ", abilities)}]";
            }

            // 선택된 슬롯은 ▶ 표시 + 노란색 굵게 강조.
            string prefix = isSelected ? "▶ " : "   ";
            string row = $"{prefix}[{i + 1}] {label}  ×{c.Count}";

            Color color = isSelected ? new Color(1f, 0.95f, 0.4f) : Color.white;
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
