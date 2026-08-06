using UnityEngine;

/// <summary>
/// WASD로 플레이어 구체를 상하좌우(XY 평면)로 이동시킨다.
/// - 방향키는 카메라 팬(CameraPanController) 전용이라 여기서는 WASD만 읽는다.
/// - 탑뷰 2D 기준으로 회전 없이 위치만 옮긴다(Z 고정).
/// - 이동 속도는 인스펙터에서 조절 가능한 프로퍼티(_moveSpeed, 기본 3.0).
///
/// 입력은 Legacy Input Manager 기준.
///
/// 이동은 Kinematic Rigidbody2D + 직접 스윕(<see cref="CollideAndSlide2D"/>) 방식이다.
/// 예전에는 Update에서 transform.position에 변위를 그냥 더했는데, 그러면 아무 충돌 검사도
/// 거치지 않아 벽을 그대로 통과했다. 이제는 이동 전에 진행 경로를 캐스팅해서 벽 앞에서 멈추고
/// 남은 이동량은 벽을 따라 미끄러지게 한다.
///
/// Dynamic Rigidbody2D에 물리 해석을 맡기지 않는 이유: 적(NavMeshAgent + Kinematic)이나 총알과
/// 부딪힐 때 플레이어가 밀려나거나 회전하는 부작용이 생긴다. 막을 대상(_blockingLayers)만
/// 정확히 막는 편이 이 게임의 조작감에 맞다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    /// <summary>이 거리 이상 어긋나면 외부 코드의 텔레포트로 간주한다(보간 오차보다 충분히 큰 값).</summary>
    private const float TeleportThreshold = 0.5f;

    [Header("이동")]
    [Tooltip("플레이어 이동 속도 (월드 유닛/초). 밸런싱 후 수정 가능.")]
    [SerializeField] private float _moveSpeed = 3.0f;

    [Header("충돌")]
    [Tooltip("플레이어를 막는 레이어. 비워두면 Wall 레이어를 자동으로 사용한다.")]
    [SerializeField] private LayerMask _blockingLayers;

    [Tooltip("벽에 완전히 달라붙어 캐스트가 불안정해지지 않도록 남겨두는 여유 간격(월드 유닛). " +
             "Physics2D의 contactOffset보다 작게 두면 무시되고 하한값이 적용된다.")]
    [SerializeField] private float _skinWidth = 0.03f;

    private Rigidbody2D _body;
    private Collider2D _collider;
    private ContactFilter2D _blockingFilter;

    /// <summary>
    /// 화면 위 좌/우 버튼이 넣어주는 가로 입력(-1~1). HUD가 매 프레임 갱신하고 손을 떼면 0으로 되돌린다.
    /// 키보드 WASD와 합쳐지므로 둘 중 아무거나 써도 된다.
    /// </summary>
    public float ExternalHorizontal { get; set; }

    /// <summary>세로 방향도 UI로 조작하고 싶을 때를 위해 같이 열어둔다(현재 HUD는 좌우만 쓴다).</summary>
    public float ExternalVertical { get; set; }

    /// <summary>이동 속도(월드 유닛/초). 런타임에 읽고 쓸 수 있다.</summary>
    public float MoveSpeed
    {
        get => _moveSpeed;
        set => _moveSpeed = Mathf.Max(0f, value);
    }

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();

        // 물리 해석은 쓰지 않고 위치만 직접 제어한다.
        _body.bodyType = RigidbodyType2D.Kinematic;
        _body.gravityScale = 0f;
        _body.freezeRotation = true;

        _blockingFilter = CollideAndSlide2D.CreateFilter(CollideAndSlide2D.ResolveDefaultWallMask(_blockingLayers));

        if (_collider == null)
        {
            Debug.LogWarning($"[PlayerMovement] {name}에 Collider2D가 없어 벽 충돌 처리를 건너뜁니다.", this);
        }
    }

    /// <summary>
    /// 스폰/맵 리셋처럼 충돌을 무시하고 즉시 옮겨야 할 때 쓴다.
    /// Transform만 옮기면 리지드바디의 물리 포즈와 어긋나 다음 스텝에 되돌아갈 수 있으므로 둘 다 맞춘다.
    /// </summary>
    public void Teleport(Vector3 position)
    {
        transform.position = position;
        if (_body != null) _body.position = position;
    }

    private void FixedUpdate()
    {
        Vector2 position = ResolveCurrentPosition();

        // 스폰/맵 리셋 직후 벽에 파묻혔다면 먼저 빠져나온다.
        // (겹친 상태에서는 어느 방향으로 캐스팅해도 거리 0에서 막혀 영영 못 움직인다.)
        position = CollideAndSlide2D.Depenetrate(_collider, position, _blockingFilter, _skinWidth);

        Vector2 input = ReadInput();
        if (input != Vector2.zero)
        {
            Vector2 delta = input * (_moveSpeed * Time.fixedDeltaTime);
            position = CollideAndSlide2D.Move(_collider, position, delta, _blockingFilter, _skinWidth);
        }

        if (position != _body.position)
        {
            _body.MovePosition(position);
        }
    }

    /// <summary>WASD와 화면 버튼 입력을 합쳐 대각선 보정(정규화)까지 마친 방향 벡터로 읽는다.</summary>
    private Vector2 ReadInput()
    {
        float x = 0f;
        float y = 0f;
        if (Input.GetKey(KeyCode.A)) x -= 1f;
        if (Input.GetKey(KeyCode.D)) x += 1f;
        if (Input.GetKey(KeyCode.S)) y -= 1f;
        if (Input.GetKey(KeyCode.W)) y += 1f;

        x += ExternalHorizontal;
        y += ExternalVertical;

        // 버튼과 키를 동시에 눌러도 속도가 2배가 되지 않도록 각 축을 먼저 -1~1로 자른다.
        x = Mathf.Clamp(x, -1f, 1f);
        y = Mathf.Clamp(y, -1f, 1f);

        // 대각선 이동이 빨라지지 않도록 정규화.
        return new Vector2(x, y).normalized;
    }

    /// <summary>
    /// 이동 기준 위치를 고른다. 평소에는 리지드바디 위치가 정본이지만,
    /// PlayerPositionModule 같은 외부 코드가 Transform을 직접 옮겼다면(스폰/맵 리셋) 그쪽을 따른다.
    /// </summary>
    private Vector2 ResolveCurrentPosition()
    {
        Vector2 bodyPosition = _body.position;
        Vector2 transformPosition = transform.position;

        if ((transformPosition - bodyPosition).sqrMagnitude <= TeleportThreshold * TeleportThreshold)
        {
            return bodyPosition;
        }

        _body.position = transformPosition; // 물리 포즈를 텔레포트 위치로 즉시 동기화
        return transformPosition;
    }
}
