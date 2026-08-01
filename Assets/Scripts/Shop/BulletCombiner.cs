using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 서로 다른 효과(BulletEffectSO)를 가진 두 강화 탄환을 합쳐 두 효과를 모두 지닌
/// 새 탄환을 만드는 순수 로직(MonoBehaviour 아님). 에셋을 만들지 않고
/// 런타임 인스턴스로 결과물을 생성한다(플레이어가 조합할 때마다 즉석 제작).
///
/// 어느 쪽을 먼저 골라도(A+B, B+A) 항상 같은 결과(id/표시명/효과 순서)가 나오도록
/// 효과 타입 이름(GetType().Name) 기준으로 정렬해서 합친다.
/// </summary>
public static class BulletCombiner
{
    /// <summary>두 탄환을 조합할 수 있는지 확인한다. 실패 시 reason에 사유.</summary>
    public static bool CanCombine(BulletItemDefinition a, BulletItemDefinition b, out string reason)
    {
        if (a == null || b == null) { reason = "탄환을 두 개 선택하세요"; return false; }
        if (a == b || (!string.IsNullOrEmpty(a.id) && a.id == b.id)) { reason = "같은 탄환은 조합할 수 없습니다"; return false; }
        if (a.isBasic || b.isBasic) { reason = "기본 탄환은 조합할 수 없습니다"; return false; }
        if (a.bulletData == null || b.bulletData == null) { reason = "탄환 데이터가 없습니다"; return false; }

        foreach (var effectA in a.bulletData.effects)
        {
            if (effectA == null) continue;
            foreach (var effectB in b.bulletData.effects)
            {
                if (effectB == null) continue;
                if (effectA.GetType() == effectB.GetType())
                {
                    reason = "이미 같은 효과를 가진 탄환입니다";
                    return false;
                }
            }
        }

        reason = null;
        return true;
    }

    /// <summary>두 탄환의 효과를 합쳐 새 탄환 정의를 반환한다. 실패 시 null.</summary>
    public static BulletItemDefinition Combine(BulletItemDefinition a, BulletItemDefinition b)
    {
        if (!CanCombine(a, b, out _)) return null;

        // 1) 효과 타입 -> (대표 효과 인스턴스, 표시 라벨) 매핑을 두 소스에서 모은다.
        var effectByType = new Dictionary<System.Type, BulletEffectSO>();
        var labelByType = new Dictionary<System.Type, string>();
        CollectEffectLabels(a, effectByType, labelByType);
        CollectEffectLabels(b, effectByType, labelByType);

        // 2) 타입 이름 기준 정렬 → 어느 쪽을 먼저 골라도 항상 같은 순서.
        var sortedTypes = new List<System.Type>(effectByType.Keys);
        sortedTypes.Sort((x, y) => string.CompareOrdinal(x.Name, y.Name));

        var mergedEffects = new List<BulletEffectSO>();
        var mergedLabels = new List<string>();
        var shortNames = new List<string>();
        foreach (var type in sortedTypes)
        {
            mergedEffects.Add(effectByType[type]);
            mergedLabels.Add(labelByType[type]);
            shortNames.Add(ShortTypeName(type).ToLowerInvariant());
        }

        var sourceA = a.bulletData;
        var sourceB = b.bulletData;

        var combinedData = ScriptableObject.CreateInstance<BulletSO>();
        combinedData.speed = Mathf.Max(sourceA.speed, sourceB.speed);
        combinedData.damage = Mathf.Max(sourceA.damage, sourceB.damage);
        combinedData.lifeTime = Mathf.Max(sourceA.lifeTime, sourceB.lifeTime);
        combinedData.maxBounceCount = Mathf.Max(sourceA.maxBounceCount, sourceB.maxBounceCount);
        combinedData.bulletSprite = sourceA.bulletSprite != null ? sourceA.bulletSprite : sourceB.bulletSprite;
        combinedData.hitVfxPrefab = sourceA.hitVfxPrefab != null ? sourceA.hitVfxPrefab : sourceB.hitVfxPrefab;
        combinedData.destroyVfxPrefab = sourceA.destroyVfxPrefab != null ? sourceA.destroyVfxPrefab : sourceB.destroyVfxPrefab;
        combinedData.effects = mergedEffects;

        string idSuffix = string.Join("_", shortNames.ToArray());
        combinedData.name = $"Bullet_{idSuffix}";

        var combinedItem = ScriptableObject.CreateInstance<BulletItemDefinition>();
        combinedItem.id = $"bullet_{idSuffix}";
        combinedItem.displayName = $"{string.Join("·", mergedLabels.ToArray())}탄";
        combinedItem.category = ItemCategory.Ammo;
        combinedItem.isBasic = false;
        combinedItem.maxStack = 1; // OnValidate는 CreateInstance에서 실행되지 않으므로 직접 설정.
        combinedItem.bulletData = combinedData;
        combinedItem.abilityLabels = mergedLabels;
        combinedItem.name = combinedItem.displayName;

        return combinedItem;
    }

    /// <summary>
    /// source가 지닌 각 효과(타입)에 대응하는 표시 라벨을 byType/byInstance에 채운다.
    /// abilityLabels 개수가 effects 개수와 어긋나 있어도 안전하게 타입명으로 대체한다.
    /// </summary>
    private static void CollectEffectLabels(BulletItemDefinition source,
        Dictionary<System.Type, BulletEffectSO> byInstance, Dictionary<System.Type, string> byType)
    {
        var effects = source.bulletData.effects;
        var labels = source.GetAbilityLabels();

        for (int i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            if (effect == null) continue;

            var type = effect.GetType();
            if (byType.ContainsKey(type)) continue; // 같은 소스 안의 중복 효과 방어.

            string label = (i < labels.Count && !string.IsNullOrEmpty(labels[i])) ? labels[i] : ShortTypeName(type);
            byType[type] = label;
            byInstance[type] = effect;
        }
    }

    private static string ShortTypeName(System.Type type)
    {
        const string suffix = "EffectSO";
        return type.Name.EndsWith(suffix) ? type.Name.Substring(0, type.Name.Length - suffix.Length) : type.Name;
    }
}
