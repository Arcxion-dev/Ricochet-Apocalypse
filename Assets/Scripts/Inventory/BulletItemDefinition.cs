using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 탄환 전용 아이템 정의. 인벤토리의 Ammo 버킷에 들어간다.
/// 설계: 기본 탄환을 제외한 강화 탄환은 "여러 능력이 부여된 고유 탄환"이라
/// 스택되지 않고 한 발 한 발이 개별 슬롯으로 존재한다(한 스테이지를 1~5발로 클리어).
///
/// - <see cref="isBasic"/> = true  : 기본 탄환(흔한 소모품, 스택 가능).
/// - <see cref="isBasic"/> = false : 강화 탄환(고유, maxStack 1). 여러 능력을 가진다.
/// - <see cref="bulletData"/> 를 연결하면 실제 탄환 스탯/효과(BulletSO)에서 능력 목록을 가져온다.
///   (BulletController 실제 발사는 총알 담당 파트와 연동 예정 — 여기서는 데이터 참조만.)
/// </summary>
[CreateAssetMenu(fileName = "New BulletItem", menuName = "Inventory/Bullet Item")]
public class BulletItemDefinition : ItemDefinition
{
    [Header("탄환")]
    [Tooltip("기본 탄환 여부. 기본은 스택 가능한 흔한 탄, 나머지는 고유 강화 탄(1발씩).")]
    public bool isBasic;

    [Tooltip("실제 탄환 스탯/효과 데이터(선택). 연결 시 능력 목록을 effects에서 가져온다.")]
    public BulletSO bulletData;

    [Tooltip("표시용 능력 라벨(선택). bulletData가 없을 때 또는 추가로 보여줄 능력 이름.")]
    public List<string> abilityLabels = new List<string>();

    private void OnValidate()
    {
        // 탄환은 항상 Ammo 버킷. 강화 탄환은 고유(1), 기본 탄환만 스택.
        category = ItemCategory.Ammo;
        maxStack = isBasic ? 999 : 1;
    }

    /// <summary>UI 표시용 능력 라벨(BulletSO 효과 이름 + 추가 라벨).</summary>
    public IReadOnlyList<string> GetAbilityLabels()
    {
        var labels = new List<string>();
        if (bulletData != null && bulletData.effects != null)
        {
            foreach (var effect in bulletData.effects)
            {
                if (effect != null) labels.Add(effect.name);
            }
        }
        if (abilityLabels != null) labels.AddRange(abilityLabels);
        return labels;
    }

    /// <summary>에셋 없이 코드로 탄환 정의를 만드는 런타임 헬퍼(디버그/테스트용).</summary>
    public static BulletItemDefinition CreateRuntime(string id, string displayName, bool isBasic, params string[] abilities)
    {
        var def = CreateInstance<BulletItemDefinition>();
        def.id = id;
        def.displayName = displayName;
        def.category = ItemCategory.Ammo;
        def.isBasic = isBasic;
        def.maxStack = isBasic ? 999 : 1;
        def.abilityLabels = new List<string>(abilities);
        def.name = displayName;
        return def;
    }
}
