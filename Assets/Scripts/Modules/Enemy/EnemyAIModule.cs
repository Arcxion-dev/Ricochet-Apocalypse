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

    private NavMeshAgent agent;
    private GridModule gridModule; // 맵 경계(플레이어 방향 변)를 계산하기 위한 참조
    private SpeedModule speedModule;
    private float repathTimer;

    /// <summary>"Civilian" 태그가 붙은 대상은 스테이지 내내 제자리에 가만히 있고 절대 추격하지 않는다.</summary>
    private bool isCivilian;

    /// <summary>이동 저지(섬광탄/저지탄) 남은 시간(초). 0보다 크면 저지 중.</summary>
    private float suppressTimer;
    /// <summary>저지 중 이동속도 감소율(1 = 완전 정지).</summary>
    private float suppressSlow;

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

        gridModule = FindObjectOfType<GridModule>(); // 맵 경계 참조 캐싱(목표 면 계산용)
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

        if (target == null) return;

        // 스테이지 시작 직후에는 제자리 대기. 플레이어가 첫 발을 쏘고 추격 지연이 끝나야
        // GameManager.EnemiesCanChase가 켜지고 그때부터 목적지를 갱신(추격)한다.
        if (GameManager.Instance != null && !GameManager.Instance.EnemiesCanChase) return;

        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            repathTimer = repathInterval;
            agent.SetDestination(GetPlayerSideTarget());
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

    /// <summary>
    /// 플레이어를 직접 조준하는 대신, 맵 경계 사각형(충전포트면과 같은 네 변) 중
    /// 플레이어에서 가장 가까운 변을 적의 새 목표로 삼습니다.
    /// 적 자신의 직교축 좌표(좌우 변이면 y, 상하 변이면 x)는 그대로 유지해
    /// 각 적이 그 변 위에서 자기 위치에 맞는 지점까지만 직선으로 접근합니다.
    /// </summary>
    private Vector3 GetPlayerSideTarget()
    {
        // GridModule이 없으면(미연결/테스트 씨 등) 기존 방식대로 플레이어 좌표를 그대로 사용한다.
        if (gridModule == null) return target.position;

        Vector2 min = gridModule.Origin;
        Vector2 max = gridModule.Origin + gridModule.GridWorldSize;

        // 플레이어와 네 경계선 사이의 거리를 비교해 "플레이어가 속한 변"을 찾는다.
        float distLeft = target.position.x - min.x;
        float distRight = max.x - target.position.x;
        float distBottom = target.position.y - min.y;
        float distTop = max.y - target.position.y;

        float minDist = Mathf.Min(Mathf.Min(distLeft, distRight), Mathf.Min(distBottom, distTop));

        Vector3 myPos = transform.position;

        if (minDist == distLeft)
            return new Vector3(min.x, Mathf.Clamp(myPos.y, min.y, max.y), myPos.z);
        if (minDist == distRight)
            return new Vector3(max.x, Mathf.Clamp(myPos.y, min.y, max.y), myPos.z);
        if (minDist == distBottom)
            return new Vector3(Mathf.Clamp(myPos.x, min.x, max.x), min.y, myPos.z);

        // top
        return new Vector3(Mathf.Clamp(myPos.x, min.x, max.x), max.y, myPos.z);
    }

        public void SetTarget(Transform newTarget) => target = newTarget;
}
