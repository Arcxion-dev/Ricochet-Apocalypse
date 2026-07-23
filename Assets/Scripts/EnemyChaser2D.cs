using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 2D(NavMeshPlus) 환경에서 적을 플레이어에게 추격시키는 이동 컨트롤러.
///
/// NavMeshAgent 자동 locomotion이 이 프로젝트의 2D 설정에서 경계 근처에서 굳는 문제가 있어,
/// NavMeshAgent에 의존하지 않고 <see cref="NavMesh.CalculatePath"/> 로 플레이어까지의 경로를
/// 계산해 그 <b>다음 코너(웨이포인트)를 향해</b> 이동한다. 경로는 벽을 돌아가므로 자연히 장애물을
/// 우회한다.
///
/// 이동 한 스텝마다 <see cref="NavMesh.Raycast"/> 로 현재→목표 사이에 navmesh 경계(벽/바깥)가
/// 있는지 확인하고, 있으면 <b>그 경계 지점까지만</b> 이동한다. 따라서 장애물을 통과하지 못하고,
/// navmesh로 덮인 영역 밖으로 나가지 못하며, 플레이어가 navmesh 밖/도달 불가면(PathInvalid)
/// 그쪽으로 따라가지 않는다.
///
/// 실제 이동은 Kinematic <see cref="Rigidbody2D"/> 의 <see cref="Rigidbody2D.MovePosition"/> 로
/// 수행한다(FixedUpdate). Kinematic RB가 있는데 transform.position을 직접 대입하면 물리 스텝에서
/// 되돌려지므로 반드시 MovePosition을 써야 한다.
/// </summary>
public class EnemyChaser2D : MonoBehaviour
{
    [Tooltip("비우면 Start에서 tag가 Player인 오브젝트를 자동으로 찾는다.")]
    [SerializeField] private Transform _target;
    [Tooltip("경로 재계산 주기(초).")]
    [SerializeField] private float _repathInterval = 0.1f;
    [Tooltip("SpeedModule이 없을 때 사용할 기본 이동 속도.")]
    [SerializeField] private float _fallbackSpeed = 2f;

    private SpeedModule _speed;
    private Rigidbody2D _rb;
    private NavMeshPath _path;
    private float _timer;

    private void Awake()
    {
        _speed = GetComponent<SpeedModule>();
        _rb = GetComponent<Rigidbody2D>();
        _path = new NavMeshPath();
    }

    private void Start()
    {
        if (_target == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _target = player.transform;
        }
    }

    private void FixedUpdate()
    {
        if (_target == null) return;

        _timer -= Time.fixedDeltaTime;
        if (_timer <= 0f)
        {
            _timer = _repathInterval;
            NavMesh.CalculatePath(transform.position, _target.position, NavMesh.AllAreas, _path);
        }

        // 유효한 경로만 따른다. PathInvalid(플레이어가 navmesh 밖/도달 불가)면 이동하지 않음.
        if (_path == null || _path.status == NavMeshPathStatus.PathInvalid) return;
        if (_path.corners.Length < 2) return;

        float speed = _speed != null ? _speed.CurrentSpeed : _fallbackSpeed;

        Vector3 cur = transform.position;
        Vector3 wp = _path.corners[1]; // 경로의 다음 웨이포인트(벽을 돌아가는 방향)
        Vector3 to = new Vector3(wp.x - cur.x, wp.y - cur.y, 0f);
        float dist = to.magnitude;
        if (dist < 0.0001f) return;

        Vector3 step = to / dist * (speed * Time.fixedDeltaTime);
        if (step.sqrMagnitude > to.sqrMagnitude) step = to; // 웨이포인트 지나치지 않게
        Vector3 want = cur + step;

        // 현재→목표 사이에 navmesh 경계(벽/바깥)가 있으면 그 경계까지만 → 통과/이탈 차단.
        Vector2 dest = NavMesh.Raycast(cur, want, out NavMeshHit hit, NavMesh.AllAreas)
            ? new Vector2(hit.position.x, hit.position.y)
            : new Vector2(want.x, want.y);

        if (_rb != null && _rb.bodyType != RigidbodyType2D.Dynamic)
            _rb.MovePosition(dest); // Kinematic RB가 있으면 MovePosition으로 이동
        else
            transform.position = new Vector3(dest.x, dest.y, cur.z);
    }

    public void SetTarget(Transform target) => _target = target;
}
