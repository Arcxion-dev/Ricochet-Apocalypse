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

[Serializable]
public class ElementalWeakness
{
    public ElementType attackerElement;
    [Tooltip("이 속성으로 공격받았을 때 적용될 대미지 배수 (1 = 기본, 2 = 취약, 0.5 = 저항)")]
    public float damageMultiplier = 1f;
}

/// <summary>
/// 속성 모듈: 이 객체가 지닌 속성과, 공격 속성별 대미지 배수를 정의합니다.
/// 예) 자신의 속성이 Fire인 경우, Water 속성 공격에는 취약(배수 2.0)하도록 테이블에 등록.
/// </summary>
public class AttributeModule : MonoBehaviour
{
    [Header("이 객체의 속성")]
    [SerializeField] private ElementType selfElement = ElementType.None;

    [Header("공격 속성별 대미지 배수 (미등록 속성은 기본 1배)")]
    [SerializeField] private List<ElementalWeakness> weaknessTable = new List<ElementalWeakness>();

    public ElementType SelfElement => selfElement;

    public float GetDamageMultiplier(ElementType attackerElement)
    {
        for (int i = 0; i < weaknessTable.Count; i++)
        {
            if (weaknessTable[i].attackerElement == attackerElement)
                return weaknessTable[i].damageMultiplier;
        }
        return 1f;
    }

    public void SetSelfElement(ElementType element) => selfElement = element;
}
