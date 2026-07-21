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
/// 조준 방향은 LineRenderer 레이저로 항상 표시한다(레이저 사이트). 발사 순간에는
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
        Vector2 dir = GetAimDirection();
        UpdateLaser(dir);

        if (Input.GetMouseButtonDown(0))
        {
            TryFire(dir);
        }
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

    /// <summary>매 프레임 조준 방향을 따라 레이저 위치/두께를 갱신한다.</summary>
    private void UpdateLaser(Vector2 dir)
    {
        if (_laser == null) return;

        Vector2 origin = _firePoint != null ? (Vector2)_firePoint.position : (Vector2)transform.position;
        _laser.SetPosition(0, origin);
        _laser.SetPosition(1, origin + dir * _laserLength);

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
