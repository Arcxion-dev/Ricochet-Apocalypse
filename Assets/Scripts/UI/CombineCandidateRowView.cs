using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>상점 우측 "보유 탄환"(조합 후보) 목록의 한 행(프리팹). 클릭하면 ①②로 선택된다.</summary>
public class CombineCandidateRowView : MonoBehaviour
{
    [SerializeField] private Image _bg;
    [SerializeField] private Outline _outline;
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _name;
    [SerializeField] private Image _tagBg;
    [SerializeField] private TMP_Text _tag;
    [SerializeField] private TMP_Text _owned;
    [SerializeField] private GameObject _badge;
    [SerializeField] private TMP_Text _badgeNum;
    [SerializeField] private Button _button;

    public void Setup(BulletItemDefinition def, int owned, string mark, Action onClick)
    {
        if (def == null) return;
        bool selected = !string.IsNullOrEmpty(mark);

        var abilities = def.GetAbilityLabels();
        string tag = abilities != null && abilities.Count > 0 ? abilities[0] : "";
        Color col = UITheme.AbilityColor(tag);

        if (_name != null) _name.text = def.ResolvedName;
        if (_icon != null) _icon.color = col;
        if (_tag != null) { _tag.text = tag; _tag.color = col; }
        if (_tagBg != null) _tagBg.color = col.A(0.18f);
        if (_owned != null) _owned.text = "보유 " + owned;
        if (_badge != null) _badge.SetActive(selected);
        if (selected && _badgeNum != null) _badgeNum.text = mark;

        if (_bg != null) _bg.color = selected ? new Color(0.133f,0.102f,0.055f,0.9f) : new Color(0.063f,0.102f,0.149f,0.9f);
        if (_outline != null) _outline.effectColor = selected ? UITheme.Gold : new Color(0.137f,0.204f,0.282f,0.7f);

        if (_button != null)
        {
            _button.onClick.RemoveAllListeners();
            if (onClick != null) _button.onClick.AddListener(() => onClick());
        }
    }
}
