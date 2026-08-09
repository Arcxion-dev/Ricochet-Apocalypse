using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>상점 좌측 "구매" 목록의 한 행(프리팹). <see cref="ShopUI"/>가 카탈로그 수만큼 생성한다.</summary>
public class ShopBuyRowView : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _name;
    [SerializeField] private TMP_Text _desc;
    [SerializeField] private TMP_Text _price;
    [SerializeField] private TMP_Text _owned;
    [SerializeField] private Button _buyButton;

    public void Setup(ItemDefinition def, int owned, Action onBuy)
    {
        if (def == null) return;
        if (_name != null) _name.text = def.ResolvedName;
        if (_desc != null) _desc.text = string.IsNullOrEmpty(def.description) ? def.category.ToKorean() : def.description;
        if (_price != null) _price.text = def.shopPrice + " G";
        if (_owned != null) _owned.text = "보유 " + owned;
        if (_icon != null)
        {
            if (def.icon != null) { _icon.sprite = def.icon; _icon.color = Color.white; }
            else { _icon.sprite = null; _icon.color = CategoryColor(def.category); }
        }
        if (_buyButton != null)
        {
            _buyButton.onClick.RemoveAllListeners();
            if (onBuy != null) _buyButton.onClick.AddListener(() => onBuy());
        }
    }

    private static Color CategoryColor(ItemCategory c)
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
