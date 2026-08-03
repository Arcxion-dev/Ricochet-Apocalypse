using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 개발용 인벤토리 디버그 창(예전 InventoryUI의 "아이템/골드 추가" 버튼 기능을 복원한 것).
/// 새 인벤토리(<see cref="InventoryUI"/>)는 표시 전용이라, 아이템/재화를 넣어 테스트하는 버튼은 여기로 분리했다.
///
/// - <b>- 키</b>(Minus / Keypad Minus)로 토글. 기본 숨김.
/// - <see cref="Bootstrap"/>으로 자동 생성(씬 배치 불필요), 씬 전환에도 유지.
/// - 열려 있으면 <see cref="IsOpen"/>가 true → PlayerShooter가 사격/입력을 막고 게임을 멈춘다(오사격 방지).
/// - 코드 생성 uGUI(디버그 도구라 프리팹 불필요). 한글 표시를 위해 TMP + Noto 사용.
/// </summary>
public class InventoryDebugUI : MonoBehaviour
{
    [SerializeField] private KeyCode _toggleKey = KeyCode.Minus;

    private static InventoryDebugUI _instance;
    public static bool IsOpen => _instance != null && _instance._canvas != null && _instance._canvas.enabled;

    private Canvas _canvas;
    private RectTransform _list;

    // 샘플/실 정의.
    private ItemDefinition _potion, _gold;
    private UsableItemSO _grenade, _airstrike, _flashbang;
    private List<ItemDefinition> _parts;
    private BulletItemDefinition _basicBullet;
    private readonly List<BulletItemDefinition> _enhancedBullets = new List<BulletItemDefinition>();
    private int _partCursor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("InventoryDebugUI");
        go.AddComponent<InventoryDebugUI>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureEventSystem();
        BuildSamples();
        BuildCanvas();
        _canvas.enabled = false;
    }

    private void Update()
    {
        if ((Input.GetKeyDown(_toggleKey) || Input.GetKeyDown(KeyCode.KeypadMinus)) && _canvas != null)
        {
            _canvas.enabled = !_canvas.enabled;
            if (_canvas.enabled) _canvas.transform.SetAsLastSibling();
        }
    }

    // ───────────────────────── 샘플 데이터(옛 InventoryUI 이식) ─────────────────────────

    private void BuildSamples()
    {
        _potion = ItemDefinition.CreateRuntime("potion_hp", "체력 물약", ItemCategory.Item, maxStack: 99);

        foreach (var def in Resources.LoadAll<UsableItemSO>("UsableItems"))
        {
            if (def == null) continue;
            switch (def.kind)
            {
                case UsableItemKind.Grenade: _grenade = def; break;
                case UsableItemKind.Airstrike: _airstrike = def; break;
                case UsableItemKind.Flashbang: _flashbang = def; break;
            }
        }
        if (_grenade == null) _grenade = UsableItemSO.CreateRuntime("item_grenade", "수류탄", UsableItemKind.Grenade, 5f, 2f, 40f, 0f);
        if (_airstrike == null) _airstrike = UsableItemSO.CreateRuntime("item_airstrike", "폭격지원", UsableItemKind.Airstrike, 12f, 3.5f, 60f, 0f);
        if (_flashbang == null) _flashbang = UsableItemSO.CreateRuntime("item_flashbang", "섬광탄", UsableItemKind.Flashbang, 6f, 3f, 0f, 3f);

        _parts = new List<ItemDefinition>
        {
            ItemDefinition.CreateRuntime("part_mag", "확장 탄창", ItemCategory.GunPart),
            ItemDefinition.CreateRuntime("part_grip", "반동 억제 그립", ItemCategory.GunPart),
            ItemDefinition.CreateRuntime("part_barrel", "롱 배럴", ItemCategory.GunPart),
        };

        foreach (var def in Resources.LoadAll<BulletItemDefinition>("BulletItems"))
        {
            if (def == null) continue;
            if (def.isBasic) _basicBullet = def;
            else _enhancedBullets.Add(def);
        }
        if (_basicBullet == null) _basicBullet = BulletItemDefinition.CreateRuntime("bullet_basic", "기본 탄환", true);

        _gold = Resources.Load<ItemDefinition>("Currency/Gold");
        if (_gold == null) _gold = ItemDefinition.CreateRuntime("gold", "골드", ItemCategory.Currency, maxStack: 9_999_999);
    }

    // ───────────────────────── UI ─────────────────────────

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<StandaloneInputModule>();
        DontDestroyOnLoad(esGO);
    }

    private void BuildCanvas()
    {
        var canvasGO = new GameObject("InventoryDebugCanvas");
        canvasGO.transform.SetParent(transform, false);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 999; // 최상단(디버그).
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(_canvas.transform, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0f, 1f); prt.anchorMax = new Vector2(0f, 1f); prt.pivot = new Vector2(0f, 1f);
        prt.anchoredPosition = new Vector2(24f, -24f);
        panel.GetComponent<Image>().color = new Color(0.04f, 0.055f, 0.086f, 0.95f);
        var vlg = panel.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 4f; vlg.padding = new RectOffset(12, 12, 12, 12); vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false; vlg.childAlignment = TextAnchor.UpperLeft;
        var fit = panel.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _list = prt;

        Header("디버그 인벤토리   ( - 토글 )");
        Btn("＋ 체력 물약", () => InventoryManager.Instance?.Add(_potion, 1));
        Btn("＋ 수류탄", () => InventoryManager.Instance?.Add(_grenade, 1));
        Btn("＋ 폭격지원", () => InventoryManager.Instance?.Add(_airstrike, 1));
        Btn("＋ 섬광탄", () => InventoryManager.Instance?.Add(_flashbang, 1));
        Btn("＋ 총기 파츠 (순차)", AddNextPart);
        Btn("＋ 기본 탄환", () => InventoryManager.Instance?.Add(_basicBullet, 1));
        foreach (var b in _enhancedBullets)
        {
            if (b == null) continue;
            var def = b;
            Btn($"＋ {def.ResolvedName}", () => InventoryManager.Instance?.Add(def, 1));
        }
        Btn("＋ 골드 ×100", () => InventoryManager.Instance?.Add(_gold, 100), UITheme.Gold);
        Btn("전체 비우기", () => InventoryManager.Instance?.Clear(), UITheme.Danger);
    }

    private void AddNextPart()
    {
        var part = _parts[_partCursor % _parts.Count];
        _partCursor++;
        InventoryManager.Instance?.Add(part, 1);
    }

    private void Header(string text)
    {
        var go = new GameObject("Header", typeof(RectTransform));
        go.transform.SetParent(_list, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.font = UITheme.Bold; t.text = text; t.fontSize = 18; t.color = UITheme.Cyan; t.alignment = TextAlignmentOptions.Left;
        go.AddComponent<LayoutElement>().preferredHeight = 28f;
    }

    private void Btn(string label, UnityEngine.Events.UnityAction onClick, Color? accent = null)
    {
        var go = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(_list, false);
        var img = go.GetComponent<Image>(); img.color = new Color(0.10f, 0.15f, 0.21f, 0.95f);
        var btn = go.GetComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(onClick);
        var le = go.AddComponent<LayoutElement>(); le.preferredWidth = 300f; le.preferredHeight = 34f;

        var lbl = new GameObject("Label", typeof(RectTransform));
        lbl.transform.SetParent(go.transform, false);
        var t = lbl.AddComponent<TextMeshProUGUI>();
        t.font = UITheme.Regular; t.text = label; t.fontSize = 16; t.color = accent ?? UITheme.TextHi;
        t.alignment = TextAlignmentOptions.Left;
        var lrt = lbl.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = new Vector2(12, 0); lrt.offsetMax = new Vector2(-8, 0);
    }
}
