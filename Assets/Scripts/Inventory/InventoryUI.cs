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
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private RectTransform _content;   // 열고 닫힐 때 스케일되는 루트(SafeArea)
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
    // 닫히는 연출 동안에도 "닫힌 것"으로 봐야 입력이 곧바로 풀린다.
    public static bool IsOpen => _instance != null && _instance._open;

    private static readonly ItemCategory[] TabCategory = { ItemCategory.Item /*unused for ALL*/, ItemCategory.Ammo, ItemCategory.GunPart, ItemCategory.Item };
    private Inventory _inventory;
    private PlayerShooter _shooter;
    private int _tab; // 0=ALL,1=Ammo,2=GunPart,3=Item
    private bool _open;
    private int _shownGold = int.MinValue;

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
        if (_group == null && _canvas != null) _group = UIAnim.GroupOf(_canvas);
        _open = _startVisible;
        if (_canvas != null) _canvas.enabled = _startVisible;
        if (_group != null) _group.alpha = _startVisible ? 1f : 0f;
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
        if (Input.GetKeyDown(_toggleKey) && _canvas != null) SetOpen(!_open);
    }

    /// <summary>인벤토리를 연다/닫는다. 열 때는 살짝 커지며 나타나고, 닫을 때는 줄어들며 사라진다.</summary>
    public void SetOpen(bool open)
    {
        if (_open == open || _canvas == null) return;
        _open = open;

        if (open)
        {
            _canvas.enabled = true;
            _canvas.transform.SetAsLastSibling();
            Refresh();
            UIAnim.ShowPopup(_group, _content, UIAnim.Normal, 0.94f);
        }
        else
        {
            UIAnim.HidePopup(_group, _content, UIAnim.Fast, () => { if (_canvas != null) _canvas.enabled = false; });
        }
    }

    // ───────────────────────── 갱신 ─────────────────────────

    private void SetTab(int tab)
    {
        int next = Mathf.Clamp(tab, 0, 3);
        bool changed = next != _tab;
        _tab = next;
        HighlightTabs(changed);
        Refresh();
        if (changed && _tabButtons != null && _tab < _tabButtons.Length && _tabButtons[_tab] != null)
            UIAnim.Punch(_tabButtons[_tab].transform, 0.12f, 0.28f);
    }

    /// <summary>선택된 탭이 스르륵 켜지고 나머지는 스르륵 꺼진다(색이 툭 바뀌지 않게).</summary>
    private void HighlightTabs(bool animate = true)
    {
        if (_tabButtons == null) return;
        for (int i = 0; i < _tabButtons.Length; i++)
        {
            var b = _tabButtons[i];
            if (b == null) continue;
            bool on = i == _tab;
            Color bg = on ? new Color(0.071f,0.188f,0.235f,0.95f) : new Color(0.063f,0.102f,0.149f,0.85f);
            Color fg = on ? UITheme.Cyan : UITheme.TextMid;

            var img = b.GetComponent<Image>();
            var t = b.GetComponentInChildren<TMP_Text>();
            if (animate)
            {
                UIAnim.ColorTo(img, bg, UIAnim.Fast);
                UIAnim.ColorTo(t, fg, UIAnim.Fast);
            }
            else
            {
                if (img != null) img.color = bg;
                if (t != null) t.color = fg;
            }
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
        if (_goldValue == null) return;
        int gold = ShopManager.CurrentGold;
        if (_shownGold == int.MinValue) _goldValue.text = gold.ToString("N0");
        else if (_shownGold != gold) UIAnim.CountTo(_goldValue, _shownGold, gold);
        _shownGold = gold;
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

        // 탭을 바꾸거나 목록이 달라지면 셀이 차례로 자리를 잡는다.
        UIAnim.StaggerIn(_gridContent, UIAnim.StaggerStep * 0.7f, UIAnim.Normal, 0.8f);
    }

    private void RefreshLoadout()
    {
        if (_shooter == null) _shooter = FindObjectOfType<PlayerShooter>();
        bool has = _shooter != null;
        if (_loadoutEmpty != null) _loadoutEmpty.SetActive(!has);

        if (_partsContainer != null && _partChipPrefab != null)
        {
            RefreshParts(has);
            UIAnim.StaggerIn(_partsContainer, UIAnim.StaggerStep, UIAnim.Normal, 0.85f);
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
            UIAnim.StaggerIn(_ammoContainer);
        }
    }

    /// <summary>
    /// 장착 파츠 칩을 "조절 가능한" 상태로 다시 만든다.
    ///
    /// - 파츠 칩: 탭하면 효과가 ON/OFF로 뒤집힌다(장착 목록에서 빼지는 않는다).
    /// - 마지막 칩(강선): 탭할 때마다 한 단계 강화하고 현재 대미지 배율을 함께 보여준다.
    ///
    /// 별도 패널을 만들지 않고 기존 파츠 그리드를 그대로 쓴다 — 칸이 15개까지 들어가서
    /// 장착 파츠(최대 5종) + 강선 칩을 담기에 충분하다.
    /// </summary>
    private void RefreshParts(bool hasShooter)
    {
        ClearChildren(_partsContainer);
        if (!hasShooter) return;

        foreach (var part in _shooter.EquippedParts)
        {
            if (part == null) continue;
            var captured = part;
            var chip = Instantiate(_partChipPrefab, _partsContainer);
            bool active = _shooter.IsPartActive(captured);
            chip.Bind(captured.DisplayName, active, UITheme.Arcane, () =>
            {
                _shooter.TogglePart(captured);
                RefreshLoadout();   // 라벨/색을 즉시 되그린다.
            });
        }

        // 강선 강화 칩. 위쪽 파츠 칩(강선 ON/OFF)과 구분되도록 "강화"라고 못박고,
        // 칩 폭이 좁아 글자가 잘리므로 라벨은 짧게 유지한다.
        // 강선 파츠가 없으면 강화할 수 없으므로 꺼진 칩으로 안내만 한다.
        bool canUpgrade = HasRifling();
        var tune = Instantiate(_partChipPrefab, _partsContainer);
        tune.Bind(canUpgrade ? $"⬆ 강화 +{_shooter.RiflingLevel}" : "⬆ 강선 없음",
                  canUpgrade, UITheme.Gold,
                  canUpgrade ? (System.Action)(() => { _shooter.UpgradeRifling(); RefreshLoadout(); }) : null);
    }

    /// <summary>강선 파츠가 장착되어 있는지(강화도 0이어도 장착돼 있을 수 있다).</summary>
    private bool HasRifling()
    {
        foreach (var part in _shooter.EquippedParts)
            if (part is RiflingPartSO) return true;
        return false;
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
