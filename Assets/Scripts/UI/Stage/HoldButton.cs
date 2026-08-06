using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 누르고 있는 동안 <see cref="IsHeld"/>가 true인 버튼. 좌/우 이동 화살표에 쓴다.
///
/// 값을 직접 <see cref="PlayerMovement"/>에 쓰지 않고 상태만 노출하는 이유:
/// 좌·우 두 버튼이 같은 입력 축을 각자 덮어쓰면 먼저 뗀 쪽이 나중 것을 지워버린다.
/// 합산은 <see cref="StageHud"/>가 한 곳에서 한다.
/// </summary>
public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Tooltip("눌린 동안 밝아지는 그래픽(보통 버튼 배경).")]
    [SerializeField] private Graphic _highlight;
    [Tooltip("눌렀을 때 살짝 줄어드는 대상. 비우면 이 오브젝트.")]
    [SerializeField] private RectTransform _scaleTarget;
    [SerializeField] private float _pressedScale = 0.9f;

    /// <summary>지금 눌려 있는지.</summary>
    public bool IsHeld { get; private set; }

    private Color _baseColor = Color.white;
    private bool _captured;

    private void Awake()
    {
        if (_scaleTarget == null) _scaleTarget = (RectTransform)transform;
        if (_highlight != null && !_captured) { _baseColor = _highlight.color; _captured = true; }
    }

    private void OnDisable() => Release();

    public void OnPointerDown(PointerEventData eventData)
    {
        IsHeld = true;
        if (_highlight != null) UIAnim.ColorTo(_highlight, Color.Lerp(_baseColor, UITheme.Cyan, 0.55f), UIAnim.Fast);
        UIAnim.ScaleTo(_scaleTarget, _pressedScale, UIAnim.Fast);
    }

    public void OnPointerUp(PointerEventData eventData) => Release();

    // 누른 채로 버튼 밖으로 손가락이 미끄러지면 이동을 멈춘다(계속 걸어가지 않도록).
    public void OnPointerExit(PointerEventData eventData) => Release();

    private void Release()
    {
        if (!IsHeld) return;
        IsHeld = false;
        if (_highlight != null) UIAnim.ColorTo(_highlight, _baseColor, UIAnim.Normal);
        UIAnim.ScaleTo(_scaleTarget, 1f, UIAnim.Normal);
    }
}
