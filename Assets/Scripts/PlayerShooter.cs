using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 플레이어의 사격 입력을 담당하는 컴포넌트. 플레이어는 고정 위치에서 마우스로 조준하고
/// 좌클릭으로 발사한다.
///
/// 발사는 "인벤토리 소비형": 인벤토리 Ammo 버킷에서 탄을 1발 꺼내(강화탄 우선, 없으면 기본탄)
/// 그 <see cref="BulletSO"/> 로 실제 총알을 스폰(BulletController.Init)한다. 잔탄 = 인벤토리 보유량.
/// 한 스테이지를 소수(1~5)의 고유 강화 탄환으로 클리어하는 설계와 직결된다.
///
/// 입력은 Legacy Input Manager 기준(Input.mousePosition / Input.GetMouseButtonDown).
///
/// 사격은 "조준 → 호흡 → 격발" 3단계로 진행된다:
/// 1) 조준(Free): 레이저가 마우스를 따라간다. 좌클릭하면 그 방향으로 조준을 고정한다.
/// 2) 호흡(Breath): 조준이 고정되고 레이저가 기준 방향을 중심으로 유기적으로 살짝 흔들린다.
///    이 상태에서 우클릭하면 발사 없이 조준으로 되돌아간다(취소).
/// 3) 격발(Fire): 호흡 상태에서 다시 좌클릭하면 그 순간의 (흔들린) 방향으로 발사하고
///    다시 조준(Free)으로 복귀한다.
///
/// 조준 방향은 LineRenderer 레이저로 항상 표시한다(레이저 사이트). 상태에 따라 색이 다르고,
/// 발사 순간에는
/// 레이저가 잠깐 굵어져 어느 방향으로 쐈는지 눈에 띄게 한다.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class PlayerShooter : MonoBehaviour
{
    [Header("조준/발사 기준")]
    [Tooltip("총알이 나가는 기준점. 비워두면 이 오브젝트의 transform을 사용한다.")]
    [SerializeField] private Transform _firePoint;
    [Tooltip("조준에 사용할 카메라. 비워두면 Camera.main을 사용한다.")]
    [SerializeField] private Camera _cam;

    [Header("탄환")]
    [Tooltip("스폰할 총알 프리팹(BulletController). Assets/Prefabs/BulletPrefab.")]
    [SerializeField] private BulletController _bulletPrefab;
    [Tooltip("스테이지 시작 시 인벤토리에 지급할 탄환 로드아웃(테스트/1~5발 컨셉).")]
    [SerializeField] private List<BulletItemDefinition> _startingBullets = new List<BulletItemDefinition>();

    [Header("조준 레이저")]
    [SerializeField] private Color _laserColor = Color.red;
    [Tooltip("평상시 레이저 두께.")]
    [SerializeField] private float _laserWidth = 0.05f;
    [Tooltip("발사 순간 굵어지는 두께.")]
    [SerializeField] private float _laserFireWidth = 0.15f;
    [Tooltip("발사 강조가 유지되는 시간(초).")]
    [SerializeField] private float _laserFlashTime = 0.12f;

    [Header("조준 가이드라인")]
    [Tooltip("가이드라인 사정거리 상한(월드 유닛). 파츠 사정거리를 합산한 뒤 이 값으로 캡한다.")]
    [SerializeField] private float _guidelineMaxRange = 14f;
    [Tooltip("반사 궤적 예측에 쓸 벽 레이어. 비워두면 탄환 프리팹의 WallLayerMask를 자동으로 가져온다.")]
    [SerializeField] private LayerMask _wallLayerMask;
    [Tooltip("반사 예측 CircleCast 반지름(작을수록 얇은 틈도 통과 예측).")]
    [SerializeField] private float _guidelineRadius = 0.1f;

    [Header("무기 파츠")]
    [Tooltip("장착된 무기 파츠 목록(손잡이/네뷸라이저/레이저/조준경/텅스텐). 인스펙터에서 끼우고 뺀다. 직선 레이저는 파츠와 무관하게 기본 표시된다.")]
    [SerializeField] private List<WeaponPartSO> _equippedParts = new List<WeaponPartSO>();

    [Header("아이템/선택 입력")]
    [Tooltip("탄환 변경 모드 진입 키. 누르면 마우스 휠/좌클릭으로 탄환을 순환한다.")]
    [SerializeField] private KeyCode _bulletChangeKey = KeyCode.Q;
    [Tooltip("아이템 변경 모드 진입 키. 누르면 마우스 휠/좌클릭으로 아이템을 순환한다.")]
    [SerializeField] private KeyCode _itemChangeKey = KeyCode.E;
    [Tooltip("선택 아이템 사용 키. 누르면 조준(범위 표시원) 후 좌클릭으로 타격 확정, 우클릭 취소.")]
    [SerializeField] private KeyCode _itemUseKey = KeyCode.F;

    [Header("호흡(격발 대기)")]
    [Tooltip("호흡 중 조준선이 기준 방향에서 벗어나는 최대 각도(도).")]
    [SerializeField] private float _breathAmplitudeDeg = 2.5f;
    [Tooltip("호흡 흔들림 속도 배율. 클수록 빠르게 흔들린다.")]
    [SerializeField] private float _breathSpeed = 1.2f;
    [Tooltip("호흡(조준 고정) 상태일 때 레이저 색.")]
    [SerializeField] private Color _breathColor = new Color(1f, 0.85f, 0.1f); // 노랑
    [Tooltip("체크 시 호흡 중 카메라 팬/줌도 잠근다(화면 완전 고정).")]
    [SerializeField] private bool _lockCameraDuringBreath = false;
    [Tooltip("카메라 잠금에 사용할 팬 컨트롤러. 비우면 씬에서 자동으로 찾는다.")]
    [SerializeField] private CameraPanController _cameraPan;

    [Header("차징 사격 연출")]
    [Tooltip("조준 고정/발사/취소 시 슬로우모션·확대·PP·흔들림 연출을 담당. 비우면 자동으로 찾는다.")]
    [SerializeField] private ChargeShotEffects _effects;

    /// <summary>발사 방식. LockThenBreath=클릭으로 조준을 고정한 뒤 호흡 흔들림. FollowMouseBreath=호흡(차징) 중에도 조준선이 마우스를 계속 추종.</summary>
    private enum FiringMode { LockThenBreath, FollowMouseBreath }

    [Header("발사 방식")]
    [Tooltip("LockThenBreath=조준→클릭(조준 고정)→호흡 흔들림→격발. FollowMouseBreath=조준→클릭(차징 진입)→마우스 추종 유지→격발. 두 방식 모두 차징 연출은 동일.")]
    [SerializeField] private FiringMode _firingMode = FiringMode.LockThenBreath;

    /// <summary>조준 단계. Free=마우스 추종, Breath=조준 고정+호흡 흔들림(격발 대기).</summary>
    private enum AimPhase { Free, Breath }
    private AimPhase _phase = AimPhase.Free;
    private Vector2 _lockedDir = Vector2.right; // 호흡 중 흔들림의 기준 방향
    private float _breathTime;                  // 호흡 누적 시간(속도 배율 반영)
    private float _breathSeed;                  // Perlin noise 시드(격발마다 달라짐)

    private LineRenderer _laser;
    private float _flashTimer;

    /// <summary>현재 씬에서 활성인 사수. 총알(BulletController)이 헤드샷 보너스/확정 상태를 조회하는 진입점.</summary>
    public static PlayerShooter Active { get; private set; }

    /// <summary>조준경 파츠가 더한 헤드샷 배수 추가 비율(헤드샷 배수 ×(1+이 값)).</summary>
    public float HeadshotMultiplierBonus => _stats.headshotMultiplierBonus;

    /// <summary>텅스텐 탄환 파츠 장착(확정 헤드샷 연쇄) 여부.</summary>
    public bool GuaranteedHeadshotChain => _stats.guaranteedHeadshotChain;

    /// <summary>다음 적 명중을 확정 헤드샷으로 처리할지(텅스텐이 자연 헤드샷 후 예약).</summary>
    private bool _nextHitGuaranteed;

    /// <summary>다음 명중을 확정 헤드샷으로 예약한다(자연 헤드샷 발생 시 텅스텐이 호출).</summary>
    public void ArmGuaranteedHeadshot() => _nextHitGuaranteed = true;

    /// <summary>예약된 확정 헤드샷을 1회 소모한다(예약돼 있었으면 true 반환 후 해제).</summary>
    public bool ConsumeGuaranteedHeadshot()
    {
        bool armed = _nextHitGuaranteed;
        _nextHitGuaranteed = false;
        return armed;
    }

    /// <summary>장착 파츠를 집계한 조준/사격 스탯. <see cref="RecomputeParts"/>로 갱신.</summary>
    private WeaponStats _stats = WeaponStats.Default;
    /// <summary>가이드라인(반사 궤적) 점들을 매 프레임 재사용하는 버퍼.</summary>
    private readonly List<Vector3> _guidePoints = new List<Vector3>();
    /// <summary>UI(O키)로 비활성화한 파츠. 장착 리스트엔 남되 집계에서만 제외한다.</summary>
    private readonly HashSet<WeaponPartSO> _disabledParts = new HashSet<WeaponPartSO>();
    /// <summary>인벤토리/파츠 UI로 게임이 정지(타임스케일 0)된 상태인지.</summary>
    private bool _uiPaused;

    /// <summary>현재 발사 가능한 총 탄환 수(인벤토리 Ammo 버킷 합계).</summary>
    public int RemainingAmmo =>
        InventoryManager.Instance != null
            ? InventoryManager.Instance.Inventory.GetTotalCount(ItemCategory.Ammo)
            : 0;

    // ───────────────────────── 탄환 선택(스위칭) ─────────────────────────

    /// <summary>숫자키로 선택 가능한 최대 탄환 종류 수(1~5).</summary>
    public const int MaxSelectableBullets = 5;

    /// <summary>선택 가능한 한 종류의 탄환(정의 + 보유 수). 같은 종류는 하나로 묶는다.</summary>
    public readonly struct BulletChoice
    {
        public readonly BulletItemDefinition Definition;
        public readonly int Count;
        public BulletChoice(BulletItemDefinition definition, int count)
        {
            Definition = definition;
            Count = count;
        }
    }

    private readonly List<BulletChoice> _choices = new List<BulletChoice>();
    private int _selectedIndex;
    private Inventory _inventory;

    /// <summary>현재 선택된 탄환 슬롯 인덱스(0-based). 숫자키 1이 0번.</summary>
    public int SelectedIndex => _selectedIndex;

    /// <summary>선택 가능한 탄환 종류 목록(읽기 전용, 최대 <see cref="MaxSelectableBullets"/>종).</summary>
    public IReadOnlyList<BulletChoice> Choices => _choices;

    /// <summary>선택이 바뀌거나 탄환 목록이 재구성될 때 발생(HUD 갱신용).</summary>
    public event System.Action SelectionChanged;

    private void Awake()
    {
        if (_firePoint == null) _firePoint = transform;
        if (_cam == null) _cam = Camera.main;
        if (_cameraPan == null) _cameraPan = FindObjectOfType<CameraPanController>();
        if (_effects == null) _effects = GetComponent<ChargeShotEffects>();
        if (_effects == null) _effects = FindObjectOfType<ChargeShotEffects>();

        // 가이드라인 반사 예측이 실제 탄환과 같은 벽 레이어를 쓰도록 탄환 프리팹에서 가져온다.
        if (_wallLayerMask.value == 0 && _bulletPrefab != null)
            _wallLayerMask = _bulletPrefab.WallLayerMask;

        // 조준선 반사 예측이 실제 탄환과 "같은 반지름"으로 튕기도록, 탄환 프리팹 콜라이더에서 반지름을 맞춘다.
        // (콜라이더 크기 차이로 조준선과 실제 탄환의 튕김 위치가 어긋나던 문제 해결)
        if (_bulletPrefab != null)
        {
            var bulletCol = _bulletPrefab.GetComponent<Collider2D>();
            if (bulletCol != null) _guidelineRadius = BulletController.ComputeCollisionRadius(bulletCol);
        }

        // 씬 전환 중 UI가 닫힌 채로 타임스케일이 0에 묶여 있으면 풀어준다(정지 상태로 씬이 시작되는 것 방지).
        if (Time.timeScale == 0f && !InventoryUI.IsOpen && !WeaponPartsUI.IsOpen && !InventoryDebugUI.IsOpen)
            Time.timeScale = 1f;

        SetupLaser();
        RecomputeParts();
    }

    private void OnDisable()
    {
        if (Active == this) Active = null;
    }

    private void OnEnable()
    {
        Active = this;
    }

    /// <summary>인벤토리/파츠 UI가 열리면 게임을 완전히 멈추고(타임스케일 0), 닫히면 재개한다.</summary>
    private void UpdateUiPause(bool uiOpen)
    {
        if (uiOpen && !_uiPaused)
        {
            // 차징(호흡) 중이었다면 연출을 즉시 중단하고 조준 상태를 되돌린 뒤 정지한다.
            // (ForceStop이 타임스케일을 1로 되돌리므로 그 다음에 0으로 세팅해야 한다.)
            if (_phase == AimPhase.Breath) ExitBreath();
            _effects?.ForceStop();

            _uiPaused = true;
            Time.timeScale = 0f;
        }
        else if (!uiOpen && _uiPaused)
        {
            _uiPaused = false;
            Time.timeScale = 1f;
        }
    }

    private void Start()
    {
        // 스테이지 시작 지급 로드아웃을 인벤토리에 넣는다.
        if (InventoryManager.Instance == null) return;
        foreach (var bullet in _startingBullets)
        {
            if (bullet != null) InventoryManager.Instance.Add(bullet, 1);
        }

        // 인벤토리 변경을 구독해 선택 가능한 탄환/아이템 목록을 항상 최신으로 유지한다.
        _inventory = InventoryManager.Instance.Inventory;
        _inventory.Changed += RebuildChoices;
        _inventory.Changed += RebuildItemChoices;
        RebuildChoices();
        RebuildItemChoices();
    }

    private void OnDestroy()
    {
        if (_inventory != null)
        {
            _inventory.Changed -= RebuildChoices;
            _inventory.Changed -= RebuildItemChoices;
        }
    }

    private void Update()
    {
        HandleBulletSelectionInput();

        // 인벤토리/파츠 UI가 열려 있으면 게임을 완전히 멈추고(타임스케일 0) 조준·격발 입력을 막는다.
        bool uiOpen = InventoryUI.IsOpen || WeaponPartsUI.IsOpen || InventoryDebugUI.IsOpen;
        UpdateUiPause(uiOpen);
        if (uiOpen)
        {
            if (_mode != InputMode.Normal) ExitMode();
            return;
        }

        // 준비(Ready) 상태(시작 전)나 스테이지 종료 후에는 조준/격발/아이템 입력을 막는다.
        if (GameManager.Instance != null && !GameManager.Instance.StageStarted)
        {
            if (_laser != null && _laser.enabled) _laser.enabled = false;
            if (_itemIndicator != null && _itemIndicator.enabled) _itemIndicator.enabled = false;
            if (_mode != InputMode.Normal) ExitMode();
            if (_phase == AimPhase.Breath) { ExitBreath(); _effects?.Cancel(); }
            return;
        }

        // 탄환/아이템 변경 모드 또는 아이템 조준 모드가 활성이면, 일반 조준/격발 대신 그 처리를 한다.
        if (HandleItemModes()) return;

        // 화면 버튼(이동/퀵슬롯/실린더) 위를 누른 탭은 조준·격발로 넘기지 않는다.
        bool overUI = IsPointerOverUI();

        if (_phase == AimPhase.Free)
        {
            // 조준: 레이저가 마우스를 따라간다. 좌클릭 시 그 방향으로 조준을 고정(→호흡).
            Vector2 dir = GetAimDirection();
            UpdateLaser(dir, _laserColor);

            if (Input.GetMouseButtonDown(0) && !overUI)
            {
                EnterBreath(dir);
            }
        }
        else // AimPhase.Breath
        {
            // 호흡: 발사 방식에 따라 "기준 방향"만 다르고, 흔들림(호흡)은 둘 다 적용한다.
            // - LockThenBreath: 고정된 기준 방향을 중심으로 흔들린다(저격 호흡).
            // - FollowMouseBreath: 매 프레임 마우스 방향을 기준으로 흔들린다(마우스 추종 + 호흡).
            Vector2 dir = _firingMode == FiringMode.FollowMouseBreath
                ? ApplyBreathSway(GetAimDirection())
                : ComputeBreathDir();
            UpdateLaser(dir, _breathColor);

            if (Input.GetMouseButtonDown(1))
            {
                ExitBreath();     // 우클릭 취소: 발사 없이 조준으로 복귀.
                _effects?.Cancel(); // 연출 원상 복귀(흔들림/오버슈트 없음).
            }
            else if (Input.GetMouseButtonDown(0) && !overUI)
            {
                // 격발: 그 순간의 (흔들린) 방향으로 발사하고 조준으로 복귀.
                bool fired = TryFire(dir);
                ExitBreath();
                // 실제 발사됐으면 발사 연출(흔들림+오버슈트 복귀), 아니면(탄 없음) 그냥 복귀.
                if (fired) _effects?.Fire();
                else _effects?.Cancel();
            }
        }
    }

    /// <summary>조준을 고정하고 호흡(격발 대기) 상태로 진입한다.</summary>
    private void EnterBreath(Vector2 baseDir)
    {
        _lockedDir = baseDir.sqrMagnitude > 0.0001f ? baseDir.normalized : Vector2.right;
        _breathTime = 0f;
        _breathSeed = Random.value * 100f;
        _phase = AimPhase.Breath;

        // 연출(확대)이 카메라 줌과 싸우지 않도록, 연출이 있으면 호흡 중 팬/줌을 잠근다.
        if ((_lockCameraDuringBreath || _effects != null) && _cameraPan != null)
            _cameraPan.ControlsEnabled = false; // 화면 완전 고정

        // 차징 연출 시작: 슬로우모션·확대·PP 상승. (조준선 흔들림은 ComputeBreathDir가 담당)
        _effects?.BeginCharge();
    }

    /// <summary>호흡 상태를 벗어나 조준(Free)으로 복귀한다. 취소·격발 공통 경로.</summary>
    private void ExitBreath()
    {
        _phase = AimPhase.Free;

        if ((_lockCameraDuringBreath || _effects != null) && _cameraPan != null)
            _cameraPan.ControlsEnabled = true; // 카메라 조작 복원
    }

    /// <summary>고정된 기준 방향(_lockedDir)에 유기적 호흡 흔들림을 더한 조준 방향을 계산한다.</summary>
    private Vector2 ComputeBreathDir() => ApplyBreathSway(_lockedDir);

    /// <summary>
    /// 주어진 기준 방향에 유기적 호흡 흔들림(각도 오프셋)을 얹은 조준 방향을 계산한다.
    /// LockThenBreath는 고정된 <see cref="_lockedDir"/>를, FollowMouseBreath는 매 프레임의
    /// 마우스 방향을 기준으로 넘겨 "마우스를 따라오되 흔들리는" 호흡을 구현한다.
    /// </summary>
    private Vector2 ApplyBreathSway(Vector2 baseDir)
    {
        _breathTime += Time.deltaTime * _breathSpeed;
        float t = _breathTime;

        // 여러 주파수의 sine에 Perlin noise를 섞어 규칙적이지 않은 저격 호흡 흔들림을 만든다.
        float sway = Mathf.Sin(t * 1.1f)
                     + 0.5f * Mathf.Sin(t * 2.7f + 1.3f)
                     + (Mathf.PerlinNoise(t * 0.8f, _breathSeed) - 0.5f) * 2f;
        // 세 성분 합의 대략적 최대 크기(1 + 0.5 + 1)로 정규화해 진폭을 각도로 통제.
        // 손잡이 파츠가 있으면 흔들림 진폭을 그만큼 줄인다.
        float amplitude = _breathAmplitudeDeg * _stats.swayMultiplier;
        float offsetDeg = (sway / 2.5f) * amplitude;

        Vector2 b = baseDir.sqrMagnitude > 0.0001f ? baseDir : Vector2.right;
        float baseAngle = Mathf.Atan2(b.y, b.x);
        float angle = baseAngle + offsetDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    // ───────────────────────── 무기 파츠 ─────────────────────────

    /// <summary>장착된(그리고 활성인) 파츠들을 집계해 조준/사격 스탯(_stats)을 다시 계산하고 연출에 반영한다.</summary>
    public void RecomputeParts()
    {
        _stats = WeaponStats.Default;
        foreach (var part in _equippedParts)
        {
            if (part != null && !_disabledParts.Contains(part)) part.Contribute(ref _stats);
        }

        // 네뷸라이저의 슬로우 강화 배율을 차징 연출로 전달.
        _effects?.SetSlowScaleMultiplier(_stats.slowScaleMultiplier);
    }

    /// <summary>장착된 파츠 목록(비활성 포함). UI(O키)에서 나열하는 데 쓴다.</summary>
    public IReadOnlyList<WeaponPartSO> EquippedParts => _equippedParts;

    /// <summary>해당 파츠가 현재 활성(효과 적용) 상태인지.</summary>
    public bool IsPartActive(WeaponPartSO part) => part != null && !_disabledParts.Contains(part);

    /// <summary>파츠의 활성/비활성을 설정한다(장착 리스트에서 빼지 않고 효과만 켜고 끈다).</summary>
    public void SetPartActive(WeaponPartSO part, bool active)
    {
        if (part == null) return;
        bool changed = active ? _disabledParts.Remove(part) : _disabledParts.Add(part);
        if (changed) RecomputeParts();
    }

    /// <summary>파츠의 활성 상태를 토글한다(O키 UI 버튼용).</summary>
    public void TogglePart(WeaponPartSO part) => SetPartActive(part, !IsPartActive(part));

    /// <summary>런타임에 파츠를 장착한다(중복 무시). 이후 인벤토리(GunPart) 연동의 진입점.</summary>
    public void EquipPart(WeaponPartSO part)
    {
        if (part == null || _equippedParts.Contains(part)) return;
        _equippedParts.Add(part);
        RecomputeParts();
    }

    /// <summary>런타임에 파츠를 해제한다.</summary>
    public void UnequipPart(WeaponPartSO part)
    {
        if (_equippedParts.Remove(part))
        {
            _disabledParts.Remove(part);
            RecomputeParts();
        }
    }

    /// <summary>강선 강화 등이 반영된 탄환 대미지 배율(×). BulletController가 명중 시 곱한다.</summary>
    public float DamageMultiplier => _stats.damageMultiplier;

    /// <summary>장착된 강선 파츠를 찾는다(활성/비활성 무관, 없으면 null).</summary>
    private RiflingPartSO FindRifling()
    {
        foreach (var part in _equippedParts)
            if (part is RiflingPartSO rifling) return rifling;
        return null;
    }

    /// <summary>현재 강선 강화도(레벨). 강선이 없으면 0.</summary>
    public int RiflingLevel => FindRifling() is RiflingPartSO r ? r.upgradeLevel : 0;

    /// <summary>강선을 1단계 강화한다(대미지 +25%씩). 강선 파츠가 있어야 동작. 새 레벨을 반환(-1=강선 없음).</summary>
    public int UpgradeRifling()
    {
        var rifling = FindRifling();
        if (rifling == null)
        {
            Debug.LogWarning("[PlayerShooter] 강선 파츠가 장착되어 있지 않아 강화할 수 없습니다.");
            return -1;
        }
        rifling.Upgrade();
        RecomputeParts();
        Debug.Log($"[PlayerShooter] 강선 강화 → +{rifling.upgradeLevel} (대미지 배율 ×{_stats.damageMultiplier:0.##})");
        return rifling.upgradeLevel;
    }

    // ───────────────────────── 탄환 선택 입력/목록 ─────────────────────────

    /// <summary>숫자키 1~5로 발사할 탄환 종류를 선택한다.</summary>
    private void HandleBulletSelectionInput()
    {
        for (int i = 0; i < MaxSelectableBullets; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
            {
                SelectBullet(i);
            }
        }
    }

    /// <summary>탄환 슬롯을 선택한다(범위를 벗어나면 무시). HUD에서도 호출 가능.</summary>
    public void SelectBullet(int index)
    {
        if (index < 0 || index >= _choices.Count) return;
        if (index == _selectedIndex) return;

        _selectedIndex = index;
        SelectionChanged?.Invoke();
        Debug.Log($"[PlayerShooter] 탄환 선택 → [{index + 1}] {_choices[index].Definition.ResolvedName}");
    }

    /// <summary>인벤토리 Ammo 버킷을 종류별로 묶어 선택 목록(최대 5종)을 다시 만든다.</summary>
    private void RebuildChoices()
    {
        _choices.Clear();

        if (_inventory != null)
        {
            var entries = _inventory.GetEntries(ItemCategory.Ammo);
            foreach (var entry in entries)
            {
                if (entry.Quantity <= 0) continue;
                if (!(entry.Definition is BulletItemDefinition bullet)) continue;

                int idx = FindChoiceIndex(bullet);
                if (idx >= 0)
                {
                    // 같은 종류는 수량을 합친다(기본탄 스택 + 동일 id 강화탄).
                    _choices[idx] = new BulletChoice(_choices[idx].Definition, _choices[idx].Count + entry.Quantity);
                }
                else if (_choices.Count < MaxSelectableBullets)
                {
                    _choices.Add(new BulletChoice(bullet, entry.Quantity));
                }
            }
        }

        // 선택 인덱스를 유효 범위로 보정.
        if (_selectedIndex >= _choices.Count) _selectedIndex = Mathf.Max(0, _choices.Count - 1);

        SelectionChanged?.Invoke();
    }

    /// <summary>선택 목록에서 같은 종류(참조 또는 id 일치)의 인덱스를 찾는다. 없으면 -1.</summary>
    private int FindChoiceIndex(BulletItemDefinition bullet)
    {
        for (int i = 0; i < _choices.Count; i++)
        {
            var def = _choices[i].Definition;
            if (def == bullet) return i;
            if (def != null && !string.IsNullOrEmpty(def.id) && def.id == bullet.id) return i;
        }
        return -1;
    }

    /// <summary>현재 선택된 탄을 반환한다. 선택 목록이 비어 있으면 기존 규칙으로 폴백.</summary>
    private BulletItemDefinition ResolveSelectedBullet()
    {
        if (_selectedIndex >= 0 && _selectedIndex < _choices.Count)
            return _choices[_selectedIndex].Definition;
        return ResolveNextBullet();
    }

    /// <summary>발사를 시도한다. 인벤토리에 탄환이 없으면 아무것도 하지 않는다. 실제 발사했으면 true.</summary>
    private bool TryFire(Vector2 dir)
    {
        // 숫자키로 선택한 탄을 쏜다(선택 목록이 없으면 강화탄 우선 규칙으로 폴백).
        BulletItemDefinition ammo = ResolveSelectedBullet();
        if (ammo == null)
        {
            Debug.Log("[PlayerShooter] 탄환 없음 - 발사 불가");
            return false;
        }

        BulletSO data = ammo.bulletData;
        if (data == null)
        {
            Debug.LogWarning($"[PlayerShooter] '{ammo.ResolvedName}' 에 BulletSO(bulletData)가 연결되지 않아 발사할 수 없습니다.");
            return false;
        }

        _flashTimer = _laserFlashTime; // 발사 방향 강조
        GameManager.Instance?.RegisterShot();

        FireBullet(data, dir);

        // 발사한 탄을 인벤토리에서 1발 소비.
        InventoryManager.Instance.Remove(ammo, 1);

        Debug.Log($"[PlayerShooter] 발사! {ammo.ResolvedName}({data.name}) 방향={dir}, 남은 탄환={RemainingAmmo}");
        return true;
    }

    /// <summary>인벤토리 Ammo 버킷에서 다음에 쏠 탄을 고른다(강화탄 우선, 없으면 기본탄).</summary>
    private BulletItemDefinition ResolveNextBullet()
    {
        if (InventoryManager.Instance == null) return null;

        var entries = InventoryManager.Instance.Inventory.GetEntries(ItemCategory.Ammo);
        BulletItemDefinition basic = null;

        foreach (var entry in entries)
        {
            if (entry.Quantity <= 0) continue;
            if (entry.Definition is BulletItemDefinition bullet)
            {
                if (!bullet.isBasic) return bullet; // 강화탄 우선.
                if (basic == null) basic = bullet;
            }
        }

        return basic; // 강화탄이 없으면 기본탄(없으면 null).
    }

    // ───────────────────────── 아이템 선택/사용 ─────────────────────────

    /// <summary>선택 가능한 한 종류의 사용 아이템(정의 + 보유 수).</summary>
    public readonly struct ItemChoice
    {
        public readonly UsableItemSO Definition;
        public readonly int Count;
        public ItemChoice(UsableItemSO definition, int count)
        {
            Definition = definition;
            Count = count;
        }
    }

    /// <summary>입력 모드. Normal=일반 조준/격발, BulletChange/ItemChange=휠·클릭 순환, ItemAim=아이템 조준.</summary>
    private enum InputMode { Normal, BulletChange, ItemChange, ItemAim }
    private InputMode _mode = InputMode.Normal;

    private readonly List<ItemChoice> _itemChoices = new List<ItemChoice>();
    private int _selectedItemIndex;

    /// <summary>선택 가능한 사용 아이템 목록(읽기 전용).</summary>
    public IReadOnlyList<ItemChoice> ItemChoices => _itemChoices;
    /// <summary>현재 선택된 아이템 슬롯 인덱스(0-based).</summary>
    public int SelectedItemIndex => _selectedItemIndex;
    /// <summary>아이템 선택/목록이 바뀔 때 발생(HUD 갱신용).</summary>
    public event System.Action ItemSelectionChanged;

    /// <summary>현재 활성 입력 모드의 한글 라벨(HUD 표시용). Normal이면 빈 문자열.</summary>
    public string ActiveModeLabel
    {
        get
        {
            switch (_mode)
            {
                case InputMode.BulletChange: return "탄환 변경 (휠/클릭)";
                case InputMode.ItemChange: return "아이템 변경 (휠/클릭)";
                case InputMode.ItemAim: return "아이템 조준 (좌클릭 사용 / 우클릭 취소)";
                default: return string.Empty;
            }
        }
    }

    private LineRenderer _itemIndicator; // 아이템 조준 시 효과 반경을 보여주는 원.
    private Vector2 _itemAimPoint;
    /// <summary>조준 모드에 들어간 프레임. 진입을 유발한 그 클릭이 곧바로 "확정"으로 먹히는 것을 막는다.</summary>
    private int _itemAimEnteredFrame = -1;

    /// <summary>탄환/아이템 변경·조준 모드를 처리한다. 활성이면 true(일반 조준/격발을 대체).</summary>
    private bool HandleItemModes()
    {
        // 모드 진입은 일반 조준(Free) 상태에서만. 진입한 프레임에는 즉시 반환해,
        // 같은 프레임의 GetKeyDown(같은 키)이 아래 종료 조건(WantsExitChange)에 걸려
        // 곧바로 빠져나오는(=모드가 유지되지 않는) 문제를 막는다.
        if (_mode == InputMode.Normal && _phase == AimPhase.Free)
        {
            if (Input.GetKeyDown(_bulletChangeKey)) { EnterMode(InputMode.BulletChange); return true; }
            if (Input.GetKeyDown(_itemChangeKey)) { EnterMode(InputMode.ItemChange); return true; }
            if (Input.GetKeyDown(_itemUseKey)) { TryEnterItemAim(); return _mode != InputMode.Normal; }
        }

        switch (_mode)
        {
            case InputMode.BulletChange:
                if (WantsExitChange(_bulletChangeKey)) { ExitMode(); break; }
                int bdir = CycleDir();
                if (bdir != 0) CycleBullet(bdir);
                UpdateLaser(GetAimDirection(), _laserColor); // 조준선은 참고용으로 계속 표시.
                return true;

            case InputMode.ItemChange:
                if (WantsExitChange(_itemChangeKey)) { ExitMode(); break; }
                int idir = CycleDir();
                if (idir != 0) CycleItem(idir);
                UpdateLaser(GetAimDirection(), _laserColor);
                return true;

            case InputMode.ItemAim:
                HandleItemAim();
                return true;
        }

        return false;
    }

    /// <summary>변경 모드 종료 조건(같은 키 다시/ESC/우클릭).</summary>
    private bool WantsExitChange(KeyCode toggleKey) =>
        Input.GetKeyDown(toggleKey) || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1);

    /// <summary>휠 또는 좌클릭으로 순환 방향을 얻는다(+1 다음, -1 이전, 0 없음).</summary>
    private int CycleDir()
    {
        if (Input.GetMouseButtonDown(0)) return +1; // 클릭 = 다음
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0.01f) return +1;
        if (scroll < -0.01f) return -1;
        return 0;
    }

    private void CycleBullet(int dir)
    {
        if (_choices.Count == 0) return;
        int idx = ((_selectedIndex + dir) % _choices.Count + _choices.Count) % _choices.Count;
        SelectBullet(idx);
    }

    private void CycleItem(int dir)
    {
        if (_itemChoices.Count == 0) return;
        int idx = ((_selectedItemIndex + dir) % _itemChoices.Count + _itemChoices.Count) % _itemChoices.Count;
        SelectItem(idx);
    }

    private void EnterMode(InputMode mode)
    {
        _mode = mode;
        // 변경/조준 중엔 카메라 팬·휠 줌을 잠가(휠이 선택 순환에 쓰이므로) 화면을 고정한다.
        if (_cameraPan != null) _cameraPan.ControlsEnabled = false;
    }

    private void ExitMode()
    {
        _mode = InputMode.Normal;
        if (_itemIndicator != null) _itemIndicator.enabled = false;
        if (_cameraPan != null) _cameraPan.ControlsEnabled = true;
    }

    /// <summary>선택된 아이템이 있으면 조준 모드로 진입한다.</summary>
    private void TryEnterItemAim()
    {
        var item = ResolveSelectedItem();
        if (item == null)
        {
            Debug.Log("[PlayerShooter] 사용할 아이템이 없습니다.");
            return;
        }
        EnterMode(InputMode.ItemAim);
        _itemAimEnteredFrame = Time.frameCount;
        if (_laser != null) _laser.enabled = false; // 조준선 숨기고 범위 원만 표시.
    }

    /// <summary>현재 아이템 조준(투척 지점 선택) 중인지. HUD 퀵슬롯이 눌림 상태를 표시하는 데 쓴다.</summary>
    public bool IsAimingItem => _mode == InputMode.ItemAim;

    /// <summary>
    /// HUD 퀵슬롯에서 아이템을 고르고 곧바로 조준 모드로 들어간다.
    /// 이미 같은 슬롯을 조준 중이면 취소(토글)한다. 실제 사용은 이후 월드를 한 번 탭했을 때.
    /// </summary>
    public void UseItemFromHud(int index)
    {
        if (index < 0 || index >= _itemChoices.Count) return;

        // 이미 그 아이템을 조준 중이면 두 번째 탭은 취소로 받는다.
        if (_mode == InputMode.ItemAim && index == _selectedItemIndex) { ExitMode(); return; }

        // 조준/격발 중이거나 스테이지가 아직 안 굴러가면 무시.
        if (InventoryUI.IsOpen || WeaponPartsUI.IsOpen) return;
        if (GameManager.Instance != null && !GameManager.Instance.StageStarted) return;
        if (_phase == AimPhase.Breath) { ExitBreath(); _effects?.Cancel(); }

        SelectItem(index);
        TryEnterItemAim();
    }

    /// <summary>아이템 조준 모드: 범위 원을 마우스(사거리 클램프) 지점에 표시하고, 좌클릭 확정/우클릭 취소.</summary>
    private void HandleItemAim()
    {
        var item = ResolveSelectedItem();
        if (item == null) { ExitMode(); return; }

        Vector2 origin = _firePoint != null ? (Vector2)_firePoint.position : (Vector2)transform.position;
        Vector2 mouse = _cam != null ? (Vector2)_cam.ScreenToWorldPoint(Input.mousePosition) : origin;
        Vector2 to = mouse - origin;
        float dist = to.magnitude;
        if (dist > item.maxRange) mouse = origin + to.normalized * item.maxRange; // 사거리 밖은 클램프.
        _itemAimPoint = mouse;

        UpdateItemIndicator(_itemAimPoint, item.effectRadius, ColorForItem(item.kind));

        if (Input.GetMouseButtonDown(1)) { ExitMode(); return; }        // 취소

        // 확정. 단, 조준을 시작시킨 그 클릭(HUD 퀵슬롯 탭)과 HUD 위를 누른 클릭은 흘려보낸다 —
        // 그러지 않으면 슬롯을 누르자마자 발밑에 터지거나, 다른 버튼을 누를 수 없다.
        if (Time.frameCount == _itemAimEnteredFrame) return;
        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI()) { UseItemAt(item, _itemAimPoint); ExitMode(); }
    }

    /// <summary>아이템 효과를 지점에 적용하고 1개 소비한다.</summary>
    private void UseItemAt(UsableItemSO item, Vector2 point)
    {
        LayerMask enemyMask = _bulletPrefab != null ? _bulletPrefab.EnemyLayerMask : ~0;
        var hits = Physics2D.OverlapCircleAll(point, item.effectRadius, enemyMask);

        if (item.kind == UsableItemKind.Flashbang)
        {
            int stopped = 0;
            var done = new HashSet<Object>();
            foreach (var h in hits)
            {
                var s = h.GetComponentInParent<ISuppressible>();
                if (s is Object o) { if (!done.Add(o)) continue; }
                if (s != null) { s.ApplySuppression(item.suppressDuration, 1f); stopped++; }
            }
            PlayItemVfx(point);
            Debug.Log($"[PlayerShooter] 섬광탄! {point} 반경 {item.effectRadius} → {stopped}체 {item.suppressDuration}초 저지");
        }
        else
        {
            int damaged = 0;
            var done = new HashSet<Object>();
            foreach (var h in hits)
            {
                var ec = h.GetComponentInParent<EnemyController>();
                if (ec != null)
                {
                    if (!done.Add(ec)) continue;
                    ec.OnBulletHit(item.damage, null);
                    damaged++;
                }
                else
                {
                    var ent = h.GetComponentInParent<Entity>();
                    if (ent != null && done.Add(ent))
                    {
                        BulletDamageDispatcher.ApplyDamage(h, item.damage, item.ResolvedName);
                        damaged++;
                    }
                }
            }
            PlayItemVfx(point);
            Debug.Log($"[PlayerShooter] {item.KindLabel}! {point} 반경 {item.effectRadius} 피해 {item.damage} → {damaged}체 적중");
        }

        InventoryManager.Instance?.Remove(item, 1);
    }

    /// <summary>폭발/섬광 이펙트를 지점에 재생(EffectHandler 있으면).</summary>
    private void PlayItemVfx(Vector2 point)
    {
        var handler = EffectHandler.Instance;
        if (handler == null) return;
        var names = handler.explosionName;
        if (names == null || names.Count == 0) return;
        handler.Play(names[Random.Range(0, names.Count)], point);
    }

    private static Color ColorForItem(UsableItemKind kind)
    {
        switch (kind)
        {
            case UsableItemKind.Grenade: return new Color(1f, 0.55f, 0.1f);   // 주황
            case UsableItemKind.Airstrike: return new Color(1f, 0.25f, 0.2f); // 붉은
            case UsableItemKind.Flashbang: return new Color(0.4f, 0.9f, 1f);  // 하늘
            default: return Color.white;
        }
    }

    private void UpdateItemIndicator(Vector2 center, float radius, Color color)
    {
        if (_itemIndicator == null)
        {
            var go = new GameObject("ItemAimIndicator");
            go.transform.SetParent(transform, false);
            _itemIndicator = go.AddComponent<LineRenderer>();
            _itemIndicator.useWorldSpace = true;
            _itemIndicator.loop = true;
            _itemIndicator.numCornerVertices = 2;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) _itemIndicator.material = new Material(shader);
            _itemIndicator.widthMultiplier = 0.08f;
            _itemIndicator.positionCount = 48;
        }

        _itemIndicator.enabled = true;
        _itemIndicator.startColor = color;
        _itemIndicator.endColor = color;

        int n = _itemIndicator.positionCount;
        for (int i = 0; i < n; i++)
        {
            float a = (i / (float)n) * Mathf.PI * 2f;
            _itemIndicator.SetPosition(i, new Vector3(center.x + Mathf.Cos(a) * radius, center.y + Mathf.Sin(a) * radius, 0f));
        }
    }

    /// <summary>인벤토리 Item 버킷에서 사용 아이템(UsableItemSO)을 종류별로 묶어 선택 목록을 만든다.</summary>
    private void RebuildItemChoices()
    {
        _itemChoices.Clear();

        if (_inventory != null)
        {
            var entries = _inventory.GetEntries(ItemCategory.Item);
            foreach (var entry in entries)
            {
                if (entry.Quantity <= 0) continue;
                if (!(entry.Definition is UsableItemSO usable)) continue;

                int idx = FindItemChoiceIndex(usable);
                if (idx >= 0)
                    _itemChoices[idx] = new ItemChoice(_itemChoices[idx].Definition, _itemChoices[idx].Count + entry.Quantity);
                else
                    _itemChoices.Add(new ItemChoice(usable, entry.Quantity));
            }
        }

        if (_selectedItemIndex >= _itemChoices.Count) _selectedItemIndex = Mathf.Max(0, _itemChoices.Count - 1);
        ItemSelectionChanged?.Invoke();
    }

    private int FindItemChoiceIndex(UsableItemSO item)
    {
        for (int i = 0; i < _itemChoices.Count; i++)
        {
            var def = _itemChoices[i].Definition;
            if (def == item) return i;
            if (def != null && !string.IsNullOrEmpty(def.id) && def.id == item.id) return i;
        }
        return -1;
    }

    /// <summary>아이템 슬롯을 선택한다(범위를 벗어나면 무시). HUD에서도 호출 가능.</summary>
    public void SelectItem(int index)
    {
        if (index < 0 || index >= _itemChoices.Count) return;
        if (index == _selectedItemIndex) return;
        _selectedItemIndex = index;
        ItemSelectionChanged?.Invoke();
        Debug.Log($"[PlayerShooter] 아이템 선택 → [{index + 1}] {_itemChoices[index].Definition.ResolvedName}");
    }

    /// <summary>현재 선택된 사용 아이템을 반환한다(없으면 null).</summary>
    private UsableItemSO ResolveSelectedItem()
    {
        if (_selectedItemIndex >= 0 && _selectedItemIndex < _itemChoices.Count)
            return _itemChoices[_selectedItemIndex].Definition;
        return null;
    }

    /// <summary>
    /// 지금 포인터가 HUD 위젯 위에 있는지. 이 컴포넌트는 EventSystem이 아니라 raw Input을 읽기 때문에,
    /// 화면 버튼(이동 화살표·퀵슬롯·실린더)을 눌렀을 때 그 탭이 조준/격발로도 새는 것을 여기서 막는다.
    /// </summary>
    private static bool IsPointerOverUI()
    {
        var es = EventSystem.current;
        if (es == null) return false;
        if (es.IsPointerOverGameObject()) return true;
        // 터치 입력은 포인터 ID가 손가락 인덱스라 별도로 확인해야 한다.
        for (int i = 0; i < Input.touchCount; i++)
            if (es.IsPointerOverGameObject(Input.GetTouch(i).fingerId)) return true;
        return false;
    }

    /// <summary>마우스 위치를 기준으로 발사 방향(정규화)을 계산한다.</summary>
    private Vector2 GetAimDirection()
    {
        if (_cam == null)
        {
            Debug.LogWarning("[PlayerShooter] 카메라가 없어 오른쪽(Vector2.right)으로 조준합니다.");
            return Vector2.right;
        }

        Vector2 mouseWorld = _cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 origin = _firePoint != null ? (Vector2)_firePoint.position : (Vector2)transform.position;
        Vector2 dir = mouseWorld - origin;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
    }

    /// <summary>조준 레이저(LineRenderer)를 초기 구성한다.</summary>
    private void SetupLaser()
    {
        _laser = GetComponent<LineRenderer>();
        if (_laser == null) _laser = gameObject.AddComponent<LineRenderer>();

        // URP에서도 색이 나오도록 스프라이트 셰이더 사용 (없으면 매젠타 방지).
        var shader = Shader.Find("Sprites/Default");
        if (shader != null) _laser.material = new Material(shader);

        _laser.positionCount = 2;
        _laser.useWorldSpace = true;
        _laser.numCapVertices = 2;
        _laser.startWidth = _laserWidth;
        _laser.endWidth = _laserWidth;

        _laser.startColor = _laserColor;
        Color endColor = _laserColor;
        endColor.a = 0.15f; // 끝으로 갈수록 옅어지는 레이저 느낌
        _laser.endColor = endColor;
    }

    /// <summary>매 프레임 조준 방향을 따라 가이드라인(위치/두께/색)을 갱신한다.</summary>
    private void UpdateLaser(Vector2 dir, Color color)
    {
        if (_laser == null) return;

        // 직선 레이저포인터는 기본 상시 표시(파츠 불필요). 레이저 파츠를 끼우면 사거리/반사가 추가된다.
        if (!_laser.enabled) _laser.enabled = true;

        Vector2 origin = _firePoint != null ? (Vector2)_firePoint.position : (Vector2)transform.position;

        // 반사 예측기 파츠가 없으면 반사를 끄고(직선), 사정거리도 직선 파츠분만 쓴다.
        // 있으면 반사 횟수/예측 사정거리를 더한다.
        int bounces = _stats.reflectionEnabled ? _stats.predictBounces : 0;

        // 사정거리 = 기본(직선) + 반사 예측 추가분(반사 켜졌을 때만) → 상한(_guidelineMaxRange)으로 캡.
        // 파츠에 거리 설정이 없으면 상한을 그대로 사용한다.
        float range = _stats.laserRange + (_stats.reflectionEnabled ? _stats.predictRange : 0f);
        if (range <= 0.01f) range = _guidelineMaxRange;
        range = Mathf.Min(range, _guidelineMaxRange);

        BuildGuidelinePoints(origin, dir, range, bounces);
        _laser.positionCount = _guidePoints.Count;
        for (int i = 0; i < _guidePoints.Count; i++)
            _laser.SetPosition(i, _guidePoints[i]);

        // 상태에 따라 색을 바꾼다(자유 조준=빨강, 호흡=지정색). 끝으로 갈수록 옅어지는 느낌 유지.
        _laser.startColor = color;
        Color endColor = color;
        endColor.a = 0.15f;
        _laser.endColor = endColor;

        // 발사 순간에는 굵게, 시간이 지나면 평상시 두께로 복귀.
        float width = _laserWidth;
        if (_flashTimer > 0f)
        {
            _flashTimer -= Time.deltaTime;
            width = _laserFireWidth;
        }
        _laser.startWidth = width;
        _laser.endWidth = width;
    }

    /// <summary>
    /// 조준 방향에서 시작해 벽(<see cref="_wallLayerMask"/>) 반사를 <paramref name="maxBounces"/>회까지
    /// 예측하며, 총 이동 거리가 <paramref name="maxRange"/>를 넘지 않는 꺾인 경로 점들을 만든다.
    /// 실제 탄환(BulletController)과 같은 CircleCast + Vector2.Reflect 규칙을 사용한다.
    /// maxBounces가 0이면 첫 벽에서 멈추는 단순 직선 가이드가 된다.
    /// 결과는 재사용 버퍼 <see cref="_guidePoints"/>에 채운다.
    /// </summary>
    private void BuildGuidelinePoints(Vector2 origin, Vector2 dir, float maxRange, int maxBounces)
    {
        _guidePoints.Clear();
        _guidePoints.Add(origin);

        // 선택 탄환 정보를 반영해 "실제 탄환과 동일한" 반응(튕김/관통/파괴 + 바운스 한도)을 예측한다.
        BulletSO bullet = ResolveSelectedBullet()?.bulletData;
        bool hasArmorPiercing = bullet != null && bullet.HasEffect<ArmorPiercingEffectSO>();
        // 반사 예측 예산 = 파츠 예측치와 실제 탄환의 최대 튕김 수 중 작은 값(둘 다 넘지 않게).
        int bounceBudget = bullet != null ? Mathf.Min(maxBounces, bullet.maxBounceCount) : maxBounces;

        Vector2 pos = origin;
        Vector2 d = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
        float remaining = maxRange;
        int bounces = 0;
        int guard = 0; // 관통 반복 등으로 인한 무한루프 방지

        while (remaining > 0.001f && guard++ < 64)
        {
            RaycastHit2D hit = Physics2D.CircleCast(pos, _guidelineRadius, d, remaining, _wallLayerMask);
            if (hit.collider == null)
            {
                _guidePoints.Add(pos + d * remaining); // 벽 없음 → 남은 사거리만큼 직진.
                break;
            }

            Vector2 hitPos = pos + d * hit.distance;
            BulletTargetType type = BulletController.ResolveTargetType(hit.collider);

            // 민간인: 실제 탄환은 여기서 소멸(스테이지 실패) → 조준선도 멈춘다.
            if (type == BulletTargetType.Civilian)
            {
                _guidePoints.Add(hitPos);
                break;
            }

            BulletHitResult result = BulletController.DetermineHitResult(type, hasArmorPiercing);

            if (result == BulletHitResult.Penetrate)
            {
                // 관통(풀숲/모래·아지랑이, 또는 철갑탄+벽): 반사가 아니므로 방향 유지하고 장애물을 지나 계속.
                float exit = RayBoxExitDistance(hitPos, d, hit.collider.bounds);
                float advance = hit.distance + exit + 0.02f;
                if (advance >= remaining) { _guidePoints.Add(pos + d * remaining); break; }
                pos += d * advance;
                remaining -= advance;
                continue;
            }

            // 튕김/파괴: 맞은 지점을 궤적에 추가.
            _guidePoints.Add(hitPos);
            remaining -= hit.distance;

            if (result == BulletHitResult.Destroy) break; // 파괴형은 여기서 소멸.

            // 튕김: 반사 예산이 남았을 때만 반사(없으면 이 벽에서 멈춤 = 실제 탄환이 소멸/예측 한계).
            if (bounces >= bounceBudget) break;
            d = Vector2.Reflect(d, hit.normal).normalized;
            pos = hitPos + d * 0.02f;
            bounces++;
        }
    }

    /// <summary>점 p에서 방향 d로 나아갈 때 축정렬 박스(bounds)를 빠져나가는 거리(AABB far 교점). 관통 장애물 스킵용.</summary>
    private static float RayBoxExitDistance(Vector2 p, Vector2 d, Bounds b)
    {
        float tExit = float.PositiveInfinity;
        if (Mathf.Abs(d.x) > 1e-6f)
        {
            float t1 = (b.min.x - p.x) / d.x, t2 = (b.max.x - p.x) / d.x;
            tExit = Mathf.Min(tExit, Mathf.Max(t1, t2));
        }
        if (Mathf.Abs(d.y) > 1e-6f)
        {
            float t1 = (b.min.y - p.y) / d.y, t2 = (b.max.y - p.y) / d.y;
            tExit = Mathf.Min(tExit, Mathf.Max(t1, t2));
        }
        return (tExit > 0f && !float.IsInfinity(tExit)) ? tExit : 0f;
    }

    /// <summary>총알 프리팹을 스폰하고 BulletSO로 초기화해 실제로 발사한다.</summary>
    private void FireBullet(BulletSO data, Vector2 dir)
    {
        if (_bulletPrefab == null)
        {
            Debug.LogWarning("[PlayerShooter] _bulletPrefab이 비어 있어 총알을 스폰할 수 없습니다.");
            return;
        }

        Vector2 origin = _firePoint != null ? (Vector2)_firePoint.position : (Vector2)transform.position;
        BulletController bullet = Instantiate(_bulletPrefab, origin, Quaternion.identity);
        bullet.Init(data, dir);
    }
}
