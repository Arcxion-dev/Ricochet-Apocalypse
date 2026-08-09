/// <summary>
/// 인벤토리 아이템의 대분류. 인벤토리는 이 분류별로 나뉘어 저장/정렬된다.
/// 새 분류가 필요하면 여기 값을 추가하면 되고(예: Consumable, Blueprint 등),
/// Inventory는 열거형 전체를 순회하므로 버킷이 자동으로 생긴다.
/// </summary>
public enum ItemCategory
{
    Item,      // 일반 아이템(소비/기타)
    GunPart,   // 총기 파츠
    Ammo,      // 탄환
    Currency,  // 재화
}

public static class ItemCategoryExtensions
{
    /// <summary>UI 표시용 한글 라벨.</summary>
    public static string ToKorean(this ItemCategory category)
    {
        switch (category)
        {
            case ItemCategory.Item: return "아이템";
            case ItemCategory.GunPart: return "총기 파츠";
            case ItemCategory.Ammo: return "탄환";
            case ItemCategory.Currency: return "재화";
            default: return category.ToString();
        }
    }
}
