using UnityEngine;

/// <summary>
/// 아이템 한 종류의 정적 데이터(무엇인가)를 정의하는 ScriptableObject.
/// 실제 아이템은 "Create > Inventory > Item Definition" 으로 에셋을 만들어 채운다.
/// (총알의 BulletSO와 동일한 데이터 주도 방식.)
///
/// - 인벤토리 저장/조회 키는 <see cref="id"/> (같은 id는 같은 아이템으로 취급, 스택 병합됨).
/// - <see cref="category"/> 로 인벤토리 버킷이 나뉜다.
/// - <see cref="maxStack"/> 이 1이면 스택 불가(파츠/유니크 아이템), 2 이상이면 합산(탄환/재화 등).
/// </summary>
[CreateAssetMenu(fileName = "New ItemDefinition", menuName = "Inventory/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    [Tooltip("고유 식별자(중복 금지). 저장/조회/스택 병합의 키")]
    public string id;

    [Tooltip("UI 표시 이름")]
    public string displayName;

    [Tooltip("분류(인벤토리 정렬 기준)")]
    public ItemCategory category = ItemCategory.Item;

    [Tooltip("아이콘(선택, UI용)")]
    public Sprite icon;

    [Min(1)]
    [Tooltip("한 슬롯에 쌓을 수 있는 최대 수량. 1이면 스택 불가(슬롯마다 1개)")]
    public int maxStack = 1;

    [TextArea]
    [Tooltip("설명(선택)")]
    public string description;

    /// <summary>2개 이상 한 슬롯에 쌓을 수 있는지 여부.</summary>
    public bool IsStackable => maxStack > 1;

    /// <summary>표시 이름이 비어 있으면 id를, 그것도 없으면 에셋명을 반환.</summary>
    public string ResolvedName =>
        !string.IsNullOrEmpty(displayName) ? displayName :
        !string.IsNullOrEmpty(id) ? id : name;

    /// <summary>
    /// 에셋 없이 코드에서 아이템 정의를 만드는 런타임 헬퍼(디버그/테스트/절차적 생성용).
    /// AssetDatabase에 저장되지 않는 인메모리 인스턴스를 반환한다.
    /// </summary>
    public static ItemDefinition CreateRuntime(string id, string displayName, ItemCategory category, int maxStack = 1)
    {
        var def = CreateInstance<ItemDefinition>();
        def.id = id;
        def.displayName = displayName;
        def.category = category;
        def.maxStack = Mathf.Max(1, maxStack);
        def.name = string.IsNullOrEmpty(displayName) ? id : displayName;
        return def;
    }
}
