using System.Collections.Generic;
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

    /// <summary>
    /// 화상탄/냉기탄처럼 "최초 1회 적중"을 추적해야 하는 장판 효과들을 위한 상태.
    /// 효과 타입별로 따로 추적해야, 화상+냉기처럼 서로 다른 장판 효과를 함께 지닌 탄환이
    /// 첫 피격 시 둘 다 발동한다(하나의 공용 bool이면 먼저 도는 효과만 발동하고 나머지는 씹힘).
    /// </summary>
    private readonly HashSet<System.Type> _triggeredZoneEffectTypes = new HashSet<System.Type>();
    public bool HasTriggeredZoneEffect(System.Type effectType) => _triggeredZoneEffectTypes.Contains(effectType);
    public void MarkZoneEffectTriggered(System.Type effectType) => _triggeredZoneEffectTypes.Add(effectType);

    /// <summary>분열탄이 자식 총알에 다시 분열 효과를 넣지 않도록 방지하는 플래그.</summary>
    public bool IsSplitChild { get; set; }

    [Header("액션감 (속도 스트레치/발사 가속)")]
    [Tooltip("이 속도를 기준(스트레치 배율 1배)으로 다른 속도의 총알은 진행방향으로 더 늘어나거나 짧아진다.")]
    [SerializeField] private float _referenceSpeedForStretch = 15f;
    [Tooltip("스트레치 배율의 최소/최대 클램프(진행방향 스케일).")]
    [SerializeField] private Vector2 _stretchClamp = new Vector2(0.8f, 1.6f);
    [Tooltip("발사 직후 목표 속도까지 가속하는 데 걸리는 시간(초). 0이면 즉시 최고속.")]
    [SerializeField] private float _launchRampDuration = 0.05f;
    [Tooltip("발사 시작 순간의 속도 배율(이후 1배까지 램프업).")]
    [Range(0f, 1f)] [SerializeField] private float _launchStartSpeedFactor = 0.35f;

    private Rigidbody2D _rb;
    private Collider2D _col;
    private TrailRenderer _trail;
    private Vector3 _baseScale;
    private int _bounceCount;
    private Collider2D _lastPenetratedWall; // 관통형 벽을 매 프레임 중복 처리하지 않기 위한 표시

    /// <summary>
    /// BulletSO.maxBounceCount 대신 이 총알 인스턴스에만 적용할 튕김 한도.
    /// null이면 BulletSO 값을 그대로 사용한다.
    /// (예: 분열탄이 벽 튕김으로 만들어낸 자식 탄환은 0으로 설정해 벽에 닿는 즉시 소멸시킨다.)
    /// </summary>
    private int? _maxBounceCountOverride;

    /// <summary>이 총알 인스턴스의 튕김 한도를 강제로 지정한다. 인스턴스 단위이므로 원본 BulletSO는 건드리지 않는다.</summary>
    public void SetMaxBounceCountOverride(int maxBounceCount) => _maxBounceCountOverride = maxBounceCount;

    private int EffectiveMaxBounceCount => _maxBounceCountOverride ?? Data.maxBounceCount;

    /// <summary>
    /// 이번 "튕김 구간"(마지막 벽 튕김 이후 ~ 다음 튕김 전)에 이미 피격 처리한 적 콜라이더 집합.
    /// 넉백으로 적이 밀리면 총알이 적 콜라이더를 잠깐 벗어났다가 같은 프레임/직후 프레임에 다시
    /// 따라잡아 재진입하는 경우가 있어(Enter/Exit만으로는 구분 불가) Exit이 아니라 <see cref="Bounce"/>
    /// 시점에만 초기화한다. 즉 한 번의 직선 관통 구간에서는 같은 적을 한 번만 맞히고,
    /// 벽에 튕겨 방향이 바뀌면 그때부터는 같은 적이라도 새로 맞을 수 있다.
    /// 뭉쳐있는 다른 적(별도 콜라이더)은 이 집합과 무관하게 각자 독립적으로 판정된다.
    /// </summary>
    private readonly HashSet<Collider2D> _hitEnemiesThisSegment = new HashSet<Collider2D>();
    private float _elapsedLife;
    private float _launchElapsed;
    private bool _isDead;

    // 물리엔진(바람/자력) 담당 시스템이 외부에서 걸어줄 수 있는 훅.
    // 바람: 매 프레임 힘을 더해주는 방식 / 자력: 총알을 무효화(Nullify)하는 방식.
    private Vector2 _externalForceThisFrame;
    private bool _isNullified;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<Collider2D>();
        _trail = GetComponent<TrailRenderer>();
        _baseScale = transform.localScale;
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
        _maxBounceCountOverride = null;
        _elapsedLife = 0f;
        _launchElapsed = 0f;
        _isDead = false;
        _hitEnemiesThisSegment.Clear();
        _triggeredZoneEffectTypes.Clear();

        if (Data.bulletSprite != null)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = Data.bulletSprite;
        }

        ApplyStretch();

        ApplyVelocity();
        RotateTowardsDirection();

        foreach (var effect in Data.effects)
        {
            if (effect != null) effect.OnInit(this);
        }
    }

    /// <summary>총알 속도에 비례해 진행방향(로컬 X)으로 늘이거나 줄인다 — 빠른 총알일수록 길쭉해 보인다.</summary>
    private void ApplyStretch()
    {
        if (_referenceSpeedForStretch <= 0f) return;
        float stretch = Mathf.Clamp(Data.speed / _referenceSpeedForStretch, _stretchClamp.x, _stretchClamp.y);
        transform.localScale = new Vector3(_baseScale.x * stretch, _baseScale.y, _baseScale.z);
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

        if (_launchElapsed < _launchRampDuration) _launchElapsed += Time.deltaTime;

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

    /// <summary>이 총알의 스윕용 반지름. <see cref="ComputeCollisionRadius"/>를 콜라이더에 적용한다.</summary>
    private float GetBulletRadius() => ComputeCollisionRadius(_col);

    /// <summary>
    /// 콜라이더 크기(로컬 size × lossyScale)로 스윕/반사 예측용 반지름을 구한다.
    /// <see cref="Collider2D.bounds"/>(회전된 AABB)를 쓰지 않아 <b>총알 회전과 무관하게 일정</b>하며,
    /// 씬에 스폰하지 않은 프리팹에서도 계산된다. 조준선 예측(<see cref="PlayerShooter"/>)이 실제 탄환과
    /// 정확히 같은 반지름으로 벽 반사를 예측하도록 양쪽이 이 메서드를 공유한다.
    /// </summary>
    public static float ComputeCollisionRadius(Collider2D col)
    {
        if (col == null) return 0.45f;
        Vector2 lossy = col.transform.lossyScale;
        Vector2 ext;
        if (col is BoxCollider2D box)
            ext = new Vector2(box.size.x * 0.5f * Mathf.Abs(lossy.x), box.size.y * 0.5f * Mathf.Abs(lossy.y));
        else if (col is CircleCollider2D circ)
        {
            float r = circ.radius * Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y));
            ext = new Vector2(r, r);
        }
        else if (col is CapsuleCollider2D cap)
            ext = new Vector2(cap.size.x * 0.5f * Mathf.Abs(lossy.x), cap.size.y * 0.5f * Mathf.Abs(lossy.y));
        else
            ext = col.bounds.extents; // 기타 콜라이더 폴백
        return Mathf.Max(0.05f, Mathf.Min(ext.x, ext.y) - 0.02f);
    }

    private void ApplyVelocity()
    {
        _rb.linearVelocity = Direction * (Data.speed * LaunchSpeedMultiplier());
    }

    /// <summary>발사 직후 <see cref="_launchRampDuration"/>초 동안 속도를 0~1배로 램프업한다(즉시 최고속 대신 살짝 가속하는 느낌).</summary>
    private float LaunchSpeedMultiplier()
    {
        if (_launchRampDuration <= 0f) return 1f;
        float t = Mathf.Clamp01(_launchElapsed / _launchRampDuration);
        return Mathf.Lerp(_launchStartSpeedFactor, 1f, t);
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
            // 이번 튕김 구간에서 같은 적을 이미 맞혔다면 무시(넉백으로 밀려났다 다시 따라잡는
            // 경우 포함 — 물리엔진이 같은 접촉을 중복 통지하는 경우도 함께 막힌다).
            if (!_hitEnemiesThisSegment.Add(other)) return;
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
    public static BulletTargetType ResolveTargetType(Collider2D other)
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
        // 사운드는 있으면 재생하되, 없거나 예외가 나도 게임플레이(튕김 등)에는 영향 없게 한다.
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySfx("Hit");
    }

private void HandleEnemyHit(Collider2D enemy)
    {
        bool hasArmorPiercing = Data.HasEffect<ArmorPiercingEffectSO>();
        PlayHitEffect(EffectKind.Hit); // 적 피격 이펙트 (Effect 담당자 EffectHandler 연동)

        // 적에 EnemyController가 있으면 그쪽 파이프라인(방어 무적 -> 헤드샷 -> 속성(효과) 취약 배수 ->
        // 장갑 배수 -> Entity.TakeDamage)으로 라우팅한다. 총알 데이터(Data)와 철갑탄 여부를 넘겨
        // AttributeModule/ArmorModule/HeadshotModule/DefenseModule이 실제로 작동하게 한다.
        // 무기 파츠(강선/조준경/텅스텐) 상태를 현재 사수에서 읽는다. 강선 강화는 기본 대미지에 곱한다.
        var shooter = PlayerShooter.Active;
        float damageMul = shooter != null ? shooter.DamageMultiplier : 1f;
        float baseDamage = Data.damage * damageMul;

        var enemyController = enemy.GetComponentInParent<EnemyController>();
        if (enemyController != null)
        {
            float headshotBonus = shooter != null ? shooter.HeadshotMultiplierBonus : 0f;
            bool tungsten = shooter != null && shooter.GuaranteedHeadshotChain;
            bool force = tungsten && shooter.ConsumeGuaranteedHeadshot();

            bool wasHeadshot = enemyController.OnBulletHit(baseDamage, Data, hasArmorPiercing, headshotBonus, force, Direction);

            // 텅스텐: 자연(비확정) 헤드샷일 때만 다음 명중을 확정 헤드샷으로 예약(무한 연쇄 방지).
            if (wasHeadshot && !force && tungsten) shooter.ArmGuaranteedHeadshot();
        }
        else
        {
            // EnemyController가 없는 적: 기존 경로(철갑탄 IArmored 배수 + 직접 Entity 대미지) 폴백.
            float finalDamage = baseDamage;
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
            Debug.LogWarning("[BulletController] 민간인 피격! 이번 스테이지 클리어 보상 감소");
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
                Bounce(normal, contactPoint, obstacle);
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
        => DetermineHitResult(targetType, Data != null && Data.HasEffect<ArmorPiercingEffectSO>());

    /// <summary>
    /// 장애물 타입 + 철갑탄 여부로 총알 반응(튕김/관통/파괴)을 결정하는 공용 규칙(static).
    /// 조준선 예측(<see cref="PlayerShooter"/>)이 실제 탄환과 동일하게 판정하도록 이 메서드를 공유한다.
    /// </summary>
    public static BulletHitResult DetermineHitResult(BulletTargetType targetType, bool hasArmorPiercing)
    {
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
    private void Bounce(Vector2 normal, Vector2 contactPoint, Collider2D obstacle = null)
    {
        // 탄성벽(ElasticWallMarker)에 튕기는 경우 튕김 횟수를 소모하지 않는다(무제한 튕김).
        bool isElasticBounce = obstacle != null && obstacle.GetComponent<ElasticWallMarker>() != null;

        if (!isElasticBounce && _bounceCount >= EffectiveMaxBounceCount)
        {
            Die();
            return;
        }

        if (!isElasticBounce) _bounceCount++;
        _hitEnemiesThisSegment.Clear(); // 방향이 바뀌었으니 이제부터는 같은 적도 새로 맞을 수 있다.

        if (normal == Vector2.zero) normal = -Direction; // 폴백
        normal.Normalize();

        // 접점(총알 콜라이더가 벽에 막 닿는 위치)으로 보정 → 벽을 파고들지 않음.
        _rb.position = contactPoint;

        Vector2 reflected = Vector2.Reflect(Direction, normal);
        SetDirection(reflected);
        ApplyVelocity();

        Debug.Log(isElasticBounce
            ? "[BulletController] 탄성벽 튕김 (카운트 소모 없음)"
            : $"[BulletController] 벽 튕김 ({_bounceCount}/{EffectiveMaxBounceCount})");
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
