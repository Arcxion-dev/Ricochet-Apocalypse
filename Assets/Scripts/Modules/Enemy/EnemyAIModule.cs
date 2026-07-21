using UnityEngine;

/// <summary>
/// AI 모듈: 막힌 장애물을 피해 플레이어에게 최단 경로로 도달하는 경로탐색을 담당합니다.
/// 실제 경로탐색(A*, NavMesh 등) 구현은 추후 채워 넣을 예정이며,
/// 현재는 SpeedModule과 연동되는 뼈대만 잡아두었습니다.
/// </summary>
public class EnemyAIModule : MonoBehaviour
{
    [Header("타겟 / 탐색 설정")]
    [SerializeField] private Transform target; // 보통 플레이어
    [SerializeField] private LayerMask obstacleLayerMask;
    [SerializeField] private float repathInterval = 0.5f;

    private SpeedModule speedModule;
    private float repathTimer;

    private void Awake()
    {
        speedModule = GetComponent<SpeedModule>();
    }

    private void Update()
    {
        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            repathTimer = repathInterval;
            RecalculatePath();
        }

        MoveAlongPath();
    }

    private void RecalculatePath()
    {
        // TODO: obstacleLayerMask를 회피하며 target까지의 최단 경로를 계산 (A* / NavMesh / Flow Field 등)
    }

    private void MoveAlongPath()
    {
        if (target == null) return;
        float speed = speedModule != null ? speedModule.CurrentSpeed : 3f;
        // TODO: 계산된 경로(waypoints)를 따라 speed로 이동시키는 로직 구현
    }

    public void SetTarget(Transform newTarget) => target = newTarget;
}
