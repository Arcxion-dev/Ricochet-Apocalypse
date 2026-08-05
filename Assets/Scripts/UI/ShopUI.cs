using System;
using System.Collections.Generic;
using DG.Tweening;
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
    [SerializeField] private RectTransform _goldPanel;
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

    [Header("등장 연출")]
    [Tooltip("씬 진입 시 순서대로 떠오르는 패널들(구매/조합/출발 버튼 순).")]
    [SerializeField] private RectTransform[] _entrancePanels;

    private Inventory _inventory;
    private BulletItemDefinition _selectedA;
    private BulletItemDefinition _selectedB;
    private int _shownGold = int.MinValue;
    private int _catalogCount = -1, _candidateCount = -1;
    private int _pendingCandidatePunch = -1;   // 클릭한 행은 재생성 후 그 자리만 튕겨 준다.
    private bool _resultReady;

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
        PlayEntrance();
    }

    /// <summary>상점에 들어서면 패널이 아래에서 차례로 떠오른다.</summary>
    private void PlayEntrance()
    {
        if (_entrancePanels == null) return;
        for (int i = 0; i < _entrancePanels.Length; i++)
            UIAnim.RiseIn(_entrancePanels[i], i * 0.08f);
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
        if (_goldValue == null) return;
        int gold = ShopManager.CurrentGold;
        if (_shownGold == int.MinValue) { _goldValue.text = gold.ToString("N0"); _shownGold = gold; return; }
        if (_shownGold == gold) return;

        UIAnim.CountTo(_goldValue, _shownGold, gold);
        // 늘면 금색으로 반짝, 줄면(구매) 살짝 붉게 스쳐 지나간다.
        UIAnim.Flash(_goldValue, gold > _shownGold ? Color.white : UITheme.Danger, UITheme.GoldText, 0.45f);
        UIAnim.Punch(_goldPanel, 0.10f, 0.28f);
        _shownGold = gold;
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
            var rowRt = row.transform as RectTransform;
            row.Setup(item, owned, () =>
            {
                if (ShopManager.TryPurchase(captured, 1, out string reason))
                {
                    RefreshAll();
                }
                else
                {
                    // 살 수 없으면 행이 고개를 젓는다.
                    UIAnim.Shake(rowRt, 12f, 0.3f);
                    Debug.Log($"[ShopUI] 구매 실패: {reason}");
                }
            });
        }

        // 목록 구성이 바뀔 때(첫 진입/품목 변화)만 순차 등장시킨다.
        if (catalog.Count != _catalogCount) UIAnim.StaggerIn(_catalogContainer);
        _catalogCount = catalog.Count;
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

        if (candidates.Count != _candidateCount) UIAnim.StaggerIn(_candidateContainer);
        else if (_pendingCandidatePunch >= 0) UIAnim.PunchChild(_candidateContainer, _pendingCandidatePunch, 0.12f);
        _candidateCount = candidates.Count;
        _pendingCandidatePunch = -1;
    }

    private void UpdateSlots()
    {
        if (_slotA != null) _slotA.Set(_selectedA);
        if (_slotB != null) _slotB.Set(_selectedB);

        bool both = _selectedA != null && _selectedB != null;
        if (_resultName != null) _resultName.text = both ? "복합 탄환" : "결과";
        if (_resultTag != null) _resultTag.text = both ? $"{FirstTag(_selectedA)} + {FirstTag(_selectedB)}" : "";
        if (_resultIcon != null) { _resultIcon.enabled = both; _resultIcon.color = UITheme.Magenta; }

        // 두 슬롯이 다 차는 순간 결과 슬롯이 살아난다.
        if (both && !_resultReady && _resultName != null)
        {
            UIAnim.Punch(_resultName.transform.parent, 0.16f, 0.4f);
            if (_resultIcon != null) UIAnim.PopIn(_resultIcon.rectTransform);
        }
        _resultReady = both;

        if (_combineButton != null) _combineButton.interactable = both;
    }

    private void OnCandidateClicked(BulletItemDefinition item)
    {
        var candidates = GetCombineCandidates();
        _pendingCandidatePunch = candidates.IndexOf(item);

        bool wasFull = _selectedA != null && _selectedB != null;
        if (item == _selectedA) _selectedA = null;
        else if (item == _selectedB) _selectedB = null;
        else if (_selectedA == null) _selectedA = item;
        else if (_selectedB == null) _selectedB = item;
        // A,B 둘 다 차 있으면 무시(먼저 해제 필요).

        // 꽉 찬 상태에서 새로 누르면 아무 일도 안 일어난다 → 슬롯이 거절 신호를 보낸다.
        if (wasFull && item != _selectedA && item != _selectedB)
        {
            UIAnim.Shake(_slotA != null ? _slotA.transform as RectTransform : null, 10f, 0.28f);
            UIAnim.Shake(_slotB != null ? _slotB.transform as RectTransform : null, 10f, 0.28f);
        }

        RefreshCandidatesFrom(candidates);
        UpdateSlots();
    }

    private void OnCombineClicked()
    {
        var slotA = _slotA != null ? _slotA.transform as RectTransform : null;
        var slotB = _slotB != null ? _slotB.transform as RectTransform : null;
        var resultRt = _resultName != null ? _resultName.transform.parent as RectTransform : null;

        if (ShopManager.TryCombine(_selectedA, _selectedB, out string reason))
        {
            _selectedA = null;
            _selectedB = null;
            // 재료 슬롯이 쪼그라들고 결과 슬롯이 터져 나온다.
            UIAnim.Punch(slotA, 0.2f, 0.3f);
            UIAnim.Punch(slotB, 0.2f, 0.3f);
            if (resultRt != null) UIAnim.PopIn(resultRt, 0.08f, UIAnim.Normal);
            ShowCombineStatus("✓ 조합 성공!", UITheme.Success);
            RefreshAll();
        }
        else
        {
            UIAnim.Shake(slotA, 12f, 0.3f);
            UIAnim.Shake(slotB, 12f, 0.3f);
            ShowCombineStatus(reason, UITheme.Danger);
        }
    }

    /// <summary>상태 문구를 페이드 인시키고, 잠시 뒤 스스로 흐려지게 한다.</summary>
    private void ShowCombineStatus(string message, Color color)
    {
        if (_combineStatus == null) return;
        _combineStatus.text = message;
        _combineStatus.color = color;

        UIAnim.Stop(_combineStatus);
        _combineStatus.alpha = 0f;
        DOTween.Sequence()
               .Append(_combineStatus.DOFade(1f, UIAnim.Fast))
               .AppendInterval(2.2f)
               .Append(_combineStatus.DOFade(0f, UIAnim.Slow))
               .SetUpdate(true).SetTarget(_combineStatus).SetLink(_combineStatus.gameObject);
        UIAnim.Punch(_combineStatus.rectTransform, 0.12f, 0.3f);
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
