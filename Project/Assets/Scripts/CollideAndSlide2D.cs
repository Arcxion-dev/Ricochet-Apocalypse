using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 콜라이더 모양 그대로 진행 방향으로 "쓸어서(shape cast)" 옮기는 2D collide &amp; slide 헬퍼.
///
/// 이 프로젝트의 플레이어 이동과 적 넉백은 둘 다 Rigidbody2D의 물리 해석에 맡기지 않고 위치를
/// 직접 옮긴다(플레이어는 Kinematic MovePosition, 적 넉백은 NavMeshAgent를 잠시 끄고 Transform 이동).
/// 그래서 아무 보정 없이 옮기면 Wall 레이어 콜라이더를 그대로 통과해버린다.
/// 이 헬퍼는 옮기기 전에 이동 경로를 미리 캐스팅해서
///   1) 벽 바로 앞(skinWidth 만큼 여유)까지만 이동하고,
///   2) 남은 이동량은 벽면 접선 방향으로 흘려보내(미끄러짐) 모서리에 끼지 않게 한다.
///
/// 캐스팅 원점을 인자로 직접 받기 때문에, 물리 엔진에 아직 반영되지 않은 위치에서도 정확하다
/// (이 프로젝트는 Physics2D.autoSyncTransforms가 꺼져 있어 Transform만 옮긴 직후의
/// Collider2D.bounds / Physics2D.OverlapCollider 결과는 한 스텝 밀릴 수 있다).
/// </summary>
public static class CollideAndSlide2D
{
    /// <summary>한 번의 캐스트에서 받아올 최대 충돌 수. 벽 몇 장이면 충분하다.</summary>
    private const int MaxHits = 8;

    /// <summary>미끄러짐 재시도 횟수. 2회면 벽 하나 + 모서리(벽 두 장) 상황까지 처리된다.</summary>
    private const int MaxIterations = 3;

    /// <summary>한 번의 밀어내기에서 보정할 최대 거리(월드 유닛). 폭주 보정을 막는 안전장치.</summary>
    private const float MaxDepenetrationDistance = 2f;

    /// <summary>기본 여유 간격. <see cref="MinSkinWidth"/> 이상이어야 물리적으로도 완전히 떨어진다.</summary>
    private const float DefaultSkinWidth = 0.03f;

    /// <summary>
    /// 여유 간격의 하한. Physics2D는 두 콜라이더를 각각 contactOffset만큼 부풀려서 접촉을 판정하므로,
    /// 간격이 contactOffset의 2배보다 좁으면 눈으로는 떨어져 있어도 Physics2D.Distance가 "겹침"으로
    /// 보고한다(실측: 간격 0.019 → distance -0.0009). 그 위로 조금 더 띄운다.
    /// </summary>
    private static float MinSkinWidth => Physics2D.defaultContactOffset * 2f + 0.005f;

    private static readonly RaycastHit2D[] HitBuffer = new RaycastHit2D[MaxHits];
    private static readonly List<Collider2D> OverlapBuffer = new List<Collider2D>(MaxHits);

    /// <summary>Wall 레이어만 막도록 만든 표준 필터. 트리거(수풀 감지 영역 등)는 통과시킨다.</summary>
    public static ContactFilter2D CreateFilter(LayerMask layers)
    {
        var filter = new ContactFilter2D
        {
            useTriggers = false,
        };
        filter.SetLayerMask(layers);
        return filter;
    }

    /// <summary>
    /// 인스펙터에서 비워둔(0) 레이어 마스크를 Wall 레이어로 대체한다.
    /// 레이어 이름이 없는 프로젝트에서도 안전하도록 못 찾으면 0을 그대로 돌려준다.
    /// </summary>
    public static LayerMask ResolveDefaultWallMask(LayerMask configured)
    {
        if (configured.value != 0) return configured;

        int wallLayer = LayerMask.NameToLayer("Wall");
        if (wallLayer < 0) return 0;

        return 1 << wallLayer;
    }

    /// <summary>
    /// <paramref name="from"/>에서 <paramref name="delta"/>만큼 옮기되, 막히면 벽 앞에서 멈추고
    /// 남은 이동량은 벽을 따라 미끄러뜨린 최종 위치를 반환한다.
    /// </summary>
    /// <param name="collider">이동 주체의 콜라이더(모양/크기 기준). null이면 보정 없이 그대로 이동.</param>
    /// <param name="from">이동 시작 위치(콜라이더가 붙은 트랜스폼의 월드 위치).</param>
    /// <param name="delta">이번 스텝에 이동하려는 변위.</param>
    /// <param name="filter">막는 대상 필터(<see cref="CreateFilter"/> 참고).</param>
    /// <param name="skinWidth">벽에 완전히 달라붙지 않도록 남겨두는 여유 간격.</param>
    public static Vector2 Move(Collider2D collider, Vector2 from, Vector2 delta, ContactFilter2D filter, float skinWidth = DefaultSkinWidth)
    {
        if (collider == null) return from + delta;

        skinWidth = Mathf.Max(skinWidth, MinSkinWidth);

        // 콜라이더 중심은 트랜스폼 원점과 다를 수 있으므로(Collider2D.offset) 캐스트 원점에 더해준다.
        // bounds 대신 offset을 직접 변환해야 autoSyncTransforms가 꺼진 상태에서도 최신 값이 나온다.
        Vector2 centerOffset = collider.transform.TransformVector(collider.offset);
        Vector2 position = from;

        for (int i = 0; i < MaxIterations; i++)
        {
            float distance = delta.magnitude;
            if (distance <= Mathf.Epsilon) break;

            Vector2 direction = delta / distance;
            int count = ShapeCast(collider, position + centerOffset, direction, distance + skinWidth, filter);

            RaycastHit2D blocking = default;
            float nearest = float.PositiveInfinity;
            for (int h = 0; h < count; h++)
            {
                RaycastHit2D hit = HitBuffer[h];
                if (hit.collider == null) continue;
                if (hit.collider.transform.IsChildOf(collider.transform)) continue; // 자기 자신/자식은 무시
                if (Vector2.Dot(hit.normal, direction) >= 0f) continue;             // 이미 벗어나는 중인 면은 무시
                if (hit.distance >= nearest) continue;

                nearest = hit.distance;
                blocking = hit;
            }

            if (blocking.collider == null)
            {
                position += delta; // 경로에 벽이 없다 - 그대로 이동
                break;
            }

            float allowed = Mathf.Max(0f, nearest - skinWidth);
            position += direction * allowed;

            // 남은 이동량에서 벽면 법선 성분을 빼면 접선 성분만 남는다 = 벽을 따라 미끄러짐.
            Vector2 remaining = direction * (distance - allowed);
            delta = remaining - Vector2.Dot(remaining, blocking.normal) * blocking.normal;
        }

        return position;
    }

    /// <summary>
    /// 이미 벽 안에 파묻힌 상태라면 바깥으로 밀어낸 위치를 반환한다(아니면 현재 위치 그대로).
    ///
    /// <see cref="Move"/>만으로는 이 상황을 빠져나올 수 없다 - 시작부터 겹쳐 있으면 어느 방향으로
    /// 캐스팅해도 거리 0에서 막혀 영구히 갇힌다. 스폰 지점에 벽이 깔렸거나 이전 넉백이 벽 속으로
    /// 밀어 넣은 경우를 위한 구제 경로.
    /// </summary>
    /// <remarks>
    /// <see cref="Physics2D.Distance"/>는 콜라이더의 "현재 물리 포즈"를 쓰므로, 호출 시점에
    /// <paramref name="collider"/>의 실제 위치가 반영되어 있어야 한다(FixedUpdate 시작 시점이 안전).
    /// </remarks>
    public static Vector2 Depenetrate(Collider2D collider, Vector2 from, ContactFilter2D filter, float skinWidth = DefaultSkinWidth)
    {
        if (collider == null) return from;

        skinWidth = Mathf.Max(skinWidth, MinSkinWidth);

        OverlapBuffer.Clear();
        int count = Physics2D.OverlapCollider(collider, filter, OverlapBuffer);
        if (count <= 0) return from;

        Vector2 correction = Vector2.zero;
        for (int i = 0; i < count; i++)
        {
            Collider2D other = OverlapBuffer[i];
            if (other == null) continue;
            if (other.transform.IsChildOf(collider.transform)) continue;

            ColliderDistance2D separation = Physics2D.Distance(collider, other);
            if (!separation.isValid || separation.distance >= 0f) continue; // 겹치지 않으면 거리는 양수

            // normal은 이 콜라이더 -> 상대 콜라이더 방향이므로, 반대로 밀어야 빠져나온다.
            correction -= separation.normal * (Mathf.Abs(separation.distance) + skinWidth);
        }

        if (correction.sqrMagnitude > MaxDepenetrationDistance * MaxDepenetrationDistance)
        {
            correction = correction.normalized * MaxDepenetrationDistance;
        }

        return from + correction;
    }

    /// <summary>
    /// 콜라이더 종류에 맞는 shape cast를 원점 <paramref name="center"/>에서 쏜다.
    /// 크기는 Transform의 lossyScale을 곱해 월드 기준으로 맞춘다.
    /// </summary>
    private static int ShapeCast(Collider2D collider, Vector2 center, Vector2 direction, float distance, ContactFilter2D filter)
    {
        Transform t = collider.transform;
        Vector3 scale = t.lossyScale;
        float angle = t.eulerAngles.z;

        switch (collider)
        {
            case BoxCollider2D box:
            {
                var size = new Vector2(box.size.x * Mathf.Abs(scale.x), box.size.y * Mathf.Abs(scale.y));
                return Physics2D.BoxCast(center, size, angle, direction, filter, HitBuffer, distance);
            }
            case CircleCollider2D circle:
            {
                float radius = circle.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                return Physics2D.CircleCast(center, radius, direction, filter, HitBuffer, distance);
            }
            case CapsuleCollider2D capsule:
            {
                var size = new Vector2(capsule.size.x * Mathf.Abs(scale.x), capsule.size.y * Mathf.Abs(scale.y));
                return Physics2D.CapsuleCast(center, size, capsule.direction, angle, direction, filter, HitBuffer, distance);
            }
            default:
            {
                // 폴리곤/컴포지트 등은 정확한 스윕 대신 AABB로 근사한다.
                Vector2 size = collider.bounds.size;
                return Physics2D.BoxCast(center, size, 0f, direction, filter, HitBuffer, distance);
            }
        }
    }
}
