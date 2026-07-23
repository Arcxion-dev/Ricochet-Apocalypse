using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 2D(NavMeshPlus) 환경에서 적을 플레이어에게 확실히 추격시키는 이동 컨트롤러.
///
/// NavMeshAgent 자동 locomotion은 이 프로젝트의 2D 설정에서 경계 근처에서 위치가 어긋나
/// (remainingDistance=0으로 "도착" 오판) 굳는 문제가 있어, 이 컴포넌트는 <b>NavMeshAgent에
/// 의존하지 않고</b> 직접 경로를 계산해 transform을 그 경로를 따라 이동시킨다.
///
/// - <see cref="NavMesh.CalculatePath"/> 로 현재 위치→플레이어 경로를 주기적으로 계산.
/// - 다음 경로 코너(waypoint)를 향해 매 프레임 transform을 <see cref="SpeedModule"/> 속도로 이동.
/// - <see cref="NavMesh.SamplePosition"/> 로 navmesh 위로만 이동(경계 밖으로 못 나감 → 굳지 않음).
/// - Rigidbody2D/NavMeshAgent 물리 간섭이 없어 결정적으로 동작한다.
/// </summary>
public class EnemyChaser2D : MonoBehaviour
{
    [Tooltip("비우면 Start에서 tag가 Player인 오브젝트를 자동으로 찾는다.")]
    [SerializeField] private Transform _target;
    [Tooltip("경로 재계산 주기(초).")]
    [SerializeField] private float _repathInterval = 0.25f;
    [Tooltip("SpeedModule이 없을 때 사용할 기본 이동 속도.")]
    [SerializeField] private float _fallbackSpeed = 2f;
    [Tooltip("코너 도달로 간주하는 거리.")]
    [SerializeField] private float _arriveDistance = 0.15f;

    private SpeedModule _speed;
    private NavMeshPath _path;
    private float _timer;
    private int _corner;

    private void Awake()
    {
        _speed = GetComponent<SpeedModule>();
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

    private void Update()
    {
        if (_target == null) return;

        float speed = _speed != null ? _speed.CurrentSpeed : _fallbackSpeed;

        // 주기적으로 플레이어까지 경로 재계산.
        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            _timer = _repathInterval;
            if (NavMesh.CalculatePath(transform.position, _target.position, NavMesh.AllAreas, _path))
                _corner = _path.corners.Length > 1 ? 1 : 0;
        }

        if (_path == null || _path.corners.Length == 0) return;
        if (_corner >= _path.corners.Length) _corner = _path.corners.Length - 1;

        Vector3 cur = transform.position;
        Vector3 wp = _path.corners[_corner];
        Vector3 to = new Vector3(wp.x - cur.x, wp.y - cur.y, 0f);
        float dist = to.magnitude;

        // 현재 코너에 도달하면 다음 코너로.
        if (dist <= _arriveDistance)
        {
            if (_corner < _path.corners.Length - 1) _corner++;
            return;
        }

        Vector3 want = cur + (to / dist) * (speed * Time.deltaTime);

        // navmesh 위로만 이동(경계 밖으로 못 나감).
        if (NavMesh.SamplePosition(want, out NavMeshHit hit, 0.6f, NavMesh.AllAreas))
        {
            Vector3 np = hit.position;
            np.z = cur.z; // 2D 평면 z 유지
            transform.position = np;
        }
    }

    public void SetTarget(Transform target) => _target = target;
}
