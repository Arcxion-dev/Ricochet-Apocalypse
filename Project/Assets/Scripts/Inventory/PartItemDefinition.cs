using UnityEngine;

/// <summary>
/// 무기 파츠를 인벤토리 아이템(<see cref="ItemCategory.GunPart"/>)으로 감싸는 정의.
/// 상점에서 구매하면 이 아이템이 인벤토리에 담기고(세이브가 id로 영속화),
/// 스테이지 로드 시 <see cref="PlayerShooter"/>가 GunPart 인벤토리를 훑어
/// 참조된 <see cref="part"/>를 장착한다(자동 장착·유지).
///
/// <see cref="WeaponPartSO"/>의 주석에서 예고된 "인벤토리(GunPart) 연동"의 실체.
/// </summary>
[CreateAssetMenu(fileName = "New PartItem", menuName = "Inventory/Part Item")]
public class PartItemDefinition : ItemDefinition
{
    [Header("파츠")]
    [Tooltip("이 아이템이 장착시키는 실제 무기 파츠 SO(Data/WeaponParts).")]
    public WeaponPartSO part;

    private void OnValidate()
    {
        category = ItemCategory.GunPart; // 파츠는 항상 GunPart 버킷.
        maxStack = 1;                    // 파츠는 유니크(1개만 보유/장착).
    }
}
