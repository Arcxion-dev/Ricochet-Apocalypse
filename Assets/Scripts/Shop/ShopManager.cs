using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상점 구매/조합 검증 로직을 담은 정적 유틸리티(SceneLoader와 동일한 패턴, MonoBehaviour 아님).
/// 재화는 Resources/Currency/Gold 에셋 하나를 공용으로 참조한다(GameManager 보상 지급과 동일 자산).
/// </summary>
public static class ShopManager
{
    /// <summary>탄환 조합에 드는 고정 비용(골드). 개별 아이템 가격과 달리 게임 규칙이라 상수로 둔다.</summary>
    public const int CombineCost = 30;

    private static ItemDefinition _gold;
    private static ItemDefinition Gold =>
        _gold != null ? _gold : (_gold = Resources.Load<ItemDefinition>("Currency/Gold"));

    /// <summary>현재 보유 골드.</summary>
    public static int CurrentGold =>
        InventoryManager.Instance != null && Gold != null
            ? InventoryManager.Instance.Inventory.GetQuantity(Gold)
            : 0;

    /// <summary>상점에서 판매 중인 아이템 목록(shopPrice가 0보다 큰 것만). BulletItems + UsableItems 리소스를 훑는다.</summary>
    public static List<ItemDefinition> GetCatalog()
    {
        var catalog = new List<ItemDefinition>();

        foreach (var bullet in Resources.LoadAll<BulletItemDefinition>("BulletItems"))
        {
            if (bullet != null && bullet.shopPrice > 0) catalog.Add(bullet);
        }
        foreach (var usable in Resources.LoadAll<UsableItemSO>("UsableItems"))
        {
            if (usable != null && usable.shopPrice > 0) catalog.Add(usable);
        }

        return catalog;
    }

    /// <summary>아이템을 구매한다. 골드가 부족하면 실패(reason에 사유).</summary>
    public static bool TryPurchase(ItemDefinition item, int quantity, out string reason)
    {
        if (item == null) { reason = "대상 없음"; return false; }
        if (quantity <= 0) { reason = "수량 오류"; return false; }
        if (Gold == null) { reason = "재화 정의를 찾을 수 없음"; return false; }
        if (InventoryManager.Instance == null) { reason = "인벤토리 없음"; return false; }

        int cost = item.shopPrice * quantity;
        if (CurrentGold < cost) { reason = $"골드 부족 (필요 {cost}, 보유 {CurrentGold})"; return false; }

        InventoryManager.Instance.Remove(Gold, cost);
        InventoryManager.Instance.Add(item, quantity);
        reason = null;
        return true;
    }

    /// <summary>서로 다른 효과를 가진 두 탄환을 합쳐 새 탄환을 만든다. 원본 각 1개 + 고정 비용을 소모한다.</summary>
    public static bool TryCombine(BulletItemDefinition a, BulletItemDefinition b, out string reason)
    {
        if (!BulletCombiner.CanCombine(a, b, out reason)) return false;
        if (Gold == null) { reason = "재화 정의를 찾을 수 없음"; return false; }
        if (InventoryManager.Instance == null) { reason = "인벤토리 없음"; return false; }
        if (CurrentGold < CombineCost) { reason = $"골드 부족 (필요 {CombineCost}, 보유 {CurrentGold})"; return false; }
        if (InventoryManager.Instance.Inventory.GetQuantity(a) <= 0) { reason = "보유하지 않은 탄환입니다"; return false; }
        if (InventoryManager.Instance.Inventory.GetQuantity(b) <= 0) { reason = "보유하지 않은 탄환입니다"; return false; }

        var combined = BulletCombiner.Combine(a, b);
        if (combined == null) { reason = "조합 실패"; return false; }

        InventoryManager.Instance.Remove(Gold, CombineCost);
        InventoryManager.Instance.Remove(a, 1);
        InventoryManager.Instance.Remove(b, 1);
        InventoryManager.Instance.Add(combined, 1);
        reason = null;
        return true;
    }
}
