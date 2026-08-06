using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// AI 모듈: NavMeshPlus(2D NavMesh)를 이용해 장애물을 피해 플레이어에게
/// 최단 경로로 이동합니다. 실제 이동은 UnityEngine.AI.NavMeshAgent가 담당하고,
/// 이 모듈은 타겟 추적 / 속도 동기화 / 목적지 갱신 주기만 관리합니다.
///
/// 사전 준비물 (씬):
/// - NavMeshPlus의 NavMeshSurface + CollectSources2d가 붙은 오브젝트에서 Bake가 되어 있어야 함
/// - 이 오브젝트에는 NavMeshAgent가 자동으로 추가됨 (RequireComponent)
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAIModule : MonoBehaviour, ISuppressible
{
    [Header("타겟 / 탐색 설정")]
    [Tooltip("비워두면 Awake 시 tag가 Player인 오브젝트를 자동으로 찾습니다.")]
    [SerializeField] private Transform target;
    [SerializeField] private float repathInterval = 0.3f;
    [SerializeField] private float stoppingDistance = 0.1f;
    [Tooltip("경계 변 목표점이 NavMesh 밖일 때 위로 스냅할 최대 탐색 거리.")]
    [SerializeField] private float navMeshSampleRadius = 2f;

    private NavMeshAgent agent;
    private SpeedModule speedModule;
    private float repathTimer;

    [Header("낑김 방지")]
    [Tooltip("경로는 있으나 이 시간(초) 이상 사실상 정지해 있으면 다음 경로 코너로 살짝 워프해 빠져나온다(좁은 코너 스낵 방지).")]
    [SerializeField] private float stuckNudgeAfter = 0.35f;
    [Tooltip("워프 한 번에 이동할 거리.")]
    [SerializeField] private float stuckNudgeStep = 0.25f;
    private float stuckTimer;

    /// <summary>"Civilian" 태그가 붙은 대상은 스테이지 내내 제자리에 가만히 있고 절대 추격하지 않는다.</summary>
    private bool isCivilian;

    /// <summary>이동 저지(섬광탄/저지탄) 남은 시간(초). 0보다 크면 저지 중.</summary>
    private float suppressTimer;
    /// <summary>저지 중 이동속도 감소율(1 = 완전 정지).</summary>
    private float suppressSlow;

    /// <summary>도망 특성 등이 강제로 목적지를 지정하는 동안 남은 시간(초). 0보다 크면 플레이어 추격 대신 이 목적지를 유지한다.</summary>
    private float overrideDestinationTimer;
    private Vector3 overrideDestination;

    /// <summary>
    /// 지정된 시간 동안 추격 대신 이 목적지로 강제 이동시킨다(도망 특성의 후퇴 등).
    /// 매 프레임 재지정되지 않으므로 도착 후에는 그 자리에 멈춘다.
    /// </summary>
    public void ForceDestinationFor(Vector3 point, float duration)
    {
        overrideDestination = point;
        overrideDestinationTimer = Mathf.Max(overrideDestinationTimer, duration);
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(point);
        }
    }

    /// <summary>ISuppressible: 지정 시간 동안 이동을 저지한다(더 강하거나 더 긴 저지가 우선).</summary>
    public void ApplySuppression(float duration, float slowRatio)
    {
        suppressTimer = Mathf.Max(suppressTimer, duration);
        suppressSlow = Mathf.Max(suppressSlow, Mathf.Clamp01(slowRatio));
    }

    private void Awake()
    {
        isCivilian = CompareTag("Civilian");

        speedModule = GetComponent<SpeedModule>();

        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false; // 2D 스프라이트가 3D 회전을 따라가지 않도록
        agent.updateUpAxis = false;   // NavMeshPlus로 XY 평면에 구운 메시 기준
        agent.stoppingDistance = stoppingDistance;
        // 좁은 통로(1칸 지그재그·벽 사이 등)에서 지역 회피(HighQuality)가 서로/네비메시 경계를
        // 과도하게 피하며 에이전트가 낑겨 멈추는 문제를 막는다. 회피를 끄면 적끼리 겹칠 수 있으나
        // 이동은 여전히 navmesh 경로를 따르므로 벽은 그대로 준수한다(관통 아님).
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;

        if (isCivilian)
        {
            // 민간인: 목적지를 절대 설정하지 않아 제자리 고정. 에이전트도 멈춰둔다(navmesh 위일 때만 안전하게).
            if (agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }
            return; // 타겟 탐색/속도 동기화 불필요.
        }

        if (target == null)
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null) target = playerGo.transform;
        }

        SyncSpeed();
    }

    private void Update()
    {
        if (isCivilian) return; // 민간인은 절대 추격하지 않는다(제자리 고정).

        if (agent == null || !agent.isOnNavMesh) return;

        // 이동 저지(섬광탄/저지탄): 타이머를 깎고, 완전 저지면 이 프레임 이동을 멈춘다.
        bool fullyStopped = false;
        if (suppressTimer > 0f)
        {
            suppressTimer -= Time.deltaTime;
            if (suppressTimer <= 0f) suppressSlow = 0f;      // 저지 종료 → 감속 해제
            else fullyStopped = suppressSlow >= 0.999f;       // 완전 정지 여부
        }

        SyncSpeed(); // 부분 감속(slowRatio<1)도 매 프레임 반영

        if (fullyStopped)
        {
            if (!agent.isStopped) agent.isStopped = true;
            return; // 완전 저지 중엔 목적지를 갱신하지 않는다.
        }
        if (agent.isStopped) agent.isStopped = false;

        if (overrideDestinationTimer > 0f)
        {
            overrideDestinationTimer -= Time.deltaTime;
            return; // 강제 목적지 유지 중 - 추격 재지정을 건너뛴다.
        }

        if (target == null) return;

        // 스테이지 시작 직후에는 제자리 대기. 플레이어가 첫 발을 쏘고 추격 지연이 끝나야
        // GameManager.EnemiesCanChase가 켜지고 그때부터 목적지를 갱신(추격)한다.
        if (GameManager.Instance != null && !GameManager.Instance.EnemiesCanChase) return;

        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            repathTimer = repathInterval;

            // 플레이어에게 가는 "최단 경로"로 직접 추격한다.
            // NavMesh 경로탐색이 장애물을 우회하는 최단 경로를 스스로 계산하므로,
            // 목적지는 항상 플레이어 위치로 둔다(가장자리 벽으로 새지 않게 함).
            // 플레이어가 NavMesh 밖(벽 근처)일 수 있으니 위로 스냅해 목적지 실패를 막는다.
            Vector3 dest = target.position;
            if (NavMesh.SamplePosition(dest, out var hit, navMeshSampleRadius, NavMesh.AllAreas))
                dest = hit.position;
            agent.SetDestination(dest);
        }

        NudgeIfStuck();
    }

    /// <summary>
    /// 경로(hasPath)는 있는데 목적지가 아직 멀리 있음에도 사실상 정지 상태로 낑겨 있으면,
    /// 다음 경로 코너(steeringTarget) 쪽으로 조금씩 Warp 해 좁은 코너를 빠져나오게 한다.
    /// (지역 회피를 꺼도 1칸 통로/코너에서 남는 기하학적 스낵을 일반적으로 해소한다.)
    /// </summary>
    private void NudgeIfStuck()
    {
        bool pathed = agent.hasPath && !agent.pathPending && agent.remainingDistance > 1.0f;
        bool crawling = agent.velocity.magnitude < agent.speed * 0.25f;
        if (pathed && crawling)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= stuckNudgeAfter)
            {
                Vector3 nudged = Vector3.MoveTowards(transform.position, agent.steeringTarget, stuckNudgeStep);
                agent.Warp(nudged);
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }
    }

    /// <summary>
    /// SpeedModule의 현재 배수를 매 프레임 NavMeshAgent 속도에 반영합니다.
    /// (신속 특성 등으로 런타임에 배수가 바뀌어도 즉시 따라가도록)
    /// </summary>
    private void SyncSpeed()
    {
        if (speedModule == null || agent == null) return;
        float mult = suppressTimer > 0f ? Mathf.Clamp01(1f - suppressSlow) : 1f;
        agent.speed = speedModule.CurrentSpeed * mult;
    }

    public void SetTarget(Transform newTarget) => target = newTarget;
}
