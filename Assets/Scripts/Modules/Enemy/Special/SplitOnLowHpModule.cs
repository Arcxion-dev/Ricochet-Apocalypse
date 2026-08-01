using UnityEngine;

/// <summary>
/// 분열 모듈: 체력이 일정 비율 이하로 떨어지면 1회, 더 낮은 체력을 가진 개체 여러 마리로
/// "부활"한다(원본은 파괴되고 자식들로 대체됨). 자식에게는 이 모듈을 제거해 무한 분열을 막는다.
/// </summary>
[RequireComponent(typeof(Entity))]
public class SplitOnLowHpModule : MonoBehaviour
{
    [Header("분열 설정")]
    [Tooltip("최대 체력 대비 이 비율 이하로 떨어지면 분열한다.")]
    [Range(0f, 1f)]
    [SerializeField] private float splitHpRatio = 0.3f;

    [Tooltip("분열로 생성될 자식 개체 수.")]
    [SerializeField] private int childCount = 2;

    [Tooltip("자식 개체 체력 = 분열 시점 체력 × 이 비율(최소 1).")]
    [Range(0f, 1f)]
    [SerializeField] private float childHealthRatio = 0.5f;

    [Tooltip("자식들이 스폰되는 반경.")]
    [SerializeField] private float spreadRadius = 0.6f;

    private Entity entity;
    private int maxHealthAtStart;
    private bool hasSplit;

    private void Awake()
    {
        entity = GetComponent<Entity>();
        maxHealthAtStart = entity.health;
        entity.OnDamaged += HandleDamaged;
    }

    private void OnDestroy()
    {
        if (entity != null) entity.OnDamaged -= HandleDamaged;
    }

    private void HandleDamaged(int amount)
    {
        if (hasSplit) return;
        if (entity.health <= 0) return; // 이 타격으로 죽었으면 분열 대신 그냥 사망
        if (entity.health > maxHealthAtStart * splitHpRatio) return;

        hasSplit = true;

        int childHealth = Mathf.Max(1, Mathf.RoundToInt(entity.health * childHealthRatio));

        for (int i = 0; i < childCount; i++)
        {
            Vector2 offset = Random.insideUnitCircle.normalized * spreadRadius;
            var child = Instantiate(gameObject, (Vector2)transform.position + offset, transform.rotation);

            var childSplitModule = child.GetComponent<SplitOnLowHpModule>();
            if (childSplitModule != null) Destroy(childSplitModule); // 무한 분열 방지

            var childEntity = child.GetComponent<Entity>();
            if (childEntity != null) childEntity.health = childHealth;
        }

        Debug.Log($"[SplitOnLowHpModule] {name} 저체력 분열 - {childCount}마리, 각 체력 {childHealth}");
        Destroy(gameObject);
    }
}
