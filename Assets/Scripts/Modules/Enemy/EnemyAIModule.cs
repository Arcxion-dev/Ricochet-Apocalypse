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
public class EnemyAIModule : MonoBehaviour
{
    [Header("타겟 / 탐색 설정")]
    [Tooltip("비워두면 Awake 시 tag가 Player인 오브젝트를 자동으로 찾습니다.")]
    [SerializeField] private Transform target;
    [SerializeField] private float repathInterval = 0.3f;
    [SerializeField] private float stoppingDistance = 0.1f;

    private NavMeshAgent agent;
    private SpeedModule speedModule;
    private float repathTimer;

    private void Awake()
    {
        speedModule = GetComponent<SpeedModule>();

        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false; // 2D 스프라이트가 3D 회전을 따라가지 않도록
        agent.updateUpAxis = false;   // NavMeshPlus로 XY 평면에 구운 메시 기준
        agent.stoppingDistance = stoppingDistance;

        if (target == null)
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null) target = playerGo.transform;
        }

        SyncSpeed();
    }

    private void Update()
    {
        if (target == null || agent == null || !agent.isOnNavMesh) return;

        SyncSpeed();

        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            repathTimer = repathInterval;
            agent.SetDestination(target.position);
        }
    }

    /// <summary>
    /// SpeedModule의 현재 배수를 매 프레임 NavMeshAgent 속도에 반영합니다.
    /// (신속 특성 등으로 런타임에 배수가 바뀌어도 즉시 따라가도록)
    /// </summary>
    private void SyncSpeed()
    {
        if (speedModule == null || agent == null) return;
        agent.speed = speedModule.CurrentSpeed;
    }

    public void SetTarget(Transform newTarget) => target = newTarget;
}
