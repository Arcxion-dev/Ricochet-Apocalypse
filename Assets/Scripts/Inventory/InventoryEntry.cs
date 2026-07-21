using System;
using UnityEngine;

/// <summary>
/// 인벤토리 한 슬롯. 아이템 정의 + 현재 수량을 들고 있다.
/// 스택 규칙(maxStack)에 맞춰 채우고 빼는 책임을 가진다.
/// </summary>
[Serializable]
public class InventoryEntry
{
    [SerializeField] private ItemDefinition _definition;
    [SerializeField] private int _quantity;

    public ItemDefinition Definition => _definition;
    public int Quantity => _quantity;

    /// <summary>이 슬롯에 더 채울 수 있는 최대치(스택 불가면 1).</summary>
    public int Capacity => _definition != null && _definition.IsStackable ? _definition.maxStack : 1;

    public bool IsFull => _quantity >= Capacity;
    public bool IsEmpty => _quantity <= 0;

    public InventoryEntry(ItemDefinition definition, int quantity = 0)
    {
        _definition = definition;
        _quantity = Mathf.Max(0, quantity);
    }

    /// <summary>이 슬롯에 최대한 채우고, 다 못 넣은 초과분을 반환한다.</summary>
    public int Fill(int amount)
    {
        if (amount <= 0 || _definition == null) return Mathf.Max(0, amount);
        int space = Capacity - _quantity;
        int added = Mathf.Clamp(amount, 0, space);
        _quantity += added;
        return amount - added;
    }

    /// <summary>이 슬롯에서 최대한 빼고, 실제로 뺀 수량을 반환한다.</summary>
    public int Take(int amount)
    {
        int removed = Mathf.Clamp(amount, 0, _quantity);
        _quantity -= removed;
        return removed;
    }
}
