using UnityEngine;

/// <summary>
/// 전역 인벤토리 접근점(싱글턴). 데이터는 순수 <see cref="global::Inventory"/> 객체가 들고 있고,
/// 이 매니저는 씬 전환에도 유지되며 어디서든 접근하도록 감싼다(GameManager와 동일한 패턴).
///
/// 씬에 수동으로 배치하지 않아도 되도록 <see cref="Bootstrap"/> 에서 자동 생성한다
/// → 어느 씬에서 Play를 눌러도 항상 존재(디버깅·확장 편의).
/// </summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    /// <summary>실제 데이터 컨테이너.</summary>
    public Inventory Inventory { get; private set; } = new Inventory();

    /// <summary>첫 씬 로드 전에 매니저가 없으면 하나 만들어 둔다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("InventoryManager");
        go.AddComponent<InventoryManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ───────────────────────── 편의 API ─────────────────────────

    /// <summary>아이템 추가(실제 넣은 수량 반환). 콘솔에 로그를 남겨 투입을 확인할 수 있다.</summary>
    public int Add(ItemDefinition definition, int amount = 1)
    {
        int added = Inventory.Add(definition, amount);
        if (added > 0 && definition != null)
        {
            Debug.Log($"[Inventory] +{added} {definition.ResolvedName} " +
                      $"({definition.category.ToKorean()}) → 보유 {Inventory.GetQuantity(definition)}");
        }
        return added;
    }

    /// <summary>아이템 제거(실제 뺀 수량 반환).</summary>
    public int Remove(ItemDefinition definition, int amount = 1)
    {
        int removed = Inventory.Remove(definition, amount);
        if (removed > 0 && definition != null)
        {
            Debug.Log($"[Inventory] -{removed} {definition.ResolvedName} " +
                      $"({definition.category.ToKorean()}) → 보유 {Inventory.GetQuantity(definition)}");
        }
        return removed;
    }

    /// <summary>전체 비우기.</summary>
    public void Clear()
    {
        Inventory.Clear();
        Debug.Log("[Inventory] 전체 비움");
    }
}
