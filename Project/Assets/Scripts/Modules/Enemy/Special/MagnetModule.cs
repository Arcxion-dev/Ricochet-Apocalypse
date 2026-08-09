using UnityEngine;

/// <summary>
/// 중력/자력 모듈: 주변 범위 내 총알을 자신 쪽으로 끌어당긴다.
/// BulletController가 이미 공개해 둔 외력 훅(ApplyExternalForce)을 매 물리 프레임 호출한다.
/// </summary>
public class MagnetModule : MonoBehaviour
{
    [Header("중력자력 설정")]
    [SerializeField] private float pullRadius = 4f;

    [Tooltip("끌어당기는 세기(총알 속도에 비례한 조향력). 대략 이 값이 클수록 반경 통과 동안 더 크게 휘어든다. 3 근처면 눈에 띄게 휨.")]
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
            float dist = toSelf.magnitude;
            if (dist < 0.0001f) continue;

            // 고정 힘이면 빠른 총알(속도 ~15)은 거의 안 휘므로, 총알 속도에 비례한 "조향력"으로 준다.
            // 이렇게 하면 총알 속도와 무관하게 반경을 지나는 동안 일정한 각도로 휘어들어온다.
            // 가까울수록 강하게(중심 1.0 ~ 경계 0.4) 잡아당겨 중력우물 같은 느낌을 준다.
            float proximity = Mathf.Clamp01(1f - dist / pullRadius);
            float steer = pullForce * Mathf.Max(bullet.CurrentSpeed, 1f) * (0.4f + 0.6f * proximity);

            bullet.ApplyExternalForce(toSelf / dist * steer * Time.fixedDeltaTime);
        }
    }
}
