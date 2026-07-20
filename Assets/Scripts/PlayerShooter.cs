using UnityEngine;

/// <summary>
/// 플레이어의 사격 입력을 담당하는 컴포넌트. 플레이어는 고정 위치에서 마우스로 조준하고
/// 좌클릭으로 발사한다.
///
/// 이번 단계에서는 "총알을 실제로 발사하지 않고 Debug.Log로 방향/잔탄만 프린트"한다.
/// 실제 총알 스폰(BulletController.Init)은 총알 담당 팀원이 FireBullet 훅에 연결한다.
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
    [Tooltip("스테이지 시작 시 지급되는 탄환 수 (제한 탄환 컨셉).")]
    [SerializeField] private int _maxAmmo = 5;

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

    private int _ammo;
    private LineRenderer _laser;
    private float _flashTimer;

    public int Ammo => _ammo;
    public int MaxAmmo => _maxAmmo;

    private void Awake()
    {
        if (_firePoint == null) _firePoint = transform;
        if (_cam == null) _cam = Camera.main;
        _ammo = _maxAmmo;
        SetupLaser();
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

    /// <summary>발사를 시도한다. 탄환이 없으면 아무것도 하지 않는다.</summary>
    private void TryFire(Vector2 dir)
    {
        if (_ammo <= 0)
        {
            Debug.Log("[PlayerShooter] 탄환 없음 - 발사 불가");
            return;
        }

        _flashTimer = _laserFlashTime; // 발사 방향 강조
        _ammo--;
        GameManager.Instance?.RegisterShot();

        // === 이번 단계 핵심: 실제 발사 대신 프린트만 ===
        Debug.Log($"[PlayerShooter] 발사! 방향={dir}, 남은 탄환={_ammo}/{_maxAmmo}");

        FireBullet(dir);
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

    /// <summary>
    /// 실제 총알을 생성/발사할 진입점. 지금은 비어 있는 훅이다.
    /// TODO: 총알 담당자 연결 지점 — 총알 프리팹 Instantiate 후 BulletController.Init(bulletSO, dir) 호출.
    /// </summary>
    private void FireBullet(Vector2 dir)
    {
        // 의도적으로 비워둠. 총알 시스템(BulletController/BulletSO) 연결 시 여기서 스폰한다.
    }

    /// <summary>탄환을 재충전한다 (상점/스테이지 시작 시 사용 예정).</summary>
    public void Refill(int amount)
    {
        _ammo = Mathf.Clamp(_ammo + amount, 0, _maxAmmo);
    }
}
