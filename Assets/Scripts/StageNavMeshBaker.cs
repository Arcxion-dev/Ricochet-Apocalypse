using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 스테이지 씬이 로드될 때(씬 전환 포함) NavMesh를 **런타임에서 다시 굽는다**.
/// 스테이지마다 장애물 배치가 다르므로, 씬별로 미리 구운 에셋에 의존하지 않고
/// 현재 씬의 장애물(Physics Collider) 기준으로 그 자리에서 navmesh를 재생성한다.
///
/// NavMeshSurface2D(NavMeshPlus) 오브젝트에 붙인다. NavMeshPlus 어셈블리에 대한
/// 컴파일 의존을 피하려고 <c>BuildNavMesh()</c>는 리플렉션으로 호출한다.
///
/// 굽기 직후, 씬에 배치된 적 NavMeshAgent들이 새 navmesh 위에 확실히 얹히도록
/// 가장 가까운 지점으로 Warp 해준다(이전 navmesh 데이터가 교체되며 잠깐 이탈하는 것 방지).
/// </summary>
public class StageNavMeshBaker : MonoBehaviour
{
    [Tooltip("굽기 후 적 NavMeshAgent를 새 navmesh 위로 스냅할 최대 탐색 거리.")]
    [SerializeField] private float _agentSnapDistance = 2f;

    private Component _surface;
    private MethodInfo _buildMethod;

    private void Awake()
    {
        // 같은 오브젝트에서 NavMeshSurface(NavMeshPlus) 컴포넌트를 타입 이름으로 찾는다.
        foreach (var c in GetComponents<Component>())
        {
            if (c != null && c.GetType().Name == "NavMeshSurface")
            {
                _surface = c;
                _buildMethod = c.GetType().GetMethod("BuildNavMesh", BindingFlags.Public | BindingFlags.Instance);
                break;
            }
        }
    }

    private void Start()
    {
        Rebake();
        // Tilemap 벽 콜라이더는 씬 로드 직후 지오메트리 생성이 한 박자 늦을 수 있어,
        // 한 프레임(+물리 스텝) 뒤 한 번 더 구워 확실히 반영한다.
        StartCoroutine(RebakeNextFrame());
    }

    private System.Collections.IEnumerator RebakeNextFrame()
    {
        yield return null;
        yield return new WaitForFixedUpdate();
        Rebake();
    }

    /// <summary>현재 씬 장애물 기준으로 navmesh를 다시 굽고 적 에이전트를 스냅한다.</summary>
    public void Rebake()
    {
        if (_surface == null || _buildMethod == null)
        {
            Debug.LogWarning("[StageNavMeshBaker] NavMeshSurface(NavMeshPlus)를 찾지 못해 재베이크를 건너뜁니다.");
            return;
        }

        _buildMethod.Invoke(_surface, null); // NavMeshSurface.BuildNavMesh()
        SnapAgents();
        Debug.Log("[StageNavMeshBaker] 런타임 NavMesh 재베이크 완료");
    }

    private void SnapAgents()
    {
        var agents = Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
        foreach (var agent in agents)
        {
            if (agent == null || agent.isOnNavMesh) continue;
            if (NavMesh.SamplePosition(agent.transform.position, out var hit, _agentSnapDistance, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }
    }
}
