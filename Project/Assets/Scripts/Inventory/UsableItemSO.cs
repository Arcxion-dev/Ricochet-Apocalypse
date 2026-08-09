using UnityEngine;

/// <summary>사용형(1회용) 아이템의 종류.</summary>
public enum UsableItemKind
{
    Grenade,    // 수류탄: 가까운 범위 선택타격(즉발 광역 피해)
    Airstrike,  // 폭격지원: 중~장거리 광범위 선택포격(넓은 광역 피해)
    Flashbang,  // 섬광탄: 근거리 적 이동 일시 저지(지속시간 동안 정지)
}

/// <summary>
/// 선택해서 사용하는 1회용 아이템 정의(수류탄/폭격지원/섬광탄).
/// 인벤토리의 <see cref="ItemCategory.Item"/> 버킷에 스택으로 담기고,
/// <see cref="PlayerShooter"/>가 F키 조준 → 클릭 지점에 효과를 적용한 뒤 1개 소비한다.
/// </summary>
[CreateAssetMenu(fileName = "New UsableItem", menuName = "Inventory/Usable Item")]
public class UsableItemSO : ItemDefinition
{
    [Header("사용 아이템")]
    [Tooltip("아이템 종류(효과 분기).")]
    public UsableItemKind kind = UsableItemKind.Grenade;

    [Tooltip("플레이어(발사점)로부터 조준 가능한 최대 사거리(월드 유닛). 이 밖은 지점이 클램프된다.")]
    public float maxRange = 5f;

    [Tooltip("효과 반경(AoE, 월드 유닛). 이 반경 안의 적에게 피해/저지가 적용된다.")]
    public float effectRadius = 2f;

    [Tooltip("피해량(수류탄/폭격지원). 섬광탄은 0.")]
    public float damage = 30f;

    [Tooltip("이동 저지 지속 시간(초). 섬광탄에서 사용.")]
    public float suppressDuration = 3f;

    private void OnValidate()
    {
        category = ItemCategory.Item; // 사용 아이템은 항상 일반 아이템 버킷.
        if (maxStack < 1) maxStack = 1;
    }

    /// <summary>에셋 없이 코드로 사용 아이템 정의를 만드는 런타임 헬퍼(디버그/폴백용).</summary>
    public static UsableItemSO CreateRuntime(string id, string displayName, UsableItemKind kind,
        float maxRange, float effectRadius, float damage, float suppressDuration, int maxStack = 9)
    {
        var d = CreateInstance<UsableItemSO>();
        d.id = id;
        d.displayName = displayName;
        d.category = ItemCategory.Item;
        d.kind = kind;
        d.maxRange = maxRange;
        d.effectRadius = effectRadius;
        d.damage = damage;
        d.suppressDuration = suppressDuration;
        d.maxStack = Mathf.Max(1, maxStack);
        d.name = displayName;
        return d;
    }

    /// <summary>UI 표시용 종류 라벨(한글).</summary>
    public string KindLabel
    {
        get
        {
            switch (kind)
            {
                case UsableItemKind.Grenade: return "수류탄";
                case UsableItemKind.Airstrike: return "폭격지원";
                case UsableItemKind.Flashbang: return "섬광탄";
                default: return kind.ToString();
            }
        }
    }
}
