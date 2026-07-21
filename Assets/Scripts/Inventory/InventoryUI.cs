using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 인벤토리 내용을 화면에 표시하고, 디버그로 아이템을 넣어 분류/스택이 맞는지 확인하는 uGUI 패널.
/// - 런타임에 Canvas/Button/Text를 코드로 생성한다(별도 씬 UI 세팅 불필요).
/// - 우상단에 고정. <see cref="_toggleKey"/>(기본 I)로 열고 닫는다.
/// - 좌측 디버그 버튼으로 각 분류에 샘플 아이템을 추가하면, 아래 목록이 분류별로 즉시 갱신된다.
///   (샘플 정의는 에셋 없이 <see cref="ItemDefinition.CreateRuntime"/> 로 만든다.)
///
/// 데이터는 <see cref="InventoryManager"/> 의 <see cref="Inventory"/> 를 그대로 보여준다.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("토글")]
    [SerializeField] private KeyCode _toggleKey = KeyCode.I;
    [SerializeField] private bool _startVisible = true;

    [Header("배치")]
    [SerializeField] private bool _persistAcrossScenes = true;
    [SerializeField] private Vector2 _margin = new Vector2(24f, 24f);
    [SerializeField] private float _panelWidth = 360f;

    private static InventoryUI _instance;

    private Canvas _canvas;
    private RectTransform _contentRoot;
    private Font _font;
    private Inventory _inventory;

    // 디버그용 샘플 정의(에셋 아님, 런타임 인메모리).
    private ItemDefinition _potion;
    private ItemDefinition _grenade;
    private List<ItemDefinition> _parts;
    private BulletItemDefinition _basicBullet;
    private List<BulletItemDefinition> _enhancedBullets;
    private ItemDefinition _gold;
    private int _partCursor;
    private int _bulletCursor;

    /// <summary>
    /// 첫 씬 로드 전에 디버그 UI가 없으면 하나 만든다. 씬 편집 없이 어느 씬에서 Play해도
    /// 우상단에 인벤토리 디버그 패널이 뜬다(확인/디버깅 편의). 정식 빌드에서 빼려면
    /// 이 메서드나 게임오브젝트를 제거하면 된다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("InventoryUI");
        go.AddComponent<InventoryUI>();
    }

    private void Awake()
    {
        if (_persistAcrossScenes)
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildSamples();
        EnsureEventSystem();
        BuildCanvas();
    }

    private void Start()
    {
        _inventory = InventoryManager.Instance != null ? InventoryManager.Instance.Inventory : null;
        if (_inventory != null) _inventory.Changed += Refresh;

        SetVisible(_startVisible);
        Refresh();
    }

    private void OnDestroy()
    {
        if (_inventory != null) _inventory.Changed -= Refresh;
    }

    private void Update()
    {
        if (Input.GetKeyDown(_toggleKey) && _canvas != null)
        {
            SetVisible(!_canvas.enabled);
        }
    }

    // ───────────────────────── 디버그 샘플 데이터 ─────────────────────────

    private void BuildSamples()
    {
        _potion = ItemDefinition.CreateRuntime("potion_hp", "체력 물약", ItemCategory.Item, maxStack: 99);
        _grenade = ItemDefinition.CreateRuntime("grenade", "수류탄", ItemCategory.Item, maxStack: 5);
        _parts = new List<ItemDefinition>
        {
            ItemDefinition.CreateRuntime("part_mag", "확장 탄창", ItemCategory.GunPart),
            ItemDefinition.CreateRuntime("part_grip", "반동 억제 그립", ItemCategory.GunPart),
            ItemDefinition.CreateRuntime("part_barrel", "롱 배럴", ItemCategory.GunPart),
        };
        // 탄환은 실제 BulletSO에 연결된 BulletItemDefinition 에셋(Resources/BulletItems)에서 로드한다.
        // 에셋이 없으면 런타임 문자열 샘플로 폴백(자산 저작 전에도 UI 디버그가 되도록).
        _enhancedBullets = new List<BulletItemDefinition>();
        var loaded = Resources.LoadAll<BulletItemDefinition>("BulletItems");
        foreach (var def in loaded)
        {
            if (def == null) continue;
            if (def.isBasic) _basicBullet = def;
            else _enhancedBullets.Add(def);
        }

        if (_basicBullet == null)
        {
            _basicBullet = BulletItemDefinition.CreateRuntime("bullet_basic", "기본 탄환", isBasic: true);
        }
        if (_enhancedBullets.Count == 0)
        {
            _enhancedBullets.Add(BulletItemDefinition.CreateRuntime("bullet_ap_homing", "관통 유도탄", false, "철갑", "유도"));
            _enhancedBullets.Add(BulletItemDefinition.CreateRuntime("bullet_explo_split", "폭발 분열탄", false, "폭발", "분열"));
            _enhancedBullets.Add(BulletItemDefinition.CreateRuntime("bullet_burn_chain", "화염 연쇄탄", false, "화상", "연쇄 번개"));
            _enhancedBullets.Add(BulletItemDefinition.CreateRuntime("bullet_grav_frost", "중력 냉기탄", false, "중력", "냉기"));
        }

        _gold = ItemDefinition.CreateRuntime("gold", "골드", ItemCategory.Currency, maxStack: 9_999_999);
    }

    // ───────────────────────── UI 구성 ─────────────────────────

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<StandaloneInputModule>(); // 레거시 Input Manager 사용.
        if (_persistAcrossScenes) DontDestroyOnLoad(esGO);
    }

    private void BuildCanvas()
    {
        var canvasGO = new GameObject("InventoryCanvas");
        canvasGO.transform.SetParent(transform, false);

        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 998;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // 우상단 고정 루트 패널(반투명 배경 + 세로 배치).
        var panel = CreateVerticalPanel(_canvas.transform, "Root");
        panel.anchorMin = new Vector2(1f, 1f);
        panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(1f, 1f);
        panel.anchoredPosition = new Vector2(-_margin.x, -_margin.y);
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);

        AddText(panel, $"인벤토리  ( {_toggleKey} 키로 토글 )", 22, new Color(1f, 0.9f, 0.5f), FontStyle.Bold);

        // 디버그 추가 버튼(한 번만 생성).
        AddButton(panel, "＋ 체력 물약", () => InventoryManager.Instance.Add(_potion, 1));
        AddButton(panel, "＋ 수류탄", () => InventoryManager.Instance.Add(_grenade, 1));
        AddButton(panel, "＋ 총기 파츠(순차)", AddNextPart);
        AddButton(panel, "＋ 기본 탄환", () => InventoryManager.Instance.Add(_basicBullet, 1));
        AddButton(panel, "＋ 강화 탄환(순차)", AddNextBullet);
        AddButton(panel, "＋ 재화 ×100", () => InventoryManager.Instance.Add(_gold, 100));
        AddButton(panel, "전체 비우기", () => InventoryManager.Instance.Clear());

        // 실시간 목록이 들어갈 컨테이너(Refresh 때마다 재구성).
        _contentRoot = CreateVerticalPanel(panel, "Content");
    }

    private void AddNextPart()
    {
        var part = _parts[_partCursor % _parts.Count];
        _partCursor++;
        InventoryManager.Instance.Add(part, 1);
    }

    private void AddNextBullet()
    {
        // 강화 탄환은 고유(1발)라 클릭할 때마다 개별 슬롯으로 쌓인다.
        var bullet = _enhancedBullets[_bulletCursor % _enhancedBullets.Count];
        _bulletCursor++;
        InventoryManager.Instance.Add(bullet, 1);
    }

    // ───────────────────────── 목록 갱신 ─────────────────────────

    private void Refresh()
    {
        if (_contentRoot == null) return;

        for (int i = _contentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(_contentRoot.GetChild(i).gameObject);
        }

        if (_inventory == null)
        {
            AddText(_contentRoot, "(InventoryManager 없음)", 16, Color.red);
            return;
        }

        // 분류별로 구분해서 표시.
        foreach (ItemCategory category in System.Enum.GetValues(typeof(ItemCategory)))
        {
            int slots = _inventory.GetSlotCount(category);
            int total = _inventory.GetTotalCount(category);
            AddText(_contentRoot, $"── {category.ToKorean()} ──  (슬롯 {slots} / 합계 {total})",
                    18, new Color(0.6f, 0.85f, 1f), FontStyle.Bold);

            var entries = _inventory.GetEntries(category);
            if (entries.Count == 0)
            {
                AddText(_contentRoot, "   · (비어 있음)", 16, new Color(0.7f, 0.7f, 0.7f));
                continue;
            }

            foreach (var entry in entries)
            {
                string label = entry.Definition != null ? entry.Definition.ResolvedName : "(null)";

                // 강화 탄환은 부여된 능력들을 함께 표시(고유 탄환임을 확인).
                if (entry.Definition is BulletItemDefinition bullet)
                {
                    var abilities = bullet.GetAbilityLabels();
                    if (abilities.Count > 0) label += $"  [{string.Join(", ", abilities)}]";
                }

                AddText(_contentRoot, $"   · {label}  ×{entry.Quantity}", 16, Color.white);
            }
        }
    }

    private void SetVisible(bool visible)
    {
        if (_canvas != null) _canvas.enabled = visible;
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

    private Button AddButton(Transform parent, string label, UnityAction onClick)
    {
        var go = new GameObject($"Button_{label}", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.color = new Color(0.18f, 0.20f, 0.26f, 0.95f);

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        var colors = button.colors;
        colors.normalColor = new Color(0.18f, 0.20f, 0.26f, 0.95f);
        colors.highlightedColor = new Color(0.28f, 0.34f, 0.44f, 1f);
        colors.pressedColor = new Color(0.10f, 0.12f, 0.16f, 1f);
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
