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
    [Tooltip("레이저 길이 (월드 유닛).")]
    [SerializeField] private float _laserLength = 20f;
    [SerializeField] private Color _laserColor = Color.red;
    [Tooltip("평상시 레이저 두께.")]
    [SerializeField] private float _laserWidth = 0.05f;
    [Tooltip("발사 순간 굵어지는 두께.")]
    [SerializeField] private float _laserFireWidth = 0.15f;
    [Tooltip("발사 강조가 유지되는 시간(초).")]
    [SerializeField] private float _laserFlashTime = 0.12f;

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

    /// <summary>조준 단계. Free=마우스 추종, Breath=조준 고정+호흡 흔들림(격발 대기).</summary>
    private enum AimPhase { Free, Breath }
    private AimPhase _phase = AimPhase.Free;
    private Vector2 _lockedDir = Vector2.right; // 호흡 중 흔들림의 기준 방향
    private float _breathTime;                  // 호흡 누적 시간(속도 배율 반영)
    private float _breathSeed;                  // Perlin noise 시드(격발마다 달라짐)

    private LineRenderer _laser;
    private float _flashTimer;

    /// <summary>현재 발사 가능한 총 탄환 수(인벤토리 Ammo 버킷 합계).</summary>
    public int RemainingAmmo =>
        InventoryManager.Instance != null
            ? InventoryManager.Instance.Inventory.GetTotalCount(ItemCategory.Ammo)
            : 0;

    private void Awake()
    {
        if (_firePoint == null) _firePoint = transform;
        if (_cam == null) _cam = Camera.main;
        if (_cameraPan == null) _cameraPan = FindObjectOfType<CameraPanController>();
        SetupLaser();
    }

    private void Start()
    {
        // 스테이지 시작 지급 로드아웃을 인벤토리에 넣는다.
        if (InventoryManager.Instance == null) return;
        foreach (var bullet in _startingBullets)
        {
            if (bullet != null) InventoryManager.Instance.Add(bullet, 1);
        }
    }

    private void Update()
    {
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
                ExitBreath(); // 우클릭 취소: 발사 없이 조준으로 복귀.
            }
            else if (Input.GetMouseButtonDown(0))
            {
                // 격발: 그 순간의 (흔들린) 방향으로 발사하고 조준으로 복귀.
                TryFire(dir);
                ExitBreath();
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

        if (_lockCameraDuringBreath && _cameraPan != null)
            _cameraPan.ControlsEnabled = false; // 화면 완전 고정
    }

    /// <summary>호흡 상태를 벗어나 조준(Free)으로 복귀한다. 취소·격발 공통 경로.</summary>
    private void ExitBreath()
    {
        _phase = AimPhase.Free;

        if (_lockCameraDuringBreath && _cameraPan != null)
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
        float offsetDeg = (sway / 2.5f) * _breathAmplitudeDeg;

        float baseAngle = Mathf.Atan2(_lockedDir.y, _lockedDir.x);
        float angle = baseAngle + offsetDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    /// <summary>발사를 시도한다. 인벤토리에 탄환이 없으면 아무것도 하지 않는다.</summary>
    private void TryFire(Vector2 dir)
    {
        // 인벤토리에서 쏠 탄을 고른다(강화탄 우선, 없으면 기본탄).
        BulletItemDefinition ammo = ResolveNextBullet();
        if (ammo == null)
        {
            Debug.Log("[PlayerShooter] 탄환 없음 - 발사 불가");
            return;
        }

        BulletSO data = ammo.bulletData;
        if (data == null)
        {
            Debug.LogWarning($"[PlayerShooter] '{ammo.ResolvedName}' 에 BulletSO(bulletData)가 연결되지 않아 발사할 수 없습니다.");
            return;
        }

        _flashTimer = _laserFlashTime; // 발사 방향 강조
        GameManager.Instance?.RegisterShot();

        FireBullet(data, dir);

        // 발사한 탄을 인벤토리에서 1발 소비.
        InventoryManager.Instance.Remove(ammo, 1);

        Debug.Log($"[PlayerShooter] 발사! {ammo.ResolvedName}({data.name}) 방향={dir}, 남은 탄환={RemainingAmmo}");
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

    /// <summary>매 프레임 조준 방향을 따라 레이저 위치/두께/색을 갱신한다.</summary>
    private void UpdateLaser(Vector2 dir, Color color)
    {
        if (_laser == null) return;

        Vector2 origin = _firePoint != null ? (Vector2)_firePoint.position : (Vector2)transform.position;
        _laser.SetPosition(0, origin);
        _laser.SetPosition(1, origin + dir * _laserLength);

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
