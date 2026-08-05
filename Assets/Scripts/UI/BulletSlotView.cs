using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD 좌하단 탄환 목록의 한 행(프리팹). <see cref="HudController"/>가 선택 가능한 탄환 수만큼
/// 인스턴스화하고 <see cref="Set"/>로 채운다. 선택된 슬롯은 시안 강조 + ▶ 프리픽스.
/// </summary>
public class BulletSlotView : MonoBehaviour
{
    [SerializeField] private Image _bg;
    [SerializeField] private Outline _outline;
    [SerializeField] private Image _accent;
    [SerializeField] private Image _keyBg;
    [SerializeField] private TMP_Text _key;
    [SerializeField] private TMP_Text _name;
    [SerializeField] private GameObject _tag;
    [SerializeField] private Image _tagBg;
    [SerializeField] private TMP_Text _tagText;
    [SerializeField] private TMP_Text _count;

    public void Set(int keyNumber, BulletItemDefinition def, int count, bool selected)
    {
        if (def == null) return;

        if (_key != null) _key.text = keyNumber.ToString();
        if (_name != null) _name.text = (selected ? "▶ " : "") + def.ResolvedName;
        if (_count != null) _count.text = def.isBasic ? "×∞" : "×" + count;

        var abilities = def.GetAbilityLabels();
        string tag = abilities != null && abilities.Count > 0 ? abilities[0] : null;
        if (_tag != null) _tag.SetActive(!string.IsNullOrEmpty(tag));
        if (!string.IsNullOrEmpty(tag))
        {
            Color tc = TagColor(tag);
            if (_tagText != null) { _tagText.text = tag; _tagText.color = tc; }
            if (_tagBg != null) { _tagBg.color = new Color(tc.r, tc.g, tc.b, 0.18f); }
        }

        // 선택 강조.
        if (_bg != null) _bg.color = selected ? new Color(0.071f,0.188f,0.235f,0.9f) : new Color(0.063f,0.102f,0.149f,0.6f);
        if (_outline != null) _outline.effectColor = selected ? new Color(0.208f,0.941f,1f,1f) : new Color(0.137f,0.204f,0.282f,0.6f);
        if (_accent != null) _accent.color = selected ? UITheme.Cyan : new Color(0.173f,0.243f,0.314f);
        if (_key != null) _key.color = selected ? new Color(0.498f,0.914f,1f) : new Color(0.624f,0.698f,0.761f);
        if (_keyBg != null) _keyBg.color = new Color(0.106f,0.165f,0.227f);
        if (_name != null) _name.color = selected ? UITheme.TextHi : new Color(0.769f,0.824f,0.871f);
        if (_count != null) _count.color = selected ? UITheme.TextHi : new Color(0.553f,0.627f,0.690f);

        // 선택된 슬롯의 강조 바는 은은하게 맥동해서 "지금 이게 장전돼 있다"를 계속 알린다.
        if (_accent != null)
        {
            DOTween.Kill(_accent);
            if (selected)
            {
                _accent.DOFade(0.45f, 0.7f)
                       .SetEase(Ease.InOutSine)
                       .SetLoops(-1, LoopType.Yoyo)
                       .SetUpdate(true)
                       .SetTarget(_accent)
                       .SetLink(gameObject);
            }
        }
    }

    /// <summary>탄환 능력 라벨을 속성 색으로 매핑(대략).</summary>
    private static Color TagColor(string label)
    {
        if (label.Contains("화") || label.Contains("불")) return UITheme.Fire;
        if (label.Contains("폭")) return UITheme.Explosive;
        if (label.Contains("관통")) return UITheme.Ice;
        if (label.Contains("얼") || label.Contains("빙") || label.Contains("냉")) return UITheme.Ice;
        if (label.Contains("전")) return UITheme.Electric;
        if (label.Contains("유도") || label.Contains("분열") || label.Contains("중력")) return UITheme.Arcane;
        return UITheme.Cyan;
    }
}
