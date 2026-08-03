using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 상점(Shop) 씬 UI. 이제 <b>프리팹 기반</b>이다(Figma 목업 → ShopScreen 프리팹, 씬에 배치).
/// - 좌측: 구매 카탈로그(<see cref="ShopManager.GetCatalog"/>) → <see cref="ShopBuyRowView"/> 동적 생성.
/// - 우측: 탄환 조합(보유 탄환 두 개 선택 ①②) → <see cref="CombineCandidateRowView"/> + 슬롯 프리뷰.
/// - 하단: 다음 스테이지로 출발.
/// 참조는 프리팹에서 바인딩된다(코드로 캔버스를 만들지 않는다).
/// </summary>
public class ShopUI : MonoBehaviour
{
    [Header("골드")]
    [SerializeField] private TMP_Text _goldValue;

    [Header("구매")]
    [SerializeField] private Transform _catalogContainer;
    [SerializeField] private ShopBuyRowView _buyRowPrefab;
    [SerializeField] private GameObject _catalogEmpty;

    [Header("조합 슬롯")]
    [SerializeField] private CombineSlotView _slotA;
    [SerializeField] private CombineSlotView _slotB;
    [SerializeField] private Image _resultIcon;
    [SerializeField] private TMP_Text _resultName;
    [SerializeField] private TMP_Text _resultTag;

    [Header("조합 후보")]
    [SerializeField] private Transform _candidateContainer;
    [SerializeField] private CombineCandidateRowView _candidateRowPrefab;
    [SerializeField] private GameObject _candidateEmpty;
    [SerializeField] private TMP_Text _combineCost;
    [SerializeField] private Button _combineButton;
    [SerializeField] private TMP_Text _combineStatus;

    [Header("출발")]
    [SerializeField] private Button _startButton;

    private Inventory _inventory;
    private BulletItemDefinition _selectedA;
    private BulletItemDefinition _selectedB;

    private void Awake()
    {
        if (_combineCost != null) _combineCost.text = $"비용 {ShopManager.CombineCost} G";
        if (_combineButton != null) _combineButton.onClick.AddListener(OnCombineClicked);
        if (_startButton != null) _startButton.onClick.AddListener(() => SceneLoader.LaunchCurrentStage());
        if (_combineStatus != null) _combineStatus.text = "";
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

    // ───────────────────────── 갱신 ─────────────────────────

    private void RefreshAll()
    {
        RefreshGold();
        RefreshCatalog();
        RefreshCandidatesFrom(GetCombineCandidates());
        UpdateSlots();
    }

    private void RefreshGold()
    {
        if (_goldValue != null) _goldValue.text = ShopManager.CurrentGold.ToString("N0");
    }

    private void RefreshCatalog()
    {
        if (_catalogContainer == null || _buyRowPrefab == null) return;
        ClearChildren(_catalogContainer);

        var catalog = ShopManager.GetCatalog();
        if (_catalogEmpty != null) _catalogEmpty.SetActive(catalog.Count == 0);
        foreach (var item in catalog)
        {
            if (item == null) continue;
            int owned = _inventory != null ? _inventory.GetQuantity(item) : 0;
            var captured = item;
            var row = Instantiate(_buyRowPrefab, _catalogContainer);
            row.Setup(item, owned, () =>
            {
                if (ShopManager.TryPurchase(captured, 1, out string reason)) RefreshAll();
                else Debug.Log($"[ShopUI] 구매 실패: {reason}");
            });
        }
    }

    private void RefreshCandidatesFrom(List<BulletItemDefinition> candidates)
    {
        if (_candidateContainer == null || _candidateRowPrefab == null) return;
        ClearChildren(_candidateContainer);

        if (_selectedA != null && !candidates.Contains(_selectedA)) _selectedA = null;
        if (_selectedB != null && !candidates.Contains(_selectedB)) _selectedB = null;

        if (_candidateEmpty != null) _candidateEmpty.SetActive(candidates.Count == 0);
        foreach (var bullet in candidates)
        {
            int owned = _inventory != null ? _inventory.GetQuantity(bullet) : 1;
            string mark = bullet == _selectedA ? "①" : bullet == _selectedB ? "②" : null;
            var captured = bullet;
            var row = Instantiate(_candidateRowPrefab, _candidateContainer);
            row.Setup(bullet, owned, mark, () => OnCandidateClicked(captured));
        }
    }

    private void UpdateSlots()
    {
        if (_slotA != null) _slotA.Set(_selectedA);
        if (_slotB != null) _slotB.Set(_selectedB);

        bool both = _selectedA != null && _selectedB != null;
        if (_resultName != null) _resultName.text = both ? "복합 탄환" : "결과";
        if (_resultTag != null) _resultTag.text = both ? $"{FirstTag(_selectedA)} + {FirstTag(_selectedB)}" : "";
        if (_resultIcon != null) { _resultIcon.enabled = both; _resultIcon.color = UITheme.Magenta; }
    }

    private void OnCandidateClicked(BulletItemDefinition item)
    {
        if (item == _selectedA) _selectedA = null;
        else if (item == _selectedB) _selectedB = null;
        else if (_selectedA == null) _selectedA = item;
        else if (_selectedB == null) _selectedB = item;
        // A,B 둘 다 차 있으면 무시(먼저 해제 필요).

        RefreshCandidatesFrom(GetCombineCandidates());
        UpdateSlots();
    }

    private void OnCombineClicked()
    {
        if (ShopManager.TryCombine(_selectedA, _selectedB, out string reason))
        {
            _selectedA = null;
            _selectedB = null;
            if (_combineStatus != null) { _combineStatus.text = "✓ 조합 성공!"; _combineStatus.color = UITheme.Success; }
            RefreshAll();
        }
        else if (_combineStatus != null)
        {
            _combineStatus.text = reason;
            _combineStatus.color = UITheme.Danger;
        }
    }

    // ───────────────────────── 조합 후보 수집 ─────────────────────────

    /// <summary>인벤토리 Ammo 중 기본탄이 아닌(효과를 지닌) 탄환을 종류별로 묶어 조합 후보를 만든다.</summary>
    private List<BulletItemDefinition> GetCombineCandidates()
    {
        var result = new List<BulletItemDefinition>();
        if (_inventory == null) return result;

        foreach (var entry in _inventory.GetEntries(ItemCategory.Ammo))
        {
            if (entry.Quantity <= 0) continue;
            if (!(entry.Definition is BulletItemDefinition bullet)) continue;
            if (bullet.isBasic || bullet.bulletData == null) continue;

            bool listed = false;
            foreach (var existing in result)
                if (existing == bullet || existing.id == bullet.id) { listed = true; break; }
            if (!listed) result.Add(bullet);
        }
        return result;
    }

    private static string FirstTag(BulletItemDefinition def)
    {
        var a = def != null ? def.GetAbilityLabels() : null;
        return a != null && a.Count > 0 ? a[0] : "";
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    // ───────────────────────── 에디터 프리뷰(테스트 전용) ─────────────────────────

    /// <summary>인벤토리 없이 목업 확인용. 카탈로그(실데이터)+후보(주어진 목록)+선택 슬롯을 그린다.</summary>
    public void EditorPreview(List<BulletItemDefinition> candidates, BulletItemDefinition a, BulletItemDefinition b)
    {
        RefreshGold();
        RefreshCatalog();
        _selectedA = a; _selectedB = b;
        RefreshCandidatesFrom(candidates);
        UpdateSlots();
    }
}
