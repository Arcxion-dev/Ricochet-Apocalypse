using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Trait
{
    public string traitName;
    [TextArea] public string description;
    // TODO: 특성별 실제 효과를 여기에 연결 (예: ScriptableObject 참조, enum, UnityEvent 등)
    //       특성 종류가 늘어나면 이 클래스를 상속하거나 효과 enum을 추가해서 확장하세요.
}

/// <summary>
/// 특성 모듈: 임의로 설정한 특성(Trait)들을 붙이는 모듈입니다.
/// 한 객체가 여러 특성을 가질 수 있으므로 리스트로 관리합니다.
/// (Trait_1, Trait_2 ... 식으로 계속 추가 가능)
/// </summary>
public class TraitModule : MonoBehaviour
{
    [SerializeField] private List<Trait> traits = new List<Trait>();

    public IReadOnlyList<Trait> Traits => traits;

    public void AddTrait(Trait trait) => traits.Add(trait);
    public bool HasTrait(string traitName) => traits.Exists(t => t.traitName == traitName);
}
