using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이펙트가 붙은 탄환(BulletItemDefinition)에 속성(ElementType)을 합쳐
/// 새 고유 탄환을 만드는 순수 로직(MonoBehaviour 아님). 에셋을 만들지 않고
/// 런타임 인스턴스로 결과물을 생성한다(플레이어가 조합할 때마다 즉석 제작).
/// </summary>
public static class BulletCombiner
{
    /// <summary>
    /// 원본 탄환에 속성을 합쳐 새 탄환 정의를 반환한다. 실패 시 null.
    /// 이미 속성이 있는 탄환은 덮어쓰기 방지를 위해 조합할 수 없다.
    /// </summary>
    public static BulletItemDefinition Combine(BulletItemDefinition source, ElementType element)
    {
        if (source == null || source.bulletData == null) return null;
        if (element == ElementType.None) return null;
        if (source.bulletData.element != ElementType.None) return null;

        var sourceData = source.bulletData;
        var combinedData = ScriptableObject.CreateInstance<BulletSO>();
        combinedData.speed = sourceData.speed;
        combinedData.damage = sourceData.damage;
        combinedData.lifeTime = sourceData.lifeTime;
        combinedData.maxBounceCount = sourceData.maxBounceCount;
        combinedData.element = element;
        combinedData.bulletSprite = sourceData.bulletSprite;
        combinedData.hitVfxPrefab = sourceData.hitVfxPrefab;
        combinedData.destroyVfxPrefab = sourceData.destroyVfxPrefab;
        combinedData.effects = new List<BulletEffectSO>(sourceData.effects);
        combinedData.name = $"{sourceData.name}_{element}";

        var combinedItem = ScriptableObject.CreateInstance<BulletItemDefinition>();
        combinedItem.id = $"{source.id}_{element}";
        combinedItem.displayName = $"{source.ResolvedName}·{element.ToKorean()}";
        combinedItem.category = ItemCategory.Ammo;
        combinedItem.isBasic = false;
        combinedItem.maxStack = 1;
        combinedItem.bulletData = combinedData;
        combinedItem.abilityLabels = new List<string>(source.GetAbilityLabels()) { element.ToKorean() };
        combinedItem.name = combinedItem.displayName;

        return combinedItem;
    }
}
