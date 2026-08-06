using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스테이지 인게임 HUD(세로 1080x1920). 씬에 프리팹 인스턴스로 직접 배치해 쓴다.
///
/// 배치(레퍼런스 기준):
/// <code>
///   ┌──────────────────────────────┐
///   │ [체력바]                 (설정) │
///   │ [골드]                   (인벤) │
///   │            콤보!               │
///   │                                │
///   │                       ◎ 실린더  │  ← 잔탄 + 탄환 선택
///   │ (◀)(▶)            [ ][ ][ ]   │  ← 이동 / 사용 아이템 퀵슬롯
///   └──────────────────────────────┘
/// </code>
///
/// 데이터는 소유하지 않고 <b>폴링</b>만 한다: Player(체력), ShopManager(골드),
/// GameManager(콤보·적 수), PlayerShooter(아이템). 탄환 쪽은 <see cref="RevolverCylinderUI"/>가 직접 본다.
/// 스테이지 밖(= PlayerShooter가 없는 씬)에서는 통째로 숨는다.
///
/// 참조는 전부 프리팹에서 바인딩한다. 코드로 캔버스를 만들지 않는다.
/// </summary>
public class StageHud : MonoBehaviour
{
    [Header("루트")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private CanvasGroup _group;

    [Header("좌상단 · 체력")]
    [SerializeField] private RectTransform _hpPanel;
    [SerializeField] private RectTransform _hpFill;    // anchorMin.x=0 기준으로 폭이 늘어나는 게이지
    [SerializeField] private Image _hpFillImage;       // 피격 시 색 플래시
    [SerializeField] private TMP_Text _hpValue;

    [Header("좌상단 · 골드 / 진행")]
    [SerializeField] private RectTransform _goldPanel;
    [SerializeField] private TMP_Text _goldValue;
    [SerializeField] private TMP_Text _stageInfo;

    [Header("상단 중앙 · 콤보 (배경 없음)")]
    [SerializeField] private TMP_Text _comboValue;
    [SerializeField] private TMP_Text _comboLabel;
    [SerializeField] private CanvasGroup _comboGroup;

    [Header("우상단 · 아이콘 버튼")]
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _inventoryButton;

    [Header("좌하단 · 이동")]
    [SerializeField] private HoldButton _moveLeft;
    [SerializeField] private HoldButton _moveRight;

    [Header("우하단 · 사용 아이템 퀵슬롯")]
    [SerializeField] private QuickSlotView[] _quickSlots;
    [SerializeField] private Sprite _fallbackItemIcon;

    [Header("우하단 · 탄환 실린더")]
    [SerializeField] private RevolverCylinderUI _cylinder;

    [Header("모드 안내")]
    [Tooltip("아이템 조준처럼 특수 입력 모드일 때 화면 하단에 뜨는 안내.")]
    [SerializeField] private GameObject _hintPanel;
    [SerializeField] private TMP_Text _hintText;

    private Player _player;
    private PlayerShooter _shooter;
    private PlayerMovement _movement;

    private float _findTimer;
    private int _maxHp;
    private bool _visible;
    private Color _hpBaseColor = Color.white;

    private int _lastHp = int.MinValue, _lastGold = int.MinValue, _lastCombo = int.MinValue;
    private int _lastStage = int.MinValue, _lastEnemies = int.MinValue;
    private int _itemSignature = int.MinValue;

    private void Awake()
    {
        if (_group == null && _canvas != null) _group = UIAnim.GroupOf(_canvas);
        if (_hpFillImage != null) _hpBaseColor = _hpFillImage.color;

        if (_settingsButton != null) _settingsButton.onClick.AddListener(SettingsUI.Show);
        if (_inventoryButton != null) _inventoryButton.onClick.AddListener(ToggleInventory);

        // 스테이지가 준비될 때까지는 감춰둔다(SetVisible이 첫 프레임에 켜준다).
        if (_canvas != null) _canvas.enabled = false;
        if (_group != null) _group.alpha = 0f;
        _visible = false;
    }

    private void Update()
    {
        AcquireRefs();

        // 전체 화면 창(인벤토리/파츠)이 열려 있으면 HUD를 접는다.
        // 그러지 않으면 반투명 창 뒤로 체력바·아이콘이 비쳐 지저분해진다.
        bool shouldShow = _shooter != null && !InventoryUI.IsOpen && !WeaponPartsUI.IsOpen;
        if (shouldShow != _visible) SetVisible(shouldShow);
        if (!shouldShow)
        {
            // 접히는 동안 이동 버튼이 눌린 채로 남지 않게 입력을 놓아준다.
            if (_movement != null) _movement.ExternalHorizontal = 0f;
            return;
        }

        TickHealth();
        TickGold();
        TickCombo();
        TickStage();
        TickItems();
        TickMovement();
        TickHint();
    }

    private void OnDisable()
    {
        // HUD가 꺼질 때 이동 입력이 눌린 채로 남지 않게 확실히 놓아준다.
        if (_movement != null) _movement.ExternalHorizontal = 0f;
    }

    /// <summary>씬 안의 플레이어/사수를 느슨하게(0.25초마다) 찾는다. 스폰 순서에 의존하지 않기 위함.</summary>
    private void AcquireRefs()
    {
        if (_shooter != null && _player != null && _movement != null) return;

        _findTimer -= Time.unscaledDeltaTime;
        if (_findTimer > 0f) return;
        _findTimer = 0.25f;

        if (_shooter == null) _shooter = PlayerShooter.Active != null ? PlayerShooter.Active : FindObjectOfType<PlayerShooter>();
        if (_player == null)
        {
            _player = FindObjectOfType<Player>();
            if (_player != null) _maxHp = Mathf.Max(_maxHp, _player.health, 1);
        }
        if (_movement == null) _movement = FindObjectOfType<PlayerMovement>();
    }

    private void SetVisible(bool visible)
    {
        _visible = visible;
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

    // ───────────────────────── 각 위젯 갱신 ─────────────────────────

    private void TickHealth()
    {
        if (_player == null) return;
        _maxHp = Mathf.Max(_maxHp, _player.health, 1);
        if (_player.health == _lastHp) return;

        int previous = _lastHp;
        int current = _player.health;
        _lastHp = current;

        if (_hpValue != null)
        {
            if (previous == int.MinValue) _hpValue.text = $"{current} / {_maxHp}";
            else UIAnim.CountTo(_hpValue, previous, current, "", $" / {_maxHp}", "0", UIAnim.Normal);
        }

        float ratio = Mathf.Clamp01((float)current / _maxHp);
        if (_hpFill != null)
        {
            if (previous == int.MinValue)
            {
                _hpFill.anchorMax = new Vector2(ratio, 1f);
                _hpFill.offsetMin = Vector2.zero;
                _hpFill.offsetMax = Vector2.zero;
            }
            else UIAnim.FillRectTo(_hpFill, ratio, UIAnim.Slow);
        }

        if (previous == int.MinValue || previous == current) return;

        bool damaged = current < previous;
        UIAnim.Flash(_hpFillImage, damaged ? UITheme.Danger : UITheme.Success, _hpBaseColor, UIAnim.Slow);
        if (damaged) UIAnim.Shake(_hpPanel, 10f, 0.25f);
    }

    private void TickGold()
    {
        int gold = ShopManager.CurrentGold;
        if (gold == _lastGold) return;

        int previous = _lastGold;
        _lastGold = gold;
        if (_goldValue == null) return;

        if (previous == int.MinValue) { _goldValue.text = gold.ToString("N0"); return; }

        UIAnim.CountTo(_goldValue, previous, gold);
        if (gold > previous)
        {
            UIAnim.Punch(_goldPanel, 0.12f, 0.3f);
            UIAnim.Flash(_goldValue, Color.white, UITheme.GoldText, 0.4f);
        }
    }

    /// <summary>
    /// 콤보는 배경 없이 큰 숫자만 띄운다. 0이면 완전히 사라지고, 오를 때마다 크게 튄다.
    /// 표시 값은 "지금 날아가는 탄이 몇을 잡았나"(CurrentCombo)다.
    /// </summary>
    private void TickCombo()
    {
        int combo = GameManager.Instance != null ? GameManager.Instance.CurrentCombo : 0;
        if (combo == _lastCombo) return;

        int previous = _lastCombo;
        _lastCombo = combo;

        if (_comboGroup != null)
        {
            UIAnim.Stop(_comboGroup);
            _comboGroup.DOFade(combo > 0 ? 1f : 0f, combo > 0 ? UIAnim.Fast : UIAnim.Slow)
                       .SetUpdate(true).SetTarget(_comboGroup).SetLink(gameObject);
        }
        if (combo <= 0) return;

        if (_comboValue != null)
        {
            _comboValue.text = combo.ToString();
            // 콤보가 쌓일수록 더 크게, 더 뜨겁게 튄다.
            float t = Mathf.Clamp01((combo - 1) / 5f);
            _comboValue.color = Color.Lerp(UITheme.Cyan, UITheme.Magenta, t);
            UIAnim.Punch(_comboValue.rectTransform, Mathf.Lerp(0.22f, 0.5f, t), 0.34f);
        }
        if (_comboLabel != null && previous != int.MinValue)
            UIAnim.Punch(_comboLabel.rectTransform, 0.12f, 0.26f);
    }

    private void TickStage()
    {
        if (_stageInfo == null) return;

        int stage = SceneLoader.CurrentStageIndex + 1;
        int enemies = GameManager.Instance != null ? GameManager.Instance.AliveEnemyCount : 0;
        if (stage == _lastStage && enemies == _lastEnemies) return;

        _lastStage = stage;
        _lastEnemies = enemies;
        _stageInfo.text = $"STAGE {stage}   ·   적 {enemies}";
        UIAnim.Punch(_stageInfo.rectTransform, 0.06f, 0.2f);
    }

    private void TickItems()
    {
        if (_quickSlots == null || _quickSlots.Length == 0) return;

        int sig = ItemSignature();
        if (sig == _itemSignature) return;
        _itemSignature = sig;

        var choices = _shooter.ItemChoices;
        for (int i = 0; i < _quickSlots.Length; i++)
        {
            if (_quickSlots[i] == null) continue;
            if (i < choices.Count) _quickSlots[i].Set(_shooter, i, choices[i].Definition, choices[i].Count, _fallbackItemIcon);
            else _quickSlots[i].Set(_shooter, i, null, 0, _fallbackItemIcon);
        }
    }

    private int ItemSignature()
    {
        int h = 19;
        var choices = _shooter.ItemChoices;
        h = h * 31 + choices.Count;
        h = h * 31 + _shooter.SelectedItemIndex;
        h = h * 31 + (_shooter.IsAimingItem ? 1 : 0);
        foreach (var c in choices)
        {
            h = h * 31 + (c.Definition != null ? c.Definition.GetInstanceID() : 0);
            h = h * 31 + c.Count;
        }
        return h;
    }

    /// <summary>좌/우 버튼의 눌림 상태를 한 곳에서 합쳐 이동 입력으로 넘긴다.</summary>
    private void TickMovement()
    {
        if (_movement == null) return;

        float x = 0f;
        if (_moveLeft != null && _moveLeft.IsHeld) x -= 1f;
        if (_moveRight != null && _moveRight.IsHeld) x += 1f;
        _movement.ExternalHorizontal = x;
    }

    private void TickHint()
    {
        bool aiming = _shooter.IsAimingItem;

        if (_quickSlots != null)
            for (int i = 0; i < _quickSlots.Length; i++)
                if (_quickSlots[i] != null) _quickSlots[i].SetArmed(aiming && i == _shooter.SelectedItemIndex);

        string mode = _shooter.ActiveModeLabel;
        bool show = !string.IsNullOrEmpty(mode);
        if (_hintText != null && show) _hintText.text = mode;
        if (_hintPanel != null && _hintPanel.activeSelf != show)
        {
            _hintPanel.SetActive(show);
            if (show) UIAnim.PopIn((RectTransform)_hintPanel.transform);
        }
    }

    /// <summary>우상단 인벤토리 아이콘. 열려 있으면 닫는다.</summary>
    private static void ToggleInventory()
    {
        var inventory = FindObjectOfType<InventoryUI>();
        if (inventory == null) { Debug.LogWarning("[StageHud] InventoryUI를 찾지 못했습니다."); return; }
        inventory.SetOpen(!InventoryUI.IsOpen);
    }
}
