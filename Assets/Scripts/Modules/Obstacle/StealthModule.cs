using UnityEngine;

/// <summary>
/// 은신 모듈: 적이 이 장애물 뒤에 위치할 때 플레이어(UI/레이더 등)에게
/// 해당 적의 정보를 노출하지 않도록 하는 모듈입니다.
/// </summary>
public class StealthModule : MonoBehaviour
{
    [Header("은신 판정 범위")]
    [SerializeField] private float concealRadius = 1.5f;

    public float ConcealRadius => concealRadius;

    /// <summary>
    /// 주어진 적 위치가 이 장애물의 은신 판정 범위 안에 있는지 여부.
    /// 실제로는 장애물과 플레이어 시야 사이의 각도/거리 등을 함께 고려해 고도화하세요.
    /// </summary>
    public bool IsConcealing(Transform enemyTransform)
    {
        if (enemyTransform == null) return false;
        return Vector3.Distance(transform.position, enemyTransform.position) <= concealRadius;
    }
}
