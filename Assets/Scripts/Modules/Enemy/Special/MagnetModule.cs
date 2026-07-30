using UnityEngine;

/// <summary>
/// 중력/자력 모듈: 주변 범위 내 총알을 자신 쪽으로 끌어당긴다.
/// BulletController가 이미 공개해 둔 외력 훅(ApplyExternalForce)을 매 물리 프레임 호출한다.
/// </summary>
public class MagnetModule : MonoBehaviour
{
    [Header("중력자력 설정")]
    [SerializeField] private float pullRadius = 4f;
    [SerializeField] private float pullForce = 3f;

    [Tooltip("총알이 속한 레이어. 기본값은 프로젝트의 Bullet 레이어(9번).")]
    [SerializeField] private LayerMask bulletLayerMask = 1 << 9;

    private void FixedUpdate()
    {
        var hits = Physics2D.OverlapCircleAll(transform.position, pullRadius, bulletLayerMask);
        foreach (var hit in hits)
        {
            var bullet = hit.GetComponent<BulletController>();
            if (bullet == null) continue;

            Vector2 toSelf = (Vector2)transform.position - (Vector2)bullet.transform.position;
            if (toSelf.sqrMagnitude < 0.0001f) continue;

            bullet.ApplyExternalForce(toSelf.normalized * pullForce * Time.fixedDeltaTime);
        }
    }
}
