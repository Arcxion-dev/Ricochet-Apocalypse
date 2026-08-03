using System.Collections.Generic;
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

    [Header("상단")]
    [SerializeField] private TMP_Text _hpValue;
    [SerializeField] private Image _hpBarFill;      // Image.type = Filled (Horizontal)
    [SerializeField] private TMP_Text _stageInfo;
    [SerializeField] private TMP_Text _combo;
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
        if (_canvas != null) _canvas.enabled = false;
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
        if (_canvas != null && _canvas.enabled != visible) _canvas.enabled = visible;
        if (!visible) return;

        // ── 체력 ──
        if (_player != null)
        {
            _maxHp = Mathf.Max(_maxHp, _player.health, 1);
            if (_player.health != _lastHp) { _lastHp = _player.health; SetHp(_player.health, _maxHp); }
        }

        // ── 콤보 / 골드 / 스테이지·적 ──
        int combo = GameManager.Instance != null ? GameManager.Instance.BestCombo : 0;
        if (combo != _lastCombo) { _lastCombo = combo; SetCombo(combo); }

        int gold = ShopManager.CurrentGold;
        if (gold != _lastGold) { _lastGold = gold; SetGold(gold); }

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
            _partsPanel.SetActive(!_partsPanel.activeSelf);
    }

    // ───────────────────────── 공개 세터(폴링/테스트 공용) ─────────────────────────

    public void SetHp(int current, int max)
    {
        if (_hpValue != null) _hpValue.text = $"{current} / {max}";
        if (_hpBarFill != null) _hpBarFill.fillAmount = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
    }

    public void SetCombo(int combo)
    {
        if (_combo != null) _combo.text = "COMBO ×" + combo;
    }

    public void SetGold(int gold)
    {
        if (_goldValue != null) _goldValue.text = gold.ToString("N0");
    }

    public void SetStage(int stageNumber, int enemiesAlive)
    {
        if (_stageInfo != null) _stageInfo.text = $"STAGE {stageNumber}   ·   ENEMIES {enemiesAlive}";
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
