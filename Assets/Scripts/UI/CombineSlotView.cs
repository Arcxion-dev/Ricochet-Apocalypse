using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>상점 조합 패널의 슬롯 한 칸(① / ② / 결과). 선택된 탄환을 표시하거나 비어있음을 표시한다.</summary>
public class CombineSlotView : MonoBehaviour
{
    [SerializeField] private Image _bg;
    [SerializeField] private Outline _outline;
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _name;
    [SerializeField] private TMP_Text _tag;

    /// <summary>선택된 탄환 표시. def가 null이면 빈 슬롯.</summary>
    public void Set(BulletItemDefinition def)
    {
        if (def == null) { Clear(); return; }
        var abilities = def.GetAbilityLabels();
        string tag = abilities != null && abilities.Count > 0 ? abilities[0] : "";
        Color col = UITheme.AbilityColor(tag);
        if (_icon != null) { _icon.enabled = true; _icon.color = col; }
        if (_name != null) { _name.text = def.ResolvedName; _name.color = UITheme.TextHi; }
        if (_tag != null) { _tag.text = tag; _tag.color = col; }
        if (_bg != null) _bg.color = new Color(0.055f,0.094f,0.149f,0.92f);
        if (_outline != null) _outline.effectColor = col;
    }

    public void Clear()
    {
        if (_icon != null) _icon.enabled = false;
        if (_name != null) { _name.text = "선택"; _name.color = UITheme.TextLo; }
        if (_tag != null) _tag.text = "";
        if (_bg != null) _bg.color = new Color(0.055f,0.094f,0.149f,0.7f);
        if (_outline != null) _outline.effectColor = new Color(0.173f,0.243f,0.314f,0.7f);
    }
}
