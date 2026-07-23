using UnityEngine;

/// <summary>
/// 총알 하나의 런타임 동작을 담당합니다.
/// - 기본적으로 Init() 시 지정된 방향으로 직선 이동합니다.
/// - 벽(Wall 레이어)과 부딪히면 반사(튕김) 처리합니다.
/// - BulletSO에 부착된 모든 BulletEffectSO의 훅을 매 상황마다 호출합니다.
/// - 물리엔진 요소(바람/자력)는 담당 시스템이 별도로 있다고 가정하고,
///   BulletController가 구독할 수 있는 형태의 훅(ApplyExternalForce, Nullify)만 열어둡니다.
///
/// 담당 범위 밖(적 AI, 장애물 파괴/판정, 물리엔진 실제 연산, VFX 실제 스폰)은
/// 전부 인터페이스/이벤트/로그 스텁으로 남겨두었습니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BulletController : MonoBehaviour
{
    [Header("레이어 설정 (담당자 프로젝트에 맞게 조정)")]
    [SerializeField] private LayerMask wallLayerMask;
    [SerializeField] private LayerMask enemyLayerMask;

    public BulletSO Data { get; private set; }
    public Vector2 Direction { get; private set; }
    public Transform Target { get; private set; } // 유도탄 등에서 사용
    public LayerMask EnemyLayerMask => enemyLayerMask;
    public LayerMask WallLayerMask => wallLayerMask;

    /// <summary>화상탄/냉기탄처럼 "최초 1회 적중"을 추적해야 하는 효과들을 위한 공용 플래그.</summary>
    public bool HasTriggeredFirstZoneHit { get; set; }

    /// <summary>분열탄이 자식 총알에 다시 분열 효과를 넣지 않도록 방지하는 플래그.</summary>
    public bool IsSplitChild { get; set; }

    private Rigidbody2D _rb;
    private int _bounceCount;
    private float _elapsedLife;
    private bool _isDead;

    // 물리엔진(바람/자력) 담당 시스템이 외부에서 걸어줄 수 있는 훅.
    // 바람: 매 프레임 힘을 더해주는 방식 / 자력: 총알을 무효화(Nullify)하는 방식.
    private Vector2 _externalForceThisFrame;
    private bool _isNullified;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f; // 탑다운 2D 슈터 기준
    }

    /// <summary>
    /// 총알을 초기화하고 발사합니다.
    /// </summary>
    /// <param name="data">이 총알의 스탯/효과를 정의하는 BulletSO</param>
    /// <param name="direction">발사 방향 (정규화되어 저장됨)</param>
    /// <param name="target">유도탄 등 타겟이 필요한 효과를 위한 선택적 타겟</param>
    public void Init(BulletSO data, Vector2 direction, Transform target = null)
    {
        Data = data;
        Direction = direction.normalized;
        Target = target;

        _bounceCount = 0;
        _elapsedLife = 0f;
        _isDead = false;
        HasTriggeredFirstZoneHit = false;

        if (Data.bulletSprite != null)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = Data.bulletSprite;
        }

        ApplyVelocity();
        RotateTowardsDirection();

        foreach (var effect in Data.effects)
        {
            if (effect != null) effect.OnInit(this);
        }
    }

    private void Update()
    {
        if (_isDead || Data == null) return;

        _elapsedLife += Time.deltaTime;
        if (Data.lifeTime > 0f && _elapsedLife >= Data.lifeTime)
        {
            Die();
            return;
        }

        foreach (var effect in Data.effects)
        {
            if (effect != null) effect.OnTick(this, Time.deltaTime);
        }
    }

    private void FixedUpdate()
    {
        if (_isDead || Data == null) return;

        // 자력 등으로 무효화된 경우 이동을 멈춤 (실제 자력 판정은 물리엔진 담당자 영역)
        if (_isNullified)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        // 바람 등 외부 힘이 이번 프레임에 걸렸다면 방향을 보정.
        if (_externalForceThisFrame != Vector2.zero)
        {
            Vector2 newVelocity = _rb.linearVelocity + _externalForceThisFrame;
            _rb.linearVelocity = newVelocity;
            Direction = newVelocity.normalized;
            RotateTowardsDirection();
            _externalForceThisFrame = Vector2.zero;
        }
        else
        {
            // 외력이 없으면 지정된 방향/속도를 유지 (직선 이동 기본 동작)
            ApplyVelocity();
        }
    }

    private void ApplyVelocity()
    {
        _rb.linearVelocity = Direction * Data.speed;
    }

    private void RotateTowardsDirection()
    {
        float angle = Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    /// <summary>
    /// 유도탄 등 효과가 방향을 직접 갱신할 때 사용하는 API.
    /// </summary>
    public void SetDirection(Vector2 newDirection)
    {
        Direction = newDirection.normalized;
        RotateTowardsDirection();
    }

    /// <summary>
    /// 유도탄의 자동 타겟 탐색 등 효과가 런타임에 타겟을 지정할 때 사용하는 API.
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        Target = newTarget;
    }

    /// <summary>
    /// 물리엔진(바람) 담당 시스템이 매 프레임 호출해서 힘을 더해줄 수 있는 진입점.
    /// (예: WindZone.OnTriggerStay2D -> bullet.ApplyExternalForce(windForce))
    /// </summary>
    public void ApplyExternalForce(Vector2 force)
    {
        _externalForceThisFrame += force;
    }

    /// <summary>
    /// 물리엔진(자력) 담당 시스템이 총알을 무효화시킬 때 호출하는 진입점.
    /// </summary>
    public void SetNullified(bool nullified)
    {
        _isNullified = nullified;
        Debug.Log($"[BulletController] 자력 등으로 총알 무효화 상태 변경: {nullified} (물리엔진 시스템 미구현 - 실제 트리거 필요)");
    }

public BulletController SpawnChildBullet(BulletSO childData, Vector2 direction)
    {
        var childGO = Instantiate(gameObject, transform.position, Quaternion.identity);
        var childController = childGO.GetComponent<BulletController>();
        childController.IsSplitChild = true;
        childController.Init(childData != null ? childData : Data, direction);
        return childController;
    }


    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_isDead) return;

        if (IsInLayerMask(other.gameObject.layer, enemyLayerMask))
        {
            HandleEnemyHit(other);
            return;
        }

        if (IsInLayerMask(other.gameObject.layer, wallLayerMask))
        {
            // 실제 장애물 타입 판정은 장애물 담당 시스템이 IObstacle 등을 통해 제공해야 함.
            // 여기서는 컴포넌트가 있으면 사용하고, 없으면 기본 Wall로 취급하는 스텁을 둔다.
            BulletTargetType targetType = ResolveTargetType(other);
            HandleObstacleHit(other, targetType);
            return;
        }
    }

    /// <summary>
    /// 장애물 오브젝트로부터 타입을 알아내는 스텁.
    /// 장애물 담당자가 IBulletObstacle 같은 인터페이스를 구현하면 그쪽 값을 우선 사용하도록
    /// 확장 지점을 열어두었습니다. 지금은 태그 기반 임시 판정 + 기본값(Wall)입니다.
    /// </summary>
    private BulletTargetType ResolveTargetType(Collider2D other)
    {
        var provider = other.GetComponent<IBulletObstacleInfoProvider>();
        if (provider != null) return provider.TargetType;

        // 장애물 시스템 미구현 상태의 임시 폴백: 태그 이름으로 대충 매핑.
        switch (other.tag)
        {
            case "ArmoredWall": return BulletTargetType.ArmoredWall;
            case "Bush": return BulletTargetType.Bush;
            case "Tree": return BulletTargetType.Tree;
            case "Rock": return BulletTargetType.Rock;
            case "Civilian": return BulletTargetType.Civilian;
            case "Sandstorm": return BulletTargetType.Sandstorm;
            case "ElectricPanel": return BulletTargetType.ElectricPanel;
            case "HeatHaze": return BulletTargetType.HeatHaze;
            default: return BulletTargetType.Wall;
        }
    }

private void HandleEnemyHit(Collider2D enemy)
    {
        bool hasArmorPiercing = Data.HasEffect<ArmorPiercingEffectSO>();

        float finalDamage = Data.damage;
        if (hasArmorPiercing)
        {
            var armorEffect = Data.GetEffect<ArmorPiercingEffectSO>();
            var armored = enemy.GetComponent<IArmored>();
            if (armored != null && armored.IsArmored)
            {
                finalDamage *= armorEffect.armoredEnemyDamageMultiplier;
            }
        }

        BulletDamageDispatcher.ApplyDamage(enemy, finalDamage, Data.name);

        foreach (var effect in Data.effects)
        {
            if (effect != null) effect.OnHitEnemy(this, enemy);
        }

        // 철갑탄은 적도 관통(계속 직진). 그 외 효과는 적중 시 소멸.
        if (!hasArmorPiercing)
        {
            Die();
        }
    }

private void HandleObstacleHit(Collider2D obstacle, BulletTargetType targetType)
    {
        if (targetType == BulletTargetType.Civilian)
        {
            Debug.LogWarning("[BulletController] 민간인 피격! 스테이지 실패 처리");
            GameManager.Instance?.OnCivilianHit();
            Die();
            return;
        }

        BulletHitResult result = DetermineHitResult(targetType);

        // 파괴 가능한 장애물(나무/바위) 처리
        var destructible = obstacle.GetComponent<DestructibleObstacle>();
        if (destructible != null)
        {
            var explosiveEffect = Data.GetEffect<ExplosiveEffectSO>();
            if (explosiveEffect != null)
            {
                if (explosiveEffect.canDestroyRock)
                {
                    destructible.ApplyExplosionDamage(explosiveEffect.explosionDamage);
                }
            }
            else
            {
                destructible.ApplyBulletHit();
            }
        }

        foreach (var effect in Data.effects)
        {
            if (effect != null) effect.OnHitObstacle(this, obstacle, targetType);
        }

        switch (result)
        {
            case BulletHitResult.Penetrate:
                Debug.Log($"[BulletController] {targetType} 관통 (철갑탄)");
                break;

            case BulletHitResult.Bounce:
                Bounce(obstacle);
                break;

            case BulletHitResult.Destroy:
                Die();
                break;
        }
    }

    /// <summary>
    /// 장애물 타입에 따라 총알이 어떻게 반응할지 결정하는 기본 규칙.
    /// - 벽: 기본은 튕김, 철갑탄이면 관통
    /// - 장갑화된 벽: 모든 총알이 튕김 (철갑탄도 관통 불가)
    /// - 나무/바위: 벽과 동일 취급 (파괴 로직은 각 담당 시스템에서 처리, 여기선 튕김으로만 반응)
    /// - 풀숲/모래바람/아지랑이: 총알이 그냥 통과 (물리적 방벽 없음)
    /// - 전자 패널: 일단 벽처럼 튕김 처리 (기믹 상호작용은 별도 시스템)
    /// </summary>
    private BulletHitResult DetermineHitResult(BulletTargetType targetType)
    {
        bool hasArmorPiercing = Data.HasEffect<ArmorPiercingEffectSO>();

        switch (targetType)
        {
            case BulletTargetType.Wall:
                return hasArmorPiercing ? BulletHitResult.Penetrate : BulletHitResult.Bounce;

            case BulletTargetType.ArmoredWall:
                return BulletHitResult.Bounce; // 철갑탄도 예외 없이 튕김

            case BulletTargetType.Tree:
            case BulletTargetType.Rock:
                return BulletHitResult.Bounce; // 파괴 여부는 장애물 담당 시스템이 별도 처리

            case BulletTargetType.Bush:
            case BulletTargetType.Sandstorm:
            case BulletTargetType.HeatHaze:
                return BulletHitResult.Penetrate; // 시야만 가릴 뿐 물리적 방벽 없음 -> 그냥 통과

            case BulletTargetType.ElectricPanel:
                return BulletHitResult.Bounce;

            default:
                return BulletHitResult.Bounce;
        }
    }

    private void Bounce(Collider2D obstacle)
    {
        if (_bounceCount >= Data.maxBounceCount)
        {
            Die();
            return;
        }

        _bounceCount++;

        // 충돌 지점의 법선 벡터를 이용한 반사. 정확한 법선은 Raycast/Contact가 더 정밀하지만,
        // 여기서는 총알 위치 -> 장애물 중심 벡터의 역방향을 근사 법선으로 사용하는 간단한 버전.
        Vector2 approxNormal = ((Vector2)transform.position - (Vector2)obstacle.bounds.ClosestPoint(transform.position));
        if (approxNormal == Vector2.zero)
        {
            approxNormal = -Direction; // 폴백: 정반대 방향
        }
        approxNormal.Normalize();

        Vector2 reflected = Vector2.Reflect(Direction, approxNormal);
        SetDirection(reflected);
        ApplyVelocity();

        Debug.Log($"[BulletController] 벽 튕김 ({_bounceCount}/{Data.maxBounceCount})");
    }

private void Die()
    {
        if (_isDead) return;
        _isDead = true;

        foreach (var effect in Data.effects)
        {
            if (effect != null) effect.OnBulletDestroyed(this);
        }

        if (Data.destroyVfxPrefab != null)
        {
            Instantiate(Data.destroyVfxPrefab, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }

    private static bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}
