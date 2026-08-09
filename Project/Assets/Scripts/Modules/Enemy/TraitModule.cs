using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Trait
{
    public string traitName;
    [TextArea] public string description;
}

/// <summary>
/// 특성 모듈: 임의로 설정한 특성(Trait)들을 붙이는 모듈입니다.
/// 한 객체가 여러 특성을 가질 수 있으므로 리스트로 관리합니다.
/// </summary>
public class TraitModule : MonoBehaviour
{
    [SerializeField] private List<Trait> traits = new List<Trait>();

    public IReadOnlyList<Trait> Traits => traits;

    public void AddTrait(Trait trait) => traits.Add(trait);
    public void AddTrait(string traitName, string description = "") => traits.Add(new Trait { traitName = traitName, description = description });
    public bool HasTrait(string traitName) => traits.Exists(t => t.traitName == traitName);
}
