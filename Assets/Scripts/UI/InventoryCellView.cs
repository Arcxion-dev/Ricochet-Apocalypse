using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 인벤토리 "보유 아이템" 그리드의 셀 한 칸(프리팹). 아이콘(없으면 카테고리 색) + 수량 + 이름.
/// <see cref="InventoryUI"/>가 보유 항목 수만큼 생성한다.
/// </summary>
public class InventoryCellView : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private Outline _outline;
    [SerializeField] private TMP_Text _count;
    [SerializeField] private TMP_Text _name;

    public void Set(ItemDefinition def, int quantity)
    {
        if (def == null) return;
        Color col = CategoryColor(def.category);
        if (_name != null) _name.text = def.ResolvedName;
        if (_count != null) _count.text = "×" + quantity;
        if (_icon != null)
        {
            if (def.icon != null) { _icon.sprite = def.icon; _icon.color = Color.white; }
            else { _icon.sprite = null; _icon.color = col; }
        }
        if (_outline != null) _outline.effectColor = new Color(col.r, col.g, col.b, 0.55f);
    }

    public static Color CategoryColor(ItemCategory c)
    {
        switch (c)
        {
            case ItemCategory.Ammo: return UITheme.Cyan;
            case ItemCategory.GunPart: return UITheme.Arcane;
            case ItemCategory.Currency: return UITheme.Gold;
            default: return UITheme.Poison;
        }
    }
}
