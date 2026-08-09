using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지 클리어 보상 드랍테이블. 각 항목을 독립 확률로 굴려 나온 아이템을 지급한다.
/// "Create > Stage > Drop Table" 로 에셋을 만든 뒤, 스테이지별로
/// <c>Resources/DropTables/{씬이름}.asset</c> (없으면 <c>Resources/DropTables/Default.asset</c>)
/// 경로에 두면 <see cref="GameManager"/>가 클리어 시 자동으로 로드해 굴린다.
/// (재화 Gold도 하나의 ItemDefinition이므로 드랍 항목으로 넣을 수 있다.)
/// </summary>
[CreateAssetMenu(fileName = "New DropTable", menuName = "Stage/Drop Table")]
public class DropTableSO : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("드랍할 아이템 정의(탄환/사용아이템/재화 등 ItemDefinition).")]
        public ItemDefinition item;

        [Range(0f, 1f)]
        [Tooltip("이 항목이 드랍될 확률(0~1). 1이면 항상 드랍.")]
        public float chance = 1f;

        [Min(0)]
        [Tooltip("드랍 시 최소 수량.")]
        public int minQuantity = 1;

        [Min(0)]
        [Tooltip("드랍 시 최대 수량(min 이상).")]
        public int maxQuantity = 1;
    }

    [Tooltip("드랍 후보 목록. 각 항목을 독립적으로 확률 굴림한다.")]
    public List<Entry> entries = new List<Entry>();

    /// <summary>
    /// 드랍테이블을 굴려 결과 목록을 만든다(지급은 호출측에서). 각 항목을 독립 확률로 판정한다.
    /// </summary>
    public List<DropResult> Roll()
    {
        var results = new List<DropResult>();
        foreach (var e in entries)
        {
            if (e == null || e.item == null) continue;
            if (Random.value > e.chance) continue;

            int max = Mathf.Max(e.minQuantity, e.maxQuantity);
            int qty = Random.Range(e.minQuantity, max + 1); // maxQuantity 포함
            if (qty > 0) results.Add(new DropResult(e.item, qty));
        }
        return results;
    }
}

/// <summary>드랍테이블 굴림 결과 한 줄(아이템 + 수량).</summary>
public readonly struct DropResult
{
    public readonly ItemDefinition Item;
    public readonly int Quantity;

    public DropResult(ItemDefinition item, int quantity)
    {
        Item = item;
        Quantity = quantity;
    }
}
