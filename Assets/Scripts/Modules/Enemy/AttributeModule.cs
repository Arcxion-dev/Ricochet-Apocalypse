using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공격/객체 속성. 필요에 따라 자유롭게 항목을 추가하세요.
/// </summary>
public enum ElementType
{
    None,
    Fire,
    Water,
    Wind,
    Earth,
    Electric,
    Ice
}

public static class ElementTypeExtensions
{
    /// <summary>UI 표시용 한글 라벨.</summary>
    public static string ToKorean(this ElementType element)
    {
        switch (element)
        {
            case ElementType.None: return "무속성";
            case ElementType.Fire: return "화염";
            case ElementType.Water: return "물";
            case ElementType.Wind: return "바람";
            case ElementType.Earth: return "대지";
            case ElementType.Electric: return "전기";
            case ElementType.Ice: return "냉기";
            default: return element.ToString();
        }
    }
}

[Serializable]
public class ElementalWeakness
{
    public ElementType attackerElement;
    [Tooltip("이 속성으로 공격받았을 때 적용될 대미지 배수 (1 = 기본, 2 = 취약, 0.5 = 저항)")]
    public float damageMultiplier = 1f;
}

/// <summary>
/// 속성 모듈: 이 객체가 지닌 속성과, 공격 속성별 대미지 배수를 정의합니다.
/// </summary>
public class AttributeModule : MonoBehaviour
{
    [Header("이 객체의 속성")]
    [SerializeField] private ElementType selfElement = ElementType.None;

    [Header("공격 속성별 대미지 배수 (아래 표에 없는 속성은 defaultMultiplier 적용)")]
    [SerializeField] private List<ElementalWeakness> weaknessTable = new List<ElementalWeakness>();

    [Tooltip("weaknessTable에 등록되지 않은 속성으로 공격받았을 때 적용될 기본 배수")]
    [SerializeField] private float defaultMultiplier = 1f;

    public ElementType SelfElement => selfElement;

public float GetDamageMultiplier(ElementType attackerElement)
    {
        for (int i = 0; i < weaknessTable.Count; i++)
        {
            if (weaknessTable[i].attackerElement == attackerElement)
                return weaknessTable[i].damageMultiplier;
        }
        return defaultMultiplier;
    }

    public void SetSelfElement(ElementType element) => selfElement = element;

public void SetDefaultMultiplier(float value) => defaultMultiplier = value;

    public void AddWeakness(ElementType element, float multiplier)
    {
        weaknessTable.Add(new ElementalWeakness { attackerElement = element, damageMultiplier = multiplier });
    }

}
