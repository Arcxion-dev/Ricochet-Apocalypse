using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 인게임 HUD 통합 컨트롤러(프리팹 기반). 기존 BulletInventoryUI / ItemInventoryUI 싱글턴을 대체하며
/// 체력·콤보·골드·탄환 목록·사용 아이템 슬롯을 한 캔버스에서 관리한다.
///
/// - <see cref="Bootstrap"/>이 첫 씬 로드 후 <c>Resources/UI/HudScreen</c> 프리팹을 인스턴스화(씬 배치 불필요).
/// - 데이터는 소유하지 않고 <b>구독/폴링</b>만 한다: PlayerShooter(탄환·아이템), Player(체력),
///   GameManager(콤보·적 수), ShopManager(골드). 표시 전용.
/// - 스테이지(PlayerShooter 존재) 밖에서는 자동으로 숨는다.
///
/// 참조 필드는 프리팹에서 바인딩된다(코드로 캔버스를 만들지 않는다).
/// </summary>
public class HudController : MonoBehaviour
{
    private static HudController _instance;

    [Header("루트")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private CanvasGroup _group;   // HUD 전체 페이드(스테이지 진입/이탈)

    [Header("상단")]
    [SerializeField] private RectTransform _hpPanel;   // 피격 시 흔들림
    [SerializeField] private TMP_Text _hpValue;
    [SerializeField] private Image _hpBarFill;      // Image.type = Filled (Horizontal)
    [SerializeField] private TMP_Text _stageInfo;
    [SerializeField] private TMP_Text _combo;
    [SerializeField] private RectTransform _goldPanel;  // 골드 증가 시 펀치
    [SerializeField] private TMP_Text _goldValue;

    [Header("아이템 슬롯")]
    [SerializeField] private TMP_Text _itemHint;
    [SerializeField] private Transform _itemContainer;
    [SerializeField] private ItemChipView _itemChipPrefab;

    [Header("장착 파츠")]
    [SerializeField] private GameObject _partsPanel;   // Tab으로 on/off
    [SerializeField] private Transform _partsContainer;
    [SerializeField] private PartChipView _partChipPrefab;
    [SerializeField] private KeyCode _partsToggleKey = KeyCode.Tab;

    [Header("탄환 목록")]
    [SerializeField] private Transform _bulletContainer;
    [SerializeField] private BulletSlotView _bulletRowPrefab;
    [SerializeField] private GameObject _bulletEmptyLabel;

    private PlayerShooter _shooter;
    private Player _player;
    private float _findTimer;
    private int _maxHp;
    private int _bulletSig = int.MinValue, _itemSig = int.MinValue, _partsSig = int.MinValue;
    private int _lastHp = int.MinValue, _lastCombo = int.MinValue, _lastGold = int.MinValue, _lastEnemies = int.MinValue, _lastStage = int.MinValue;
    private int _lastBulletCount = -1, _lastBulletSelected = -1;
    private int _lastItemCount = -1, _lastItemSelected = -1;
    private bool _hudVisible;
    private Color _hpBarBaseColor = Color.white;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var prefab = Resources.Load<GameObject>("UI/HudScreen");
        if (prefab == null) { Debug.LogError("[HudController] Resources/UI/HudScreen 프리팹을 찾을 수 없습니다."); return; }
        Instantiate(prefab);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        if (_group == null && _canvas != null) _group = UIAnim.GroupOf(_canvas);
        if (_hpBarFill != null) _hpBarBaseColor = _hpBarFill.color;
        if (_canvas != null) _canvas.enabled = false;
        if (_group != null) _group.alpha = 0f;
        _hudVisible = false;
    }

    private void Update()
    {
        if (_shooter == null || _player == null)
        {
            _findTimer -= Time.unscaledDeltaTime;
            if (_findTimer <= 0f)
            {
                _findTimer = 0.25f;
                if (_shooter == null) _shooter = FindObjectOfType<PlayerShooter>();
                if (_player == null) { _player = FindObjectOfType<Player>(); if (_player != null) _maxHp = Mathf.Max(_maxHp, _player.health); }
            }
        }

        bool visible = _shooter != null;
        if (visible != _hudVisible) SetHudVisible(visible);
        if (!visible) return;

        // ── 체력 ──
        if (_player != null)
        {
            _maxHp = Mathf.Max(_maxHp, _player.health, 1);
            if (_player.health != _lastHp) SetHp(_player.health, _maxHp); // _lastHp는 SetHp가 갱신(이전 값이 연출에 필요).
        }

        // ── 콤보 / 골드 / 스테이지·적 ──
        int combo = GameManager.Instance != null ? GameManager.Instance.BestCombo : 0;
        if (combo != _lastCombo) { _lastCombo = combo; SetCombo(combo); }

        int gold = ShopManager.CurrentGold;
        if (gold != _lastGold) { SetGold(gold); _lastGold = gold; }

        int stage = SceneLoader.CurrentStageIndex + 1;
        int enemies = GameManager.Instance != null ? GameManager.Instance.AliveEnemyCount : 0;
        if (stage != _lastStage || enemies != _lastEnemies) { _lastStage = stage; _lastEnemies = enemies; SetStage(stage, enemies); }

        // ── 탄환 목록 ──
        int bSig = BulletSignature();
        if (bSig != _bulletSig) { _bulletSig = bSig; RefreshBullets(_shooter.Choices, _shooter.SelectedIndex); }

        // ── 아이템 슬롯 ──
        int iSig = ItemSignature();
        if (iSig != _itemSig) { _itemSig = iSig; RefreshItems(_shooter.ItemChoices, _shooter.SelectedItemIndex); }

        if (_itemHint != null)
        {
            string mode = _shooter.ActiveModeLabel;
            _itemHint.text = string.IsNullOrEmpty(mode) ? "E 변경 · F 사용" : mode;
        }

        // ── 장착 파츠 ──
        int pSig = PartsSignature();
        if (pSig != _partsSig) { _partsSig = pSig; RefreshParts(_shooter.EquippedParts); }
        if (Input.GetKeyDown(_partsToggleKey) && _partsPanel != null)
            TogglePartsPanel();
    }

    // ───────────────────────── 표시/숨김 ─────────────────────────

    /// <summary>스테이지 진입/이탈 시 HUD를 통째로 페이드한다.</summary>
    private void SetHudVisible(bool visible)
    {
        _hudVisible = visible;
        if (_canvas == null) return;

        if (visible)
        {
            _canvas.enabled = true;
            UIAnim.ShowPopup(_group, null, UIAnim.Slow);
        }
        else
        {
            UIAnim.HidePopup(_group, null, UIAnim.Normal, () => { if (_canvas != null) _canvas.enabled = false; });
        }
    }

    /// <summary>파츠 패널을 접었다 폈다(세로로 펼쳐지는 느낌).</summary>
    private void TogglePartsPanel()
    {
        var rt = _partsPanel.transform as RectTransform;
        bool open = !_partsPanel.activeSelf;

        if (open)
        {
            _partsPanel.SetActive(true);
            var g = UIAnim.GroupOf(_partsPanel.transform);
            UIAnim.Stop(rt); UIAnim.Stop(g);
            g.alpha = 0f;
            rt.localScale = new Vector3(1f, 0.8f, 1f);
            g.DOFade(1f, UIAnim.Fast).SetUpdate(true).SetTarget(g).SetLink(_partsPanel);
            rt.DOScale(Vector3.one, UIAnim.Normal).SetEase(UIAnim.EasePop).SetUpdate(true).SetTarget(rt).SetLink(_partsPanel);
        }
        else
        {
            var g = UIAnim.GroupOf(_partsPanel.transform);
            UIAnim.Stop(rt); UIAnim.Stop(g);
            g.DOFade(0f, UIAnim.Fast).SetUpdate(true).SetTarget(g).SetLink(_partsPanel)
             .OnComplete(() => { if (_partsPanel != null) _partsPanel.SetActive(false); });
            rt.DOScale(new Vector3(1f, 0.8f, 1f), UIAnim.Fast).SetEase(UIAnim.EaseOut).SetUpdate(true).SetTarget(rt).SetLink(_partsPanel);
        }
    }

    // ───────────────────────── 공개 세터(폴링/테스트 공용) ─────────────────────────

    public void SetHp(int current, int max)
    {
        int previous = _lastHp;
        _lastHp = current;

        if (_hpValue != null)
        {
            if (previous == int.MinValue) _hpValue.text = $"{current} / {max}";
            else UIAnim.CountTo(_hpValue, previous, current, "", $" / {max}", "0", UIAnim.Normal);
        }

        float ratio = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
        if (_hpBarFill != null)
        {
            if (previous == int.MinValue) _hpBarFill.fillAmount = ratio;
            else UIAnim.FillTo(_hpBarFill, ratio, UIAnim.Slow);   // 게이지는 컴포넌트를 키로 쓴다.
        }

        if (previous == int.MinValue || previous == current) return;

        // 피격이면 붉게 튀기고 패널을 흔든다. 회복이면 초록으로 한 번 밝힌다.
        // 색은 GameObject를 키로 쓰므로 위 fillAmount 트윈과 서로 죽이지 않는다.
        bool damaged = current < previous;
        UIAnim.Flash(_hpBarFill, damaged ? UITheme.Danger : UITheme.Success, _hpBarBaseColor, UIAnim.Slow);
        if (damaged) UIAnim.Shake(_hpPanel, 10f, 0.25f);
    }

    public void SetCombo(int combo)
    {
        if (_combo == null) return;
        _combo.text = "COMBO ×" + combo;

        if (combo <= 0)
        {
            // 콤보가 끊기면 조용히 흐려진다.
            UIAnim.Stop(_combo);
            _combo.DOFade(0.35f, UIAnim.Normal).SetUpdate(true).SetTarget(_combo).SetLink(_combo.gameObject);
            return;
        }

        UIAnim.Stop(_combo);
        _combo.alpha = 1f;
        // 콤보가 쌓일수록 조금 더 크게 튄다.
        UIAnim.Punch(_combo.rectTransform, Mathf.Min(0.14f + combo * 0.02f, 0.34f), 0.3f);
    }

    public void SetGold(int gold)
    {
        if (_goldValue == null) return;
        int previous = _lastGold == int.MinValue ? gold : _lastGold;
        UIAnim.CountTo(_goldValue, previous, gold);
        if (gold > previous)
        {
            UIAnim.Punch(_goldPanel, 0.12f, 0.3f);
            UIAnim.Flash(_goldValue, Color.white, UITheme.GoldText, 0.4f);
        }
    }

    public void SetStage(int stageNumber, int enemiesAlive)
    {
        if (_stageInfo == null) return;
        _stageInfo.text = $"STAGE {stageNumber}   ·   ENEMIES {enemiesAlive}";
        // 적 수가 줄어들 때마다 아주 살짝만 반응한다(너무 시끄럽지 않게).
        UIAnim.Punch(_stageInfo.rectTransform, 0.06f, 0.2f);
    }

    public void RefreshBullets(IReadOnlyList<PlayerShooter.BulletChoice> choices, int selectedIndex)
    {
        if (_bulletContainer == null || _bulletRowPrefab == null) return;
        ClearChildren(_bulletContainer);

        int count = choices != null ? choices.Count : 0;
        for (int i = 0; i < count; i++)
        {
            var c = choices[i];
            var row = Instantiate(_bulletRowPrefab, _bulletContainer);
            row.Set(i + 1, c.Definition, c.Count, i == selectedIndex);
        }
        if (_bulletEmptyLabel != null) _bulletEmptyLabel.SetActive(count == 0);

        // 목록 구성이 바뀌었을 때만 순차 등장. 사격으로 수량만 준 경우엔
        // 매번 다시 밀려들어오면 시끄러우므로 선택된 행만 톡 튕긴다.
        if (count != _lastBulletCount) UIAnim.StaggerIn(_bulletContainer);
        else if (selectedIndex != _lastBulletSelected) UIAnim.PunchChild(_bulletContainer, selectedIndex);
        else UIAnim.PunchChild(_bulletContainer, selectedIndex, 0.07f);

        _lastBulletCount = count;
        _lastBulletSelected = selectedIndex;
    }

    public void RefreshItems(IReadOnlyList<PlayerShooter.ItemChoice> choices, int selectedIndex)
    {
        if (_itemContainer == null || _itemChipPrefab == null) return;
        ClearChildren(_itemContainer);

        int count = choices != null ? choices.Count : 0;
        for (int i = 0; i < count; i++)
        {
            var c = choices[i];
            var chip = Instantiate(_itemChipPrefab, _itemContainer);
            chip.Set(c.Definition, c.Count, i == selectedIndex);
        }

        if (count != _lastItemCount) UIAnim.StaggerIn(_itemContainer);
        else UIAnim.PunchChild(_itemContainer, selectedIndex, selectedIndex != _lastItemSelected ? 0.14f : 0.07f);

        _lastItemCount = count;
        _lastItemSelected = selectedIndex;
    }

    public void RefreshParts(IReadOnlyList<WeaponPartSO> parts)
    {
        if (_partsContainer == null || _partChipPrefab == null) return;
        ClearChildren(_partsContainer);

        int count = parts != null ? parts.Count : 0;
        for (int i = 0; i < count; i++)
        {
            if (parts[i] == null) continue;
            var chip = Instantiate(_partChipPrefab, _partsContainer);
            chip.Set(parts[i].DisplayName);
        }
        UIAnim.StaggerIn(_partsContainer, UIAnim.StaggerStep * 1.5f);
    }

    // ───────────────────────── 내부 ─────────────────────────

    private int BulletSignature()
    {
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

    private int ItemSignature()
    {
        int h = 19;
        h = h * 31 + _shooter.SelectedItemIndex;
        var choices = _shooter.ItemChoices;
        h = h * 31 + choices.Count;
        foreach (var c in choices)
        {
            h = h * 31 + (c.Definition != null ? c.Definition.GetInstanceID() : 0);
            h = h * 31 + c.Count;
        }
        return h;
    }

    private int PartsSignature()
    {
        int h = 23;
        var parts = _shooter.EquippedParts;
        h = h * 31 + (parts != null ? parts.Count : 0);
        if (parts != null) foreach (var p in parts) h = h * 31 + (p != null ? p.GetInstanceID() : 0);
        return h;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }
}
