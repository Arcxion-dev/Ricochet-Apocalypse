using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 버튼 하나하나에 붙는 촉감. 누르면 살짝 눌리고, 떼면 통통 튀며 돌아온다.
/// 마우스 오버(데스크톱)에서는 아주 살짝 커진다 — 터치에서는 자연히 무시된다.
///
/// Button의 ColorTint 전환과 겹치지 않게 <b>스케일만</b> 다룬다.
/// 비활성(interactable=false) 버튼은 반응하지 않는다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class UIButtonMotion : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField, Tooltip("눌렸을 때 크기 배율")] private float _pressedScale = 0.94f;
    [SerializeField, Tooltip("마우스 오버 시 크기 배율(터치에서는 무시됨)")] private float _hoverScale = 1.03f;
    [SerializeField, Tooltip("클릭이 실제로 발생했을 때 한 번 튕긴다")] private bool _punchOnClick = true;

    private RectTransform _rt;
    private Selectable _selectable;
    private bool _hovered;
    private bool _pressed;

    private bool Interactable => _selectable == null || _selectable.IsInteractable();

    private void Awake()
    {
        _rt = (RectTransform)transform;
        _selectable = GetComponent<Selectable>();
    }

    private void OnDisable()
    {
        // 눌린 채로 비활성화되면 크기가 어긋난 채 남는다.
        DOTween.Kill(_rt);
        _hovered = _pressed = false;
        if (_rt != null) _rt.localScale = Vector3.one;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        if (!_pressed) ScaleTo(Interactable ? _hoverScale : 1f, UIAnim.Fast, UIAnim.EaseIn);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        if (!_pressed) ScaleTo(1f, UIAnim.Fast, UIAnim.EaseIn);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!Interactable) return;
        _pressed = true;
        ScaleTo(_pressedScale, UIAnim.Fast * 0.6f, UIAnim.EaseOut);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_pressed) return;
        _pressed = false;
        ScaleTo(_hovered ? _hoverScale : 1f, UIAnim.Normal, UIAnim.EasePop);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_punchOnClick || !Interactable) return;
        // OnPointerUp이 걸어둔 복귀 트윈과 겹치지 않게 먼저 정리한다.
        DOTween.Kill(_rt);
        UIAnim.Punch(_rt, 0.10f, 0.24f);
    }

    private void ScaleTo(float scale, float duration, Ease ease)
    {
        if (_rt == null) return;
        DOTween.Kill(_rt);
        _rt.DOScale(scale, duration).SetEase(ease).SetUpdate(true).SetTarget(_rt).SetLink(gameObject);
    }
}
