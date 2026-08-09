using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 중력자탄이 총알 소멸 후에도 "끌어모으기 -> 폭발" 시퀀스를 진행할 수 있도록
/// 별도의 임시 오브젝트에서 코루틴을 실행시키는 러너.
/// 총알(BulletController)은 Die() 시점에 Destroy되므로, 그 위에서 코루틴을 돌릴 수 없어
/// 이 전용 실행기를 그 자리에 스폰해서 사용합니다.
/// </summary>
public class GravityWellRunner : MonoBehaviour
{
    public void Run(Vector2 position, float pullRadius, float pullForce, float pullDuration, float explosionDamage, LayerMask enemyLayerMask)
    {
        StartCoroutine(Routine(position, pullRadius, pullForce, pullDuration, explosionDamage, enemyLayerMask));
    }

    private IEnumerator Routine(Vector2 position, float pullRadius, float pullForce, float pullDuration, float explosionDamage, LayerMask enemyLayerMask)
    {
        float elapsed = 0f;

        while (elapsed < pullDuration)
        {
            float remaining = pullDuration - elapsed;
            var hits = Physics2D.OverlapCircleAll(position, pullRadius, enemyLayerMask);
            foreach (var hit in hits)
            {
                // 적은 NavMeshAgent로 스스로 플레이어를 추격하므로, 단순 rb.MovePosition은
                // 다음 프레임 에이전트 이동에 덮여 끌려오지 않는다. 그래서 (1) AI를 저지(ISuppressible)해
                // 추격을 멈추고, (2) NavMeshAgent.Move로 네비메시 위에서 중심 쪽으로 실제로 끌어당긴다.
                hit.GetComponentInParent<ISuppressible>()?.ApplySuppression(remaining, 1f);

                Vector2 toCenter = position - (Vector2)hit.transform.position;
                Vector2 step = toCenter.normalized * pullForce * Time.deltaTime;

                var agent = hit.GetComponentInParent<NavMeshAgent>();
                if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                {
                    agent.Move(step);
                }
                else
                {
                    var rb = hit.attachedRigidbody;
                    if (rb != null) rb.MovePosition(rb.position + step);
                    else hit.transform.position += (Vector3)step;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        var finalHits = Physics2D.OverlapCircleAll(position, pullRadius, enemyLayerMask);
        foreach (var hit in finalHits)
        {
            BulletDamageDispatcher.ApplyDamage(hit, explosionDamage, "중력자탄");
        }

        Debug.Log($"[중력자탄] 위치 {position}에서 폭발, 대상 {finalHits.Length}기 데미지 {explosionDamage} 적용");

        Destroy(gameObject);
    }
}
