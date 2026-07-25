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
    private Collider2D _col;
    private int _bounceCount;
    private Collider2D _lastPenetratedWall; // 관통형 벽을 매 프레임 중복 처리하지 않기 위한 표시
    private float _elapsedLife;
    private bool _isDead;

    // 물리엔진(바람/자력) 담당 시스템이 외부에서 걸어줄 수 있는 훅.
    // 바람: 매 프레임 힘을 더해주는 방식 / 자력: 총알을 무효화(Nullify)하는 방식.
    private Vector2 _externalForceThisFrame;
    private bool _isNullified;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<Collider2D>();
        _rb.gravityScale = 0f; // 탑다운 2D 슈터 기준

        // 고속 총알이 얇은 벽을 뚫고 지나가는(터널링) 것을 막기 위해 연속 충돌 감지를 켠다.
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
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

        // 이번 스텝에 이동할 경로를 미리 스윕(CircleCast)해서 벽을 감지한다.
        // 트리거(OnTriggerEnter2D)만으로는 고속 총알이 벽을 뚫고 지나가거나(터널링),
        // 감지가 늦어 반대편으로 튀어나가는 문제가 있어 벽 충돌은 스윕으로 처리한다.
        SweepWalls();
    }

    /// <summary>
    /// 현재 위치에서 이번 물리 스텝 이동 거리만큼 앞을 CircleCast로 훑어 벽을 감지한다.
    /// 벽을 만나면 정확한 접점/법선으로 처리(튕김 시 접점으로 위치 보정 → 관통·오버슈트 방지).
    /// </summary>
    private void SweepWalls()
    {
        Vector2 vel = _rb.linearVelocity;
        float stepDist = vel.magnitude * Time.fixedDeltaTime;
        if (stepDist <= 0.0001f) return;

        Vector2 dir = vel.normalized;
        float radius = GetBulletRadius();
        RaycastHit2D hit = Physics2D.CircleCast(_rb.position, radius, dir, stepDist + 0.02f, wallLayerMask);
        if (hit.collider == null)
        {
            _lastPenetratedWall = null; // 벽에서 벗어남
            return;
        }

        BulletTargetType targetType = ResolveTargetType(hit.collider);
        HandleObstacleHit(hit.collider, targetType, hit.normal, hit.centroid);
    }

    /// <summary>총알 콜라이더를 근사한 스윕용 반지름(회전 무관하게 안전한 값).</summary>
    private float GetBulletRadius()
    {
        if (_col == null) return 0.45f;
        Vector3 ext = _col.bounds.extents;
        return Mathf.Max(0.05f, Mathf.Min(ext.x, ext.y) - 0.02f);
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

        // 벽(Wall) 충돌은 트리거가 아니라 FixedUpdate의 SweepWalls()에서 처리한다.
        // (고속 총알 터널링/늦은 감지로 인한 관통 방지)
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

    /// <summary>EffectHandler 이펙트 카테고리(피격/튕김/폭발).</summary>
    private enum EffectKind { Hit, Bounce, Explosion }

    /// <summary>
    /// EffectHandler가 씬에 있으면 해당 카테고리의 무작위 이펙트를 현재 위치에 재생한다.
    /// EffectHandler가 없거나 목록이 비어 있으면 조용히 무시(씬에 EffectHandler가 없어도 안전).
    /// </summary>
    private void PlayHitEffect(EffectKind kind)
    {
        var handler = EffectHandler.Instance;
        if (handler == null) return;

        System.Collections.Generic.List<string> names =
            kind == EffectKind.Hit ? handler.hitName :
            kind == EffectKind.Bounce ? handler.bounceName :
            handler.explosionName;

        if (names == null || names.Count == 0) return;
        handler.Play(names[Random.Range(0, names.Count)], transform.position);
    }

private void HandleEnemyHit(Collider2D enemy)
    {
        bool hasArmorPiercing = Data.HasEffect<ArmorPiercingEffectSO>();
        PlayHitEffect(EffectKind.Hit); // 적 피격 이펙트 (Effect 담당자 EffectHandler 연동)

        // 적에 EnemyController가 있으면 그쪽 파이프라인(방어 무적 -> 헤드샷 -> 속성 취약 배수 ->
        // 장갑 배수 -> Entity.TakeDamage)으로 라우팅한다. 총알 속성(Data.element)과 철갑탄 여부를 넘겨
        // AttributeModule/ArmorModule/HeadshotModule/DefenseModule이 실제로 작동하게 한다.
        var enemyController = enemy.GetComponentInParent<EnemyController>();
        if (enemyController != null)
        {
            enemyController.OnBulletHit(Data.damage, Data.element, hasArmorPiercing);
        }
        else
        {
            // EnemyController가 없는 적: 기존 경로(철갑탄 IArmored 배수 + 직접 Entity 대미지) 폴백.
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
        }

        foreach (var effect in Data.effects)
        {
            if (effect != null) effect.OnHitEnemy(this, enemy);
        }

        // 모든 총알은 적을 관통합니다 (적 충돌로는 소멸하지 않음).
        // 총알의 소멸은 벽 튕김 횟수 초과, 생존시간 만료 등 다른 조건에서만 발생합니다.
    }

private void HandleObstacleHit(Collider2D obstacle, BulletTargetType targetType, Vector2 normal, Vector2 contactPoint)
    {


        if (targetType == BulletTargetType.Civilian)
        {
            Debug.LogWarning("[BulletController] 민간인 피격! 스테이지 실패 처리");
            GameManager.Instance?.OnCivilianHit();
            Die();
            return;
        }

        BulletHitResult result = DetermineHitResult(targetType);

        // 관통형(총알이 통과하는) 벽은 스윕이 매 프레임 같은 벽을 다시 감지하므로,
        // 파괴/효과 훅은 "그 벽에 처음 닿았을 때" 1회만 실행한다(원래 트리거 1회 동작과 동일).
        bool isNewContact = obstacle != _lastPenetratedWall;
        if (result != BulletHitResult.Penetrate || isNewContact)
        {
            // 벽 충돌/튕김 이펙트 재생 (Effect 담당자 EffectHandler 연동)
            PlayHitEffect(EffectKind.Bounce);

            // 파괴 가능한 장애물(나무/바위) 처리
            var destructible = obstacle.GetComponent<DestructibleObstacle>();
            if (destructible != null)
            {
                // 파괴 이펙트 재생
                PlayHitEffect(EffectKind.Explosion);

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
        }

        switch (result)
        {
            case BulletHitResult.Penetrate:
                _lastPenetratedWall = obstacle; // 통과 중인 벽 기억(중복 처리 방지)
                break;

            case BulletHitResult.Bounce:
                _lastPenetratedWall = null;
                Bounce(normal, contactPoint);
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

    /// <summary>
    /// 스윕이 알려준 정확한 접점/법선으로 튕긴다. 총알을 접점으로 옮겨(오버슈트로 벽에 파고들지
    /// 않게) 벽 표면 법선 기준으로 반사시킨다. 스윕은 벽에 닿기 "전"에 감지하므로 총알 중심이
    /// 벽 반대편으로 넘어가는 일이 없어 통과가 구조적으로 발생하지 않는다.
    /// </summary>
    private void Bounce(Vector2 normal, Vector2 contactPoint)
    {
        if (_bounceCount >= Data.maxBounceCount)
        {
            Die();
            return;
        }

        _bounceCount++;

        if (normal == Vector2.zero) normal = -Direction; // 폴백
        normal.Normalize();

        // 접점(총알 콜라이더가 벽에 막 닿는 위치)으로 보정 → 벽을 파고들지 않음.
        _rb.position = contactPoint;

        Vector2 reflected = Vector2.Reflect(Direction, normal);
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
