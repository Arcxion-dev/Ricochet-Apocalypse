using UnityEngine;

public class Player : Entity
{
    [Header("접촉 피해 (적과 몸이 닿으면 체력 감소)")]
    [Tooltip("적과 접촉할 때 한 번에 입는 피해량.")]
    [SerializeField] private int _contactDamage = 10;
    [Tooltip("한 번 맞은 뒤 다음 접촉 피해까지의 무적 시간(초). 겹쳐 있는 동안 매 프레임 깎이지 않도록 한다.")]
    [SerializeField] private float _invulnerabilitySeconds = 1.0f;
    [Tooltip("접촉 피해로 판정할 대상 레이어. 비워두면 'Enemy' 레이어를 사용한다.")]
    [SerializeField] private LayerMask _enemyLayers;

    // 적/플레이어 모두 Kinematic Rigidbody2D + 비트리거 콜라이더라 OnCollision/OnTrigger 콜백이 발생하지 않는다.
    // 그래서 매 물리 프레임 플레이어 콜라이더의 겹침을 직접 질의(Overlap)해 접촉 피해를 준다.
    private Collider2D _collider;
    private ContactFilter2D _enemyFilter;
    private readonly Collider2D[] _overlaps = new Collider2D[8];
    private float _invulnTimer;

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();

        int mask = _enemyLayers.value != 0 ? _enemyLayers.value : LayerMask.GetMask("Enemy");
        _enemyFilter = new ContactFilter2D
        {
            useLayerMask = mask != 0, // 마스크가 없으면(0) 레이어 필터를 끄고 태그로만 거른다.
            layerMask = mask,
            useTriggers = false,      // 적 콜라이더는 비트리거.
        };
    }

    private void FixedUpdate()
    {
        if (_invulnTimer > 0f) { _invulnTimer -= Time.fixedDeltaTime; return; }
        if (_collider == null) return;

        // 스테이지가 실제로 진행(적 추격 개시)될 때만 접촉 피해를 받는다: 스폰 직후 겹침으로 인한 즉사 방지.
        if (GameManager.Instance != null && !GameManager.Instance.EnemiesCanChase) return;

        int n = _collider.Overlap(_enemyFilter, _overlaps);
        for (int i = 0; i < n; i++)
        {
            var other = _overlaps[i];
            if (other == null || !other.CompareTag("Enemy")) continue; // 민간인(Civilian)·벽 등은 제외.
            TakeDamage(_contactDamage);            // Entity.TakeDamage → DecreaseHP + OnDamaged(HUD가 폴링해 체력바 갱신/플래시).
            _invulnTimer = _invulnerabilitySeconds; // 무적 시작.
            break;                                  // 한 프레임에 한 번만 피해.
        }
    }

    protected override void DecreaseHP(int _amount)
    {

        health -= _amount;
        if (health <= 0)
        {
            GameManager.Instance?.OnPlayerDeath(); // 스테이지 실패 판정
            Destroy(gameObject); //임시
        }
    }
}
