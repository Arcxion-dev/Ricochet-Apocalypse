using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 화면 우하단 사용 아이템 퀵슬롯 한 칸(가로로 3칸).
///
/// 탭하면 <see cref="PlayerShooter.UseItemFromHud"/>로 그 아이템의 조준 모드에 들어가고,
/// 조준 중인 슬롯은 테두리가 살아나며 맥동한다. 같은 슬롯을 다시 탭하면 취소된다.
/// 비어 있는 칸은 눌러도 아무 일도 일어나지 않는다.
/// </summary>
public class QuickSlotView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image _frame;      // 슬롯 테두리(선택 시 강조)
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _count;
    [SerializeField] private TMP_Text _hotkey;  // "1" "2" "3"
    [SerializeField] private CanvasGroup _group;

    private PlayerShooter _shooter;
    private int _index = -1;
    private bool _hasItem;
    private bool _armed;

    private void Awake()
    {
        if (_group == null) _group = UIAnim.GroupOf(transform);
    }

    /// <summary>슬롯 내용을 갱신한다. <paramref name="definition"/>이 null이면 빈 칸.</summary>
    public void Set(PlayerShooter shooter, int index, UsableItemSO definition, int count, Sprite fallbackIcon)
    {
        _shooter = shooter;
        _index = index;
        _hasItem = definition != null;

        if (_hotkey != null) _hotkey.text = (index + 1).ToString();

        if (_icon != null)
        {
            _icon.enabled = _hasItem;
            if (_hasItem)
            {
                _icon.sprite = definition.icon != null ? definition.icon : fallbackIcon;
                _icon.color = TintFor(definition.kind);
            }
        }
        if (_count != null)
        {
            _count.enabled = _hasItem && count > 1;
            if (_hasItem) _count.text = count.ToString();
        }
        if (_group != null) _group.alpha = _hasItem ? 1f : 0.35f;
    }

    /// <summary>이 슬롯이 지금 조준 대기 중인지 표시한다(테두리 강조 + 맥동).</summary>
    public void SetArmed(bool armed)
    {
        if (_armed == armed) return;
        _armed = armed;

        if (_frame != null)
            UIAnim.ColorTo(_frame, armed ? UITheme.Gold : UITheme.Stroke, UIAnim.Fast);

        var rt = (RectTransform)transform;
        UIAnim.Stop(rt);
        if (armed)
        {
            // 사용 대기 중임을 알리는 느린 맥동. 취소하면 원래 크기로 돌아간다.
            rt.DOScale(1.08f, 0.45f).SetLoops(-1, LoopType.Yoyo)
              .SetEase(UIAnim.EaseSoft).SetUpdate(true).SetTarget(rt).SetLink(gameObject);
        }
        else
        {
            UIAnim.ScaleTo(rt, 1f, UIAnim.Fast);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_hasItem || _shooter == null)
        {
            UIAnim.Shake((RectTransform)transform, 6f, 0.2f);   // 빈 칸은 거절 피드백만.
            return;
        }
        UIAnim.Punch(transform, 0.14f, 0.24f);
        _shooter.UseItemFromHud(_index);
    }

    private static Color TintFor(UsableItemKind kind)
    {
        switch (kind)
        {
            case UsableItemKind.Grenade:   return UITheme.Explosive;
            case UsableItemKind.Airstrike: return UITheme.Fire;
            case UsableItemKind.Flashbang: return UITheme.Ice;
            default: return UITheme.TextHi;
        }
    }
}
