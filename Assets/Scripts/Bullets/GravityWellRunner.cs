using System.Collections;
using UnityEngine;

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
            var hits = Physics2D.OverlapCircleAll(position, pullRadius, enemyLayerMask);
            foreach (var hit in hits)
            {
                var rb = hit.attachedRigidbody;
                if (rb != null)
                {
                    Vector2 toCenter = (position - (Vector2)hit.transform.position).normalized;
                    rb.MovePosition(rb.position + toCenter * pullForce * Time.deltaTime);
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
