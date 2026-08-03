using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD 좌하단 "사용 아이템" 슬롯 한 칸(프리팹). <see cref="HudController"/>가 보유 사용 아이템 수만큼
/// 인스턴스화하고 <see cref="Set"/>로 채운다. 현재 선택된 아이템은 시안 강조.
/// </summary>
public class ItemChipView : MonoBehaviour
{
    [SerializeField] private Image _bg;
    [SerializeField] private Outline _outline;
    [SerializeField] private TMP_Text _name;
    [SerializeField] private TMP_Text _count;

    public void Set(ItemDefinition def, int count, bool selected)
    {
        if (def == null) return;
        if (_name != null) { _name.text = def.ResolvedName; _name.color = selected ? UITheme.TextHi : new Color(0.718f,0.776f,0.831f); }
        if (_count != null) { _count.text = "×" + count; _count.color = selected ? new Color(0.498f,0.914f,1f) : new Color(0.424f,0.494f,0.557f); }
        if (_bg != null) _bg.color = selected ? new Color(0.071f,0.188f,0.235f,0.95f) : new Color(0.067f,0.102f,0.149f,0.95f);
        if (_outline != null) _outline.effectColor = selected ? new Color(0.208f,0.941f,1f,1f) : new Color(0.149f,0.220f,0.290f,0.7f);
    }
}
