using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// "장착 파츠" 칩 한 개(프리팹). 파츠 이름을 표시한다. 아이콘 필드가 없어 이름 중심.
///
/// 인벤토리 화면에서는 <see cref="Bind"/>로 동작을 물려 눌러서 조절할 수 있게 만든다:
/// 파츠 칩은 탭할 때마다 효과 ON/OFF가 뒤집히고(장착 목록에서 빼는 게 아니라 효과만 끈다),
/// 강선 칩은 탭할 때마다 한 단계 강화된다. 표시 전용으로 쓸 때는 <see cref="Set"/>만 부르면 된다.
/// </summary>
public class PartChipView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image _dot;
    [SerializeField] private TMP_Text _name;

    private Action _onClick;
    private Image _background;

    private void Awake() => _background = GetComponent<Image>();

    /// <summary>이름만 보여주는 기본 표시(비대화형).</summary>
    public void Set(string partName)
    {
        if (_name != null) _name.text = partName;
        _onClick = null;
    }

    /// <summary>
    /// 탭 가능한 칩으로 만든다.
    /// <paramref name="active"/>가 false면 꺼진 상태(흐리게 + 붉은 점)로 보여준다.
    /// </summary>
    public void Bind(string label, bool active, Color accent, Action onClick)
    {
        if (_name != null)
        {
            _name.text = label;
            _name.color = active ? UITheme.TextHi : UITheme.TextLo;
        }
        if (_dot != null) _dot.color = active ? accent : UITheme.Danger.A(0.55f);
        if (_background != null)
            _background.color = active ? UITheme.PanelRaised.A(0.95f) : UITheme.PanelBg.A(0.7f);

        _onClick = onClick;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_onClick == null) return;
        UIAnim.Punch(transform, 0.16f, 0.24f);
        _onClick();
    }
}
