using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인벤토리의 순수 데이터 계층(MonoBehaviour 아님 → 테스트/디버그 쉬움).
/// 아이템을 한 곳에 섞지 않고 <see cref="ItemCategory"/> 별 버킷에 나눠 담는다.
/// - 같은 id의 스택 가능한 아이템은 슬롯에 합산되고, 넘치면 새 슬롯이 생긴다.
/// - 스택 불가 아이템은 개수만큼 개별 슬롯으로 쌓인다.
/// 변경이 생기면 <see cref="Changed"/> 이벤트로 알린다(UI 갱신용).
/// </summary>
public class Inventory
{
    private readonly Dictionary<ItemCategory, List<InventoryEntry>> _buckets =
        new Dictionary<ItemCategory, List<InventoryEntry>>();

    /// <summary>인벤토리 내용이 바뀔 때마다 호출(추가/제거/비우기).</summary>
    public event Action Changed;

    public Inventory()
    {
        foreach (ItemCategory category in Enum.GetValues(typeof(ItemCategory)))
        {
            _buckets[category] = new List<InventoryEntry>();
        }
    }

    /// <summary>해당 분류의 슬롯 목록(읽기 전용).</summary>
    public IReadOnlyList<InventoryEntry> GetEntries(ItemCategory category) => _buckets[category];

    /// <summary>아이템을 추가한다. 반환값은 실제로 넣은 수량.</summary>
    public int Add(ItemDefinition definition, int amount = 1)
    {
        if (definition == null || amount <= 0) return 0;

        var list = _buckets[definition.category];
        int remaining = amount;

        // 1) 스택 가능하면 같은 id의 기존 슬롯부터 채운다.
        if (definition.IsStackable)
        {
            foreach (var entry in list)
            {
                if (!Matches(entry, definition)) continue;
                remaining = entry.Fill(remaining);
                if (remaining <= 0) break;
            }
        }

        // 2) 남은 수량은 새 슬롯으로.
        while (remaining > 0)
        {
            var entry = new InventoryEntry(definition, 0);
            remaining = entry.Fill(remaining);
            list.Add(entry);
        }

        Changed?.Invoke();
        return amount;
    }

    /// <summary>아이템을 제거한다. 반환값은 실제로 뺀 수량.</summary>
    public int Remove(ItemDefinition definition, int amount = 1)
    {
        if (definition == null || amount <= 0) return 0;

        var list = _buckets[definition.category];
        int remaining = amount;

        // 뒤에서부터(최근 슬롯부터) 뺀다.
        for (int i = list.Count - 1; i >= 0 && remaining > 0; i--)
        {
            if (!Matches(list[i], definition)) continue;
            remaining -= list[i].Take(remaining);
            if (list[i].IsEmpty) list.RemoveAt(i);
        }

        int removed = amount - remaining;
        if (removed > 0) Changed?.Invoke();
        return removed;
    }

    /// <summary>특정 아이템의 총 보유 수량.</summary>
    public int GetQuantity(ItemDefinition definition)
    {
        if (definition == null) return 0;
        int total = 0;
        foreach (var entry in _buckets[definition.category])
        {
            if (Matches(entry, definition)) total += entry.Quantity;
        }
        return total;
    }

    /// <summary>분류 전체의 총 수량(모든 슬롯 합).</summary>
    public int GetTotalCount(ItemCategory category)
    {
        int total = 0;
        foreach (var entry in _buckets[category]) total += entry.Quantity;
        return total;
    }

    /// <summary>분류의 슬롯 개수.</summary>
    public int GetSlotCount(ItemCategory category) => _buckets[category].Count;

    /// <summary>전부 비운다(디버그/스테이지 리셋용).</summary>
    public void Clear()
    {
        bool any = false;
        foreach (var list in _buckets.Values)
        {
            if (list.Count > 0) { list.Clear(); any = true; }
        }
        if (any) Changed?.Invoke();
    }

    /// <summary>슬롯이 이 정의와 같은 아이템인지(참조 또는 id 일치).</summary>
    private static bool Matches(InventoryEntry entry, ItemDefinition definition)
    {
        if (entry?.Definition == null) return false;
        if (entry.Definition == definition) return true;
        return !string.IsNullOrEmpty(definition.id) && entry.Definition.id == definition.id;
    }
}
