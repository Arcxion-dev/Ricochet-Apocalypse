using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 스테이지 클리어 창의 "획득 아이템" 카드 한 장(프리팹). <see cref="StageClearUI"/>가
/// 드랍 결과 개수만큼 인스턴스화하고 <see cref="Set"/>로 내용을 채운다.
///
/// 등급 색: 총기 파츠(<see cref="ItemCategory.GunPart"/>)는 RARE(보라), 그 외는 COMMON.
/// 아이템에 아이콘이 있으면 표시하고, 없으면 카테고리 색의 사각 아이콘으로 대체한다.
/// </summary>
public class DropCardView : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _typeText;
    [SerializeField] private TMP_Text _rarityText;
    [SerializeField] private Outline _outline;

    public void Set(ItemDefinition item, int quantity)
    {
        if (item == null) return;

        bool rare = item.category == ItemCategory.GunPart;
        Color col = ColorFor(item.category);

        if (_nameText != null) _nameText.text = item.ResolvedName;
        if (_typeText != null)
            _typeText.text = rare ? item.category.ToKorean() : $"{item.category.ToKorean()}  ×{quantity}";
        if (_rarityText != null)
        {
            _rarityText.text = rare ? "RARE" : "COMMON";
            _rarityText.color = rare ? col : new Color(0.55f, 0.63f, 0.69f);
        }
        if (_icon != null)
        {
            if (item.icon != null) { _icon.sprite = item.icon; _icon.color = Color.white; }
            else { _icon.sprite = null; _icon.color = col; }
        }
        if (_outline != null)
            _outline.effectColor = rare ? new Color(col.r, col.g, col.b, 1f) : new Color(0.137f, 0.204f, 0.282f, 0.7f);
    }

    private static Color ColorFor(ItemCategory c)
    {
        switch (c)
        {
            case ItemCategory.Ammo: return new Color(0.435f, 0.914f, 1f);     // 시안
            case ItemCategory.GunPart: return new Color(0.690f, 0.482f, 1f);  // 보라(RARE)
            case ItemCategory.Currency: return new Color(1f, 0.761f, 0.294f);  // 골드
            default: return new Color(0.275f, 0.890f, 0.608f);                // 초록(일반)
        }
    }
}
