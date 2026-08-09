using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공격 탄환이 지닌 특수 효과 종류. Assets/Data/BulletEffects의 BulletEffectSO 9종과 1:1로 대응합니다
/// (화염/물/바람 같은 원소 개념은 이 게임에 없음 — "속성"은 철갑탄/폭발탄 같은 탄환 효과를 뜻합니다).
/// </summary>
public enum BulletAttackAttribute
{
    None,
    ArmorPiercing,
    Explosive,
    Split,
    Suppression,
    Homing,
    Gravity,
    ChainLightning,
    Burn,
    Frost
}

[Serializable]
public class ElementalWeakness
{
    public BulletAttackAttribute attackerAttribute;
    [Tooltip("이 효과를 가진 탄환의 직격/폭발 대미지에 적용될 배수 (1 = 기본, 2 = 취약, 0.5 = 저항)")]
    public float damageMultiplier = 1f;

    [Tooltip("이 효과가 만든 장판(화상탄/냉기탄) 틱 대미지에 적용될 별도 배수. 직격과 장판의 취약도가 다를 때(예: 소환/시체산 몹은 장판에만 취약) 사용")]
    public float zoneTickMultiplier = 1f;

    [Tooltip("이 효과가 만든 장판에 닿으면 남은 체력과 무관하게 즉사시킬지 여부 (예: 화염 몹이 화상 장판을 밟으면 한방)")]
    public bool oneShotZoneTick = false;
}

/// <summary>
/// 속성 모듈: 공격 탄환이 지닌 효과(철갑탄/폭발탄 등)별 대미지 배수를 정의합니다.
/// </summary>
public class AttributeModule : MonoBehaviour
{
    [Header("공격 효과별 대미지 배수 (아래 표에 없는 효과는 defaultMultiplier 적용)")]
    [SerializeField] private List<ElementalWeakness> weaknessTable = new List<ElementalWeakness>();

    [Tooltip("weaknessTable에 등록되지 않은 효과로 공격받았을 때 적용될 기본 배수")]
    [SerializeField] private float defaultMultiplier = 1f;

    /// <summary>
    /// bulletData가 지닌 효과들 중 weaknessTable에 등록된 항목을 모두 찾아 배수를 곱해 반환합니다.
    /// 하나도 매칭되지 않으면(효과 없는 기본탄 등) defaultMultiplier를 반환합니다.
    /// bulletData가 null이면(총알이 아닌 광역 아이템 등) 효과 없음으로 취급합니다.
    /// </summary>
    public float GetDamageMultiplier(BulletSO bulletData)
    {
        float multiplier = 1f;
        bool matchedAny = false;

        for (int i = 0; i < weaknessTable.Count; i++)
        {
            if (!BulletHasAttribute(bulletData, weaknessTable[i].attackerAttribute)) continue;
            multiplier *= weaknessTable[i].damageMultiplier;
            matchedAny = true;
        }

        return matchedAny ? multiplier : defaultMultiplier;
    }

    public void SetDefaultMultiplier(float value) => defaultMultiplier = value;

    public void AddWeakness(BulletAttackAttribute attribute, float multiplier)
    {
        weaknessTable.Add(new ElementalWeakness { attackerAttribute = attribute, damageMultiplier = multiplier });
    }

    /// <summary>
    /// 이 효과(예: 화상탄/냉기탄)가 만든 장판의 틱 대미지에 적용할 배수. weaknessTable에 없으면 1(기본).
    /// </summary>
    public float GetZoneTickMultiplier(BulletAttackAttribute attribute)
    {
        for (int i = 0; i < weaknessTable.Count; i++)
        {
            if (weaknessTable[i].attackerAttribute == attribute) return weaknessTable[i].zoneTickMultiplier;
        }
        return 1f;
    }

    /// <summary>이 효과가 만든 장판에 닿으면 즉사해야 하는지(자기 속성과 일치하는 장판인지) 여부.</summary>
    public bool ShouldOneShotZoneTick(BulletAttackAttribute attribute)
    {
        for (int i = 0; i < weaknessTable.Count; i++)
        {
            if (weaknessTable[i].attackerAttribute == attribute) return weaknessTable[i].oneShotZoneTick;
        }
        return false;
    }

    private static bool BulletHasAttribute(BulletSO bulletData, BulletAttackAttribute attribute)
    {
        if (attribute == BulletAttackAttribute.None)
            return bulletData == null || bulletData.effects == null || bulletData.effects.Count == 0;

        if (bulletData == null) return false;

        switch (attribute)
        {
            case BulletAttackAttribute.ArmorPiercing: return bulletData.HasEffect<ArmorPiercingEffectSO>();
            case BulletAttackAttribute.Explosive: return bulletData.HasEffect<ExplosiveEffectSO>();
            case BulletAttackAttribute.Split: return bulletData.HasEffect<SplitEffectSO>();
            case BulletAttackAttribute.Suppression: return bulletData.HasEffect<SuppressionEffectSO>();
            case BulletAttackAttribute.Homing: return bulletData.HasEffect<HomingEffectSO>();
            case BulletAttackAttribute.Gravity: return bulletData.HasEffect<GravityEffectSO>();
            case BulletAttackAttribute.ChainLightning: return bulletData.HasEffect<ChainLightningEffectSO>();
            case BulletAttackAttribute.Burn: return bulletData.HasEffect<BurnEffectSO>();
            case BulletAttackAttribute.Frost: return bulletData.HasEffect<FrostEffectSO>();
            default: return false;
        }
    }
}
