using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 전체 인벤토리 화면(프리팹 기반, Figma/레퍼런스 네온 스타일).
/// - <see cref="Bootstrap"/>이 첫 씬 로드 후 <c>Resources/UI/InventoryScreen</c> 프리팹을 인스턴스화.
/// - I 키로 열고 닫는다(<see cref="IsOpen"/>은 PlayerShooter가 입력 차단에 사용 — 계약 유지).
/// - 좌측 LOADOUT: 장착 파츠 + 선택 탄환(PlayerShooter). 우측 OWNED: 탭(전체/탄환/파츠/아이템) + 그리드.
///
/// 데이터는 <see cref="InventoryManager"/>의 <see cref="Inventory"/>와 PlayerShooter를 폴링/구독한다(표시 전용).
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [SerializeField] private KeyCode _toggleKey = KeyCode.I;
    [SerializeField] private bool _startVisible = false;

    [Header("루트")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private TMP_Text _goldValue;

    [Header("탭 (0=전체 1=탄환 2=파츠 3=아이템)")]
    [SerializeField] private Button[] _tabButtons;

    [Header("그리드")]
    [SerializeField] private Transform _gridContent;
    [SerializeField] private InventoryCellView _cellPrefab;
    [SerializeField] private GameObject _gridEmpty;

    [Header("로드아웃")]
    [SerializeField] private Transform _partsContainer;
    [SerializeField] private PartChipView _partChipPrefab;
    [SerializeField] private Transform _ammoContainer;
    [SerializeField] private BulletSlotView _ammoRowPrefab;
    [SerializeField] private GameObject _loadoutEmpty;

    private static InventoryUI _instance;
    public static bool IsOpen => _instance != null && _instance._canvas != null && _instance._canvas.enabled;

    private static readonly ItemCategory[] TabCategory = { ItemCategory.Item /*unused for ALL*/, ItemCategory.Ammo, ItemCategory.GunPart, ItemCategory.Item };
    private Inventory _inventory;
    private PlayerShooter _shooter;
    private int _tab; // 0=ALL,1=Ammo,2=GunPart,3=Item

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var prefab = Resources.Load<GameObject>("UI/InventoryScreen");
        if (prefab == null) { Debug.LogError("[InventoryUI] Resources/UI/InventoryScreen 프리팹을 찾을 수 없습니다."); return; }
        Instantiate(prefab);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureEventSystem();

        if (_tabButtons != null)
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                int idx = i;
                if (_tabButtons[i] != null) _tabButtons[i].onClick.AddListener(() => SetTab(idx));
            }
        if (_canvas != null) _canvas.enabled = _startVisible;
    }

    private void Start()
    {
        _inventory = InventoryManager.Instance != null ? InventoryManager.Instance.Inventory : null;
        if (_inventory != null) _inventory.Changed += Refresh;
        SetTab(0);
    }

    private void OnDestroy()
    {
        if (_inventory != null) _inventory.Changed -= Refresh;
    }

    private void Update()
    {
        if (Input.GetKeyDown(_toggleKey) && _canvas != null)
        {
            _canvas.enabled = !_canvas.enabled;
            if (_canvas.enabled) { _canvas.transform.SetAsLastSibling(); Refresh(); }
        }
    }

    // ───────────────────────── 갱신 ─────────────────────────

    private void SetTab(int tab)
    {
        _tab = Mathf.Clamp(tab, 0, 3);
        HighlightTabs();
        Refresh();
    }

    private void HighlightTabs()
    {
        if (_tabButtons == null) return;
        for (int i = 0; i < _tabButtons.Length; i++)
        {
            var b = _tabButtons[i];
            if (b == null) continue;
            bool on = i == _tab;
            var img = b.GetComponent<Image>();
            if (img != null) img.color = on ? new Color(0.071f,0.188f,0.235f,0.95f) : new Color(0.063f,0.102f,0.149f,0.85f);
            var t = b.GetComponentInChildren<TMP_Text>();
            if (t != null) t.color = on ? UITheme.Cyan : UITheme.TextMid;
        }
    }

    public void Refresh()
    {
        RefreshGold();
        RefreshGrid();
        RefreshLoadout();
    }

    private void RefreshGold()
    {
        if (_goldValue != null) _goldValue.text = ShopManager.CurrentGold.ToString("N0");
    }

    private void RefreshGrid()
    {
        if (_gridContent == null || _cellPrefab == null) return;
        var list = new List<(ItemDefinition def, int qty)>();
        if (_inventory != null)
        {
            if (_tab == 0)
            {
                CollectCategory(ItemCategory.Ammo, list);
                CollectCategory(ItemCategory.GunPart, list);
                CollectCategory(ItemCategory.Item, list);
            }
            else CollectCategory(TabCategory[_tab], list);
        }
        PopulateGrid(list);
    }

    private void CollectCategory(ItemCategory c, List<(ItemDefinition, int)> list)
    {
        foreach (var e in _inventory.GetEntries(c))
            if (e.Definition != null && e.Quantity > 0) list.Add((e.Definition, e.Quantity));
    }

    private void PopulateGrid(List<(ItemDefinition def, int qty)> items)
    {
        ClearChildren(_gridContent);
        foreach (var (def, qty) in items)
        {
            var cell = Instantiate(_cellPrefab, _gridContent);
            cell.Set(def, qty);
        }
        if (_gridEmpty != null) _gridEmpty.SetActive(items.Count == 0);
    }

    private void RefreshLoadout()
    {
        if (_shooter == null) _shooter = FindObjectOfType<PlayerShooter>();
        bool has = _shooter != null;
        if (_loadoutEmpty != null) _loadoutEmpty.SetActive(!has);

        if (_partsContainer != null && _partChipPrefab != null)
        {
            ClearChildren(_partsContainer);
            if (has)
                foreach (var p in _shooter.EquippedParts)
                    if (p != null) { var c = Instantiate(_partChipPrefab, _partsContainer); c.Set(p.DisplayName); }
        }
        if (_ammoContainer != null && _ammoRowPrefab != null)
        {
            ClearChildren(_ammoContainer);
            if (has)
            {
                var choices = _shooter.Choices;
                for (int i = 0; i < choices.Count; i++)
                {
                    var row = Instantiate(_ammoRowPrefab, _ammoContainer);
                    row.Set(i + 1, choices[i].Definition, choices[i].Count, i == _shooter.SelectedIndex);
                }
            }
        }
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<StandaloneInputModule>();
        DontDestroyOnLoad(esGO);
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--) Destroy(parent.GetChild(i).gameObject);
    }

    // ───────────────────────── 에디터 프리뷰(테스트 전용) ─────────────────────────

    public void EditorPreview(List<(ItemDefinition def, int qty)> items, IReadOnlyList<WeaponPartSO> parts, IReadOnlyList<PlayerShooter.BulletChoice> ammo, int selectedAmmo)
    {
        RefreshGold();
        _tab = 0; HighlightTabs();
        PopulateGrid(items);
        if (_loadoutEmpty != null) _loadoutEmpty.SetActive(false);
        if (_partsContainer != null && _partChipPrefab != null)
        {
            ClearChildren(_partsContainer);
            if (parts != null) foreach (var p in parts) if (p != null) { var c = Instantiate(_partChipPrefab, _partsContainer); c.Set(p.DisplayName); }
        }
        if (_ammoContainer != null && _ammoRowPrefab != null)
        {
            ClearChildren(_ammoContainer);
            if (ammo != null) for (int i = 0; i < ammo.Count; i++) { var row = Instantiate(_ammoRowPrefab, _ammoContainer); row.Set(i + 1, ammo[i].Definition, ammo[i].Count, i == selectedAmmo); }
        }
    }
}
