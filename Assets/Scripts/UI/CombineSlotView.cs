using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 상점 조합 패널의 슬롯 한 칸(① / ② / 결과). 선택된 탄환을 표시하거나 비어있음을 표시한다.
/// 화면에 계속 살아 있는 오브젝트라 상태가 바뀔 때 색이 툭 튀지 않게 트윈으로 넘긴다.
/// </summary>
public class CombineSlotView : MonoBehaviour
{
    [SerializeField] private Image _bg;
    [SerializeField] private Outline _outline;
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _name;
    [SerializeField] private TMP_Text _tag;

    private bool _filled;

    /// <summary>선택된 탄환 표시. def가 null이면 빈 슬롯.</summary>
    public void Set(BulletItemDefinition def)
    {
        if (def == null) { Clear(); return; }
        var abilities = def.GetAbilityLabels();
        string tag = abilities != null && abilities.Count > 0 ? abilities[0] : "";
        Color col = UITheme.AbilityColor(tag);

        bool wasEmpty = !_filled;
        _filled = true;

        if (_icon != null)
        {
            _icon.enabled = true;
            UIAnim.ColorTo(_icon, col);
            if (wasEmpty) UIAnim.PopIn(_icon.rectTransform);
        }
        if (_name != null) { _name.text = def.ResolvedName; UIAnim.ColorTo(_name, UITheme.TextHi); }
        if (_tag != null) { _tag.text = tag; UIAnim.ColorTo(_tag, col); }
        UIAnim.ColorTo(_bg, new Color(0.055f, 0.094f, 0.149f, 0.92f));
        UIAnim.ColorTo(_outline, col);

        // 빈 칸이 채워지는 순간만 한 번 튕긴다.
        if (wasEmpty) UIAnim.Punch(transform, 0.16f, 0.32f);
    }

    public void Clear()
    {
        bool wasFilled = _filled;
        _filled = false;

        if (_icon != null) _icon.enabled = false;
        if (_name != null) { _name.text = "선택"; UIAnim.ColorTo(_name, UITheme.TextLo); }
        if (_tag != null) _tag.text = "";
        UIAnim.ColorTo(_bg, new Color(0.055f, 0.094f, 0.149f, 0.7f));
        UIAnim.ColorTo(_outline, new Color(0.173f, 0.243f, 0.314f, 0.7f));

        if (wasFilled) UIAnim.Punch(transform, 0.10f, 0.24f);
    }
}
