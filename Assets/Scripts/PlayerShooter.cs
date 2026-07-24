using System.Collections.Generic;
using UnityEngine;

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
    [Tooltip("장착된 무기 파츠 목록(손잡이/네뷸라이저/레이저/조준경). 인스펙터에서 끼우고 뺀다. 레이저·조준경이 없으면 가이드라인은 표시되지 않는다.")]
    [SerializeField] private List<WeaponPartSO> _equippedParts = new List<WeaponPartSO>();

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

    /// <summary>조준 단계. Free=마우스 추종, Breath=조준 고정+호흡 흔들림(격발 대기).</summary>
    private enum AimPhase { Free, Breath }
    private AimPhase _phase = AimPhase.Free;
    private Vector2 _lockedDir = Vector2.right; // 호흡 중 흔들림의 기준 방향
    private float _breathTime;                  // 호흡 누적 시간(속도 배율 반영)
    private float _breathSeed;                  // Perlin noise 시드(격발마다 달라짐)

    private LineRenderer _laser;
    private float _flashTimer;

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

        // 씬 전환 중 UI가 닫힌 채로 타임스케일이 0에 묶여 있으면 풀어준다(정지 상태로 씬이 시작되는 것 방지).
        if (Time.timeScale == 0f && !InventoryUI.IsOpen && !WeaponPartsUI.IsOpen)
            Time.timeScale = 1f;

        SetupLaser();
        RecomputeParts();
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

        // 인벤토리 변경을 구독해 선택 가능한 탄환 목록을 항상 최신으로 유지한다.
        _inventory = InventoryManager.Instance.Inventory;
        _inventory.Changed += RebuildChoices;
        RebuildChoices();
    }

    private void OnDestroy()
    {
        if (_inventory != null) _inventory.Changed -= RebuildChoices;
    }

    private void Update()
    {
        HandleBulletSelectionInput();

        // 인벤토리/파츠 UI가 열려 있으면 게임을 완전히 멈추고(타임스케일 0) 조준·격발 입력을 막는다.
        bool uiOpen = InventoryUI.IsOpen || WeaponPartsUI.IsOpen;
        UpdateUiPause(uiOpen);
        if (uiOpen) return;

        if (_phase == AimPhase.Free)
        {
            // 조준: 레이저가 마우스를 따라간다. 좌클릭 시 그 방향으로 조준을 고정(→호흡).
            Vector2 dir = GetAimDirection();
            UpdateLaser(dir, _laserColor);

            if (Input.GetMouseButtonDown(0))
            {
                EnterBreath(dir);
            }
        }
        else // AimPhase.Breath
        {
            // 호흡: 조준이 고정되고 기준 방향을 중심으로 흔들린다.
            Vector2 dir = ComputeBreathDir();
            UpdateLaser(dir, _breathColor);

            if (Input.GetMouseButtonDown(1))
            {
                ExitBreath();     // 우클릭 취소: 발사 없이 조준으로 복귀.
                _effects?.Cancel(); // 연출 원상 복귀(흔들림/오버슈트 없음).
            }
            else if (Input.GetMouseButtonDown(0))
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

    /// <summary>고정된 기준 방향에 유기적 호흡 흔들림을 더한 조준 방향을 계산한다.</summary>
    private Vector2 ComputeBreathDir()
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

        float baseAngle = Mathf.Atan2(_lockedDir.y, _lockedDir.x);
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

        // 레이저/조준경 파츠가 하나도 없으면 가이드라인을 숨긴다("레이저도 파츠" 설계).
        if (!_stats.laserEnabled)
        {
            if (_laser.enabled) _laser.enabled = false;
            return;
        }
        if (!_laser.enabled) _laser.enabled = true;

        Vector2 origin = _firePoint != null ? (Vector2)_firePoint.position : (Vector2)transform.position;

        // 사정거리 = 레이저 + 조준경(합연산) → 상한(_guidelineMaxRange)으로 캡.
        // 파츠에 거리 설정이 없으면 상한을 그대로 사용한다.
        float range = _stats.laserRange + _stats.predictRange;
        if (range <= 0.01f) range = _guidelineMaxRange;
        range = Mathf.Min(range, _guidelineMaxRange);

        BuildGuidelinePoints(origin, dir, range, _stats.predictBounces);
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

        Vector2 pos = origin;
        Vector2 d = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
        float remaining = maxRange;
        int bounces = 0;

        while (remaining > 0.001f)
        {
            RaycastHit2D hit = Physics2D.CircleCast(pos, _guidelineRadius, d, remaining, _wallLayerMask);
            if (hit.collider == null)
            {
                _guidePoints.Add(pos + d * remaining); // 벽 없음 → 남은 사거리만큼 직진.
                break;
            }

            Vector2 hitPos = pos + d * hit.distance;
            _guidePoints.Add(hitPos);
            remaining -= hit.distance;

            if (bounces >= maxBounces) break; // 반사 예산 소진 → 벽에서 멈춤.

            d = Vector2.Reflect(d, hit.normal).normalized;
            pos = hitPos + d * 0.02f; // 방금 맞은 벽을 즉시 재감지하지 않도록 살짝 띄운다.
            bounces++;
        }
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
