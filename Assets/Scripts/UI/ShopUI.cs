using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 상점(Shop) 씬 전용 uGUI. 다른 HUD들(InventoryUI 등)과 달리 전역 부트스트랩/DontDestroyOnLoad가
/// 아니라 Shop 씬에만 배치되는 일반 컴포넌트다(상점은 그 자체로 독립된 씬이라 다른 씬엔 필요 없음).
///
/// - 좌측: 구매 패널(ShopManager.GetCatalog() 목록 + 구매 버튼).
/// - 우측: 탄환 조합 패널(속성 없는 보유 탄환 선택 + 속성 선택 + 조합 버튼).
/// - 하단: 다음 스테이지로 출발 버튼(SceneLoader.LoadNextStage).
///
/// 코드로 Canvas/Button/Text를 생성하는 방식은 InventoryUI/WeaponPartsUI와 동일한 컨벤션을 따른다.
/// </summary>
public class ShopUI : MonoBehaviour
{
    [Header("배치")]
    [SerializeField] private Vector2 _margin = new Vector2(24f, 24f);
    [SerializeField] private float _panelWidth = 360f;

    private static readonly ElementType[] AllElements =
    {
        ElementType.Fire, ElementType.Water, ElementType.Wind,
        ElementType.Earth, ElementType.Electric, ElementType.Ice,
    };

    private Font _font;
    private Inventory _inventory;

    private Text _goldText;
    private RectTransform _catalogRoot;
    private RectTransform _combineListRoot;
    private RectTransform _elementRoot;
    private Text _combineStatusText;

    private BulletItemDefinition _selectedSource;
    private ElementType _selectedElement = ElementType.None;

    private void Awake()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureEventSystem();
        BuildCanvas();
    }

    private void Start()
    {
        _inventory = InventoryManager.Instance != null ? InventoryManager.Instance.Inventory : null;
        if (_inventory != null) _inventory.Changed += RefreshAll;
        RefreshAll();
    }

    private void OnDestroy()
    {
        if (_inventory != null) _inventory.Changed -= RefreshAll;
    }

    // ───────────────────────── UI 구성 ─────────────────────────

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<StandaloneInputModule>(); // 레거시 Input Manager 사용.
    }

    private void BuildCanvas()
    {
        var canvasGO = new GameObject("ShopCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        BuildGoldPanel(canvas.transform);
        BuildCatalogPanel(canvas.transform);
        BuildCombinePanel(canvas.transform);
        BuildContinuePanel(canvas.transform);
    }

    private void BuildGoldPanel(Transform parent)
    {
        var panel = CreateVerticalPanel(parent, "GoldPanel");
        panel.anchorMin = new Vector2(0.5f, 1f);
        panel.anchorMax = new Vector2(0.5f, 1f);
        panel.pivot = new Vector2(0.5f, 1f);
        panel.anchoredPosition = new Vector2(0f, -_margin.y);
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);

        _goldText = AddText(panel, "보유 골드: 0", 24, new Color(1f, 0.9f, 0.4f), FontStyle.Bold);
    }

    private void BuildCatalogPanel(Transform parent)
    {
        var panel = CreateVerticalPanel(parent, "CatalogPanel");
        panel.anchorMin = new Vector2(0f, 1f);
        panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 1f);
        panel.anchoredPosition = new Vector2(_margin.x, -_margin.y);
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);

        AddText(panel, "구매", 22, new Color(0.6f, 0.95f, 1f), FontStyle.Bold);
        _catalogRoot = CreateVerticalPanel(panel, "CatalogList");
    }

    private void BuildCombinePanel(Transform parent)
    {
        var panel = CreateVerticalPanel(parent, "CombinePanel");
        panel.anchorMin = new Vector2(1f, 1f);
        panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(1f, 1f);
        panel.anchoredPosition = new Vector2(-_margin.x, -_margin.y);
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);

        AddText(panel, $"탄환 조합 (비용 {ShopManager.CombineCost} 골드)", 22, new Color(1f, 0.75f, 0.5f), FontStyle.Bold);

        AddText(panel, "원본 탄환 선택", 16, new Color(0.8f, 0.8f, 0.8f));
        _combineListRoot = CreateVerticalPanel(panel, "CombineCandidates");

        AddText(panel, "속성 선택", 16, new Color(0.8f, 0.8f, 0.8f));
        _elementRoot = CreateVerticalPanel(panel, "ElementButtons");

        var combineBtn = AddButton(panel, "조합하기", new Color(0.20f, 0.28f, 0.40f, 0.95f));
        combineBtn.onClick.AddListener(OnCombineClicked);

        _combineStatusText = AddText(panel, "", 15, new Color(1f, 0.6f, 0.6f));
    }

    private void BuildContinuePanel(Transform parent)
    {
        var panel = CreateVerticalPanel(parent, "ContinuePanel");
        panel.anchorMin = new Vector2(0.5f, 0f);
        panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, _margin.y);

        var btn = AddButton(panel, "다음 스테이지로 출발", new Color(0.18f, 0.34f, 0.20f, 0.95f));
        btn.onClick.AddListener(() => SceneLoader.LoadNextStage());
    }

    // ───────────────────────── 갱신 ─────────────────────────

    private void RefreshAll()
    {
        RefreshGold();
        RefreshCatalog();
        RefreshCombineCandidates();
        RefreshElementButtons();
    }

    private void RefreshGold()
    {
        if (_goldText != null) _goldText.text = $"보유 골드: {ShopManager.CurrentGold}";
    }

    private void RefreshCatalog()
    {
        if (_catalogRoot == null) return;
        ClearChildren(_catalogRoot);

        var catalog = ShopManager.GetCatalog();
        if (catalog.Count == 0)
        {
            AddText(_catalogRoot, "(판매 중인 아이템 없음)", 16, new Color(0.7f, 0.7f, 0.7f));
            return;
        }

        foreach (var item in catalog)
        {
            if (item == null) continue;
            int owned = _inventory != null ? _inventory.GetQuantity(item) : 0;
            string label = $"{item.ResolvedName}  ({item.shopPrice}G)  보유 {owned}";

            var row = AddButton(_catalogRoot, label, new Color(0.16f, 0.20f, 0.28f, 0.95f));
            row.onClick.AddListener(() =>
            {
                if (ShopManager.TryPurchase(item, 1, out string reason)) RefreshAll();
                else Debug.Log($"[ShopUI] 구매 실패: {reason}");
            });
        }
    }

    /// <summary>인벤토리 Ammo 중 속성이 아직 없는 탄환을 종류별로 묶어 조합 후보 목록을 만든다.</summary>
    private List<BulletItemDefinition> GetCombineCandidates()
    {
        var result = new List<BulletItemDefinition>();
        if (_inventory == null) return result;

        foreach (var entry in _inventory.GetEntries(ItemCategory.Ammo))
        {
            if (entry.Quantity <= 0) continue;
            if (!(entry.Definition is BulletItemDefinition bullet)) continue;
            if (bullet.bulletData == null || bullet.bulletData.element != ElementType.None) continue;

            bool alreadyListed = false;
            foreach (var existing in result)
            {
                if (existing == bullet || existing.id == bullet.id) { alreadyListed = true; break; }
            }
            if (!alreadyListed) result.Add(bullet);
        }
        return result;
    }

    private void RefreshCombineCandidates()
    {
        if (_combineListRoot == null) return;
        ClearChildren(_combineListRoot);

        var candidates = GetCombineCandidates();
        if (_selectedSource != null && !candidates.Contains(_selectedSource)) _selectedSource = null;

        if (candidates.Count == 0)
        {
            AddText(_combineListRoot, "(조합 가능한 탄환 없음)", 15, new Color(0.7f, 0.7f, 0.7f));
            return;
        }

        foreach (var bullet in candidates)
        {
            bool isSelected = bullet == _selectedSource;
            string prefix = isSelected ? "▶ " : "   ";
            var row = AddButton(_combineListRoot, $"{prefix}{bullet.ResolvedName}",
                isSelected ? new Color(0.20f, 0.34f, 0.20f, 0.95f) : new Color(0.16f, 0.20f, 0.28f, 0.95f));
            row.onClick.AddListener(() =>
            {
                _selectedSource = bullet;
                RefreshCombineCandidates();
            });
        }
    }

    private void RefreshElementButtons()
    {
        if (_elementRoot == null) return;
        ClearChildren(_elementRoot);

        foreach (var element in AllElements)
        {
            bool isSelected = element == _selectedElement;
            string prefix = isSelected ? "▶ " : "   ";
            var row = AddButton(_elementRoot, $"{prefix}{element.ToKorean()}",
                isSelected ? new Color(0.34f, 0.24f, 0.16f, 0.95f) : new Color(0.16f, 0.20f, 0.28f, 0.95f));
            row.onClick.AddListener(() =>
            {
                _selectedElement = element;
                RefreshElementButtons();
            });
        }
    }

    private void OnCombineClicked()
    {
        if (ShopManager.TryCombine(_selectedSource, _selectedElement, out string reason))
        {
            _selectedSource = null;
            _selectedElement = ElementType.None;
            if (_combineStatusText != null) _combineStatusText.text = "조합 성공!";
            RefreshAll();
        }
        else
        {
            if (_combineStatusText != null) _combineStatusText.text = reason;
            Debug.Log($"[ShopUI] 조합 실패: {reason}");
        }
    }

    // ───────────────────────── 위젯 생성 헬퍼 ─────────────────────────

    private void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

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
        text.fontSize = 18;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;

        var trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        return button;
    }
}
