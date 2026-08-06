using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 화면 우하단의 잔탄 표시 + 탄환 선택기. 리볼버의 회전 실린더를 흉내낸다.
///
/// 구조: 가운데 큰 원(= 1번 슬롯, 다음에 발사될 탄) 하나를 작은 약실들이 둘러싼다.
/// 실린더 뒷판(<see cref="_rotor"/>)이 통째로 돌고, 12시 방향(공이 표시 아래)에 온 약실이
/// 가운데 슬롯으로 "장전"된다.
///
/// 조작:
/// - 실린더 가장자리를 잡고 <b>돌리면</b> 손가락 각도를 그대로 따라간다.
/// - 가운데 근처를 잡고 <b>위아래로 끌면</b> 세로 이동량이 회전으로 환산된다(위로 = 다음 탄).
/// - 약실을 그냥 <b>탭</b>하면 그 약실로 바로 돌아간다.
///
/// 손을 떼면 가장 가까운 약실로 스내핑한다. 이때
/// 1) OutBack 이징이 살짝 넘어갔다 돌아오며 톱니에 물리는 "찰칵" 느낌을 내고,
/// 2) 12시 약실의 탄이 가운데 큰 슬롯으로 빨려 들어오는 연출이 이어지고,
/// 3) 그 연출이 끝나는 순간 카메라를 한 번 친다(<see cref="HitFeedbackManager.Punch"/>).
///
/// 데이터는 소유하지 않는다. <see cref="PlayerShooter"/>의 탄환 목록을 읽고 선택만 되돌려준다.
/// </summary>
public class RevolverCylinderUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("루트")]
    [Tooltip("드래그 좌표 계산 기준. 비우면 이 오브젝트의 RectTransform.")]
    [SerializeField] private RectTransform _root;
    [Tooltip("실제로 회전하는 부분(뒷판 + 약실들).")]
    [SerializeField] private RectTransform _rotor;
    [Tooltip("실린더 뒤에 깔리는 글로우. 조작 중에 밝아진다.")]
    [SerializeField] private Image _glow;
    [Tooltip("조작 가능함을 알리는 파선 링. 천천히 자전한다.")]
    [SerializeField] private RectTransform _dashRing;

    [Header("약실 (12시부터 반시계 방향)")]
    [SerializeField] private CylinderChamberView[] _chambers;

    [Header("가운데 큰 슬롯 (1번 = 발사될 탄)")]
    [SerializeField] private RectTransform _mainSlot;
    [SerializeField] private Image _mainRing;
    [SerializeField] private RectTransform _mainIconRect;
    [SerializeField] private Image _mainIcon;
    [SerializeField] private TMP_Text _mainCount;
    [SerializeField] private TMP_Text _mainName;
    [SerializeField] private GameObject _emptyLabel;

    [Header("에셋")]
    [Tooltip("탄환 정의에 아이콘이 없을 때 쓸 기본 탄환 그림.")]
    [SerializeField] private Sprite _fallbackBulletIcon;

    [Header("조작감")]
    [Tooltip("약실 중심이 놓이는 반지름(px). 뒷판 스프라이트의 구멍 위치와 맞춰야 한다.")]
    [SerializeField] private float _orbitRadius = 123f;

    [Header("화면 가장자리 페이드")]
    [Tooltip("약실이 완전히 사라지는 가로 위치(실린더 중심 기준 오른쪽 +px). " +
             "'화면 오른쪽 끝까지 거리 - 약실 반지름'으로 잡아야 잘리기 직전에 사라진다.")]
    [SerializeField] private float _edgeVisibleX = 102f;
    [Tooltip("페이드가 진행되는 가로 거리(px). 클수록 더 일찍부터 서서히 흐려진다.")]
    [SerializeField] private float _edgeFadeWidth = 70f;
    [Tooltip("세로 드래그 모드에서 약실 한 칸을 넘기는 데 필요한 픽셀 거리.")]
    [SerializeField] private float _pixelsPerStep = 90f;
    [Tooltip("이 비율보다 중심에서 멀리 잡으면 '돌리기', 가까우면 '위아래로 끌기'가 된다.")]
    [Range(0.05f, 0.9f)]
    [SerializeField] private float _rotateGrabRatio = 0.34f;
    [Tooltip("탭으로 인정할 최대 이동 거리(px). 이보다 많이 움직이면 드래그로 본다.")]
    [SerializeField] private float _tapSlop = 14f;

    [Header("연출")]
    [SerializeField] private float _snapDuration = 0.3f;
    [SerializeField] private float _feedDuration = 0.24f;
    [Tooltip("장전이 끝나는 순간 카메라를 치는 세기(월드 유닛).")]
    [SerializeField] private float _shakeMagnitude = 0.16f;
    [SerializeField] private float _shakeDuration = 0.14f;
    [Tooltip("스내핑 순간 재생할 SFX id. SoundManager에 등록된 id를 넣는다. 비우면 재생하지 않는다.")]
    [SerializeField] private string _clickSfxId = "";

    private PlayerShooter _shooter;
    private float _rotorAngle;          // 누적 회전각(도). 한 바퀴를 넘어도 잘리지 않는다.
    private int _selected = -1;         // 현재 12시에 물려 있는 약실
    private int _filled;                // 탄이 들어 있는 약실 수(= 탄환 종류 수)
    private int _listSignature = int.MinValue;

    // 드래그 상태
    private bool _dragging;
    private bool _rotateMode;           // true=각도 추종, false=세로 이동 환산
    private float _lastPointerAngle;
    private Vector2 _pressPosition;

    private int ChamberCount => _chambers != null ? _chambers.Length : 0;
    private float StepAngle => ChamberCount > 0 ? 360f / ChamberCount : 360f;

    private void Awake()
    {
        if (_root == null) _root = (RectTransform)transform;
        LayoutChambers();
        ApplyRotation(_rotorAngle);   // 첫 프레임부터 가장자리 페이드가 맞게 보이도록.
    }

    private void OnEnable()
    {
        // 파선 링은 계속 천천히 돈다 — "돌릴 수 있는 물건"이라는 신호.
        if (_dashRing != null)
        {
            UIAnim.Stop(_dashRing);
            _dashRing.DOLocalRotate(new Vector3(0f, 0f, -360f), 18f, RotateMode.LocalAxisAdd)
                     .SetEase(Ease.Linear).SetLoops(-1).SetUpdate(true)
                     .SetTarget(_dashRing).SetLink(gameObject);
        }
    }

    private void Update()
    {
        if (_shooter == null)
        {
            _shooter = PlayerShooter.Active != null ? PlayerShooter.Active : FindObjectOfType<PlayerShooter>();
            if (_shooter == null) return;
        }

        int sig = Signature();
        if (sig == _listSignature) return;

        // 스내핑/장전 연출이 도는 중에 다시 그리면 가운데 아이콘이 연출보다 먼저 바뀌어 버린다.
        // 시그니처를 갱신하지 않고 넘겨서, 연출이 끝난 다음 프레임에 반영한다.
        if (IsAnimating()) return;

        _listSignature = sig;
        Refresh();
    }

    /// <summary>회전 스내핑 또는 장전 연출이 진행 중인지.</summary>
    private bool IsAnimating() =>
        DOTween.IsTweening(this) || (_mainIconRect != null && DOTween.IsTweening(_mainIconRect));

    // ───────────────────────── 배치 / 갱신 ─────────────────────────

    /// <summary>약실들을 12시부터 반시계 방향으로 원둘레에 늘어놓는다(뒷판 스프라이트의 구멍과 같은 규칙).</summary>
    private void LayoutChambers()
    {
        for (int i = 0; i < ChamberCount; i++)
        {
            if (_chambers[i] == null) continue;
            float a = (90f + i * StepAngle) * Mathf.Deg2Rad;
            _chambers[i].Rect.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * _orbitRadius;
        }
    }

    /// <summary>탄환 목록/선택이 바뀌었는지 판별하는 값.</summary>
    private int Signature()
    {
        int h = 17;
        var choices = _shooter.Choices;
        h = h * 31 + choices.Count;
        h = h * 31 + _shooter.SelectedIndex;
        foreach (var c in choices)
        {
            h = h * 31 + (c.Definition != null ? c.Definition.GetInstanceID() : 0);
            h = h * 31 + c.Count;
        }
        return h;
    }

    /// <summary>PlayerShooter의 탄환 목록을 약실에 옮겨 담고, 선택된 칸을 12시로 돌린다.</summary>
    private void Refresh()
    {
        var choices = _shooter.Choices;
        _filled = Mathf.Min(choices.Count, ChamberCount);

        for (int i = 0; i < ChamberCount; i++)
        {
            if (_chambers[i] == null) continue;
            if (i < _filled) _chambers[i].Set(choices[i].Definition, choices[i].Count, _fallbackBulletIcon);
            else _chambers[i].Set(null, 0, _fallbackBulletIcon);
        }

        if (_emptyLabel != null) _emptyLabel.SetActive(_filled == 0);

        int target = Mathf.Clamp(_shooter.SelectedIndex, 0, Mathf.Max(0, _filled - 1));
        if (_filled == 0)
        {
            _selected = -1;
            SetMainSlot(null, 0, animate: false);
            return;
        }

        if (target != _selected)
        {
            // 외부(숫자키 등)에서 선택이 바뀐 경우에도 실린더가 따라 돌고 같은 촉감을 준다.
            RotateToChamber(target, animate: true, feed: true);
            PlaySeatFeedback(true);
        }
        else
        {
            // 수량만 바뀐 경우: 돌리지 않고 숫자만 고친다.
            SetMainSlot(choices[target].Definition, choices[target].Count, animate: false);
        }
    }

    /// <summary>가운데 큰 슬롯의 표시를 갱신한다. <paramref name="animate"/>면 위에서 빨려 들어오는 장전 연출을 한다.</summary>
    private void SetMainSlot(BulletItemDefinition definition, int count, bool animate)
    {
        bool has = definition != null;

        if (_mainIcon != null)
        {
            _mainIcon.enabled = has;
            if (has)
            {
                _mainIcon.sprite = definition.icon != null ? definition.icon : _fallbackBulletIcon;
                _mainIcon.color = CylinderChamberView.BulletTint(definition);
            }
        }
        if (_mainCount != null)
        {
            _mainCount.enabled = has;
            if (has) _mainCount.text = "×" + count;
        }
        if (_mainName != null)
        {
            _mainName.enabled = has;
            if (has) _mainName.text = definition.ResolvedName;
        }
        if (_mainRing != null && has)
            _mainRing.color = Color.Lerp(Color.white, CylinderChamberView.BulletTint(definition), 0.45f);

        if (!animate || !has || _mainIconRect == null) return;

        // 12시 약실에서 가운데로 탄이 딸려 들어오는 연출.
        var g = UIAnim.GroupOf(_mainIconRect);
        UIAnim.Stop(_mainIconRect);
        UIAnim.Stop(g);

        _mainIconRect.anchoredPosition = new Vector2(0f, _orbitRadius * 0.85f);
        _mainIconRect.localScale = Vector3.one * 0.45f;
        g.alpha = 0f;

        var seq = DOTween.Sequence();
        seq.Join(_mainIconRect.DOAnchorPos(Vector2.zero, _feedDuration).SetEase(Ease.OutBack, 1.9f));
        seq.Join(_mainIconRect.DOScale(1f, _feedDuration).SetEase(Ease.OutBack, 2.4f));
        seq.Join(g.DOFade(1f, _feedDuration * 0.6f).SetEase(UIAnim.EaseIn));
        seq.SetUpdate(true).SetTarget(_mainIconRect).SetLink(gameObject);
    }

    /// <summary>
    /// 손을 떼는 순간의 촉감 — "찰칵" 소리 + 슬롯 펀치 + 총이 한 번 튀는 반동.
    /// <paramref name="full"/>이면 카메라까지 친다(탄이 실제로 바뀐 경우).
    /// 같은 약실로 되돌아온 헛 드래그에서는 가벼운 걸림감만 주고 카메라는 건드리지 않는다.
    ///
    /// 연출이 <b>끝날 때</b>가 아니라 손을 뗀 <b>그 순간</b>에 친다. 회전이 멎기를 기다렸다가
    /// 치면 입력과 반응 사이가 벌어져 굼떠 보인다.
    /// </summary>
    private void PlaySeatFeedback(bool full)
    {
        if (!string.IsNullOrEmpty(_clickSfxId)) SoundManager.Instance?.PlaySfx(_clickSfxId);
        if (_mainSlot != null) UIAnim.Punch(_mainSlot, full ? 0.16f : 0.08f, 0.26f);

        if (_root != null)
        {
            UIAnim.Stop(_root);
            _root.DOPunchAnchorPos(new Vector2(full ? -9f : -4f, 0f), 0.22f, 10, 0.9f)
                 .SetUpdate(true).SetTarget(_root).SetLink(gameObject);
        }

        if (full) HitFeedbackManager.Punch(_shakeMagnitude, _shakeDuration);
    }

    // ───────────────────────── 회전 / 스내핑 ─────────────────────────

    private void ApplyRotation(float angle)
    {
        _rotorAngle = angle;
        if (_rotor != null) _rotor.localEulerAngles = new Vector3(0f, 0f, angle);

        for (int i = 0; i < ChamberCount; i++)
        {
            if (_chambers[i] == null) continue;
            _chambers[i].SetCounterRotation(angle);
            _chambers[i].SetEdgeFade(EdgeFadeAt(i, angle));
        }
    }

    /// <summary>
    /// 실린더가 <paramref name="rotorAngle"/>도 돌아갔을 때 <paramref name="index"/>번 약실의 투명도.
    ///
    /// 실린더는 화면 오른쪽 벽에 붙어 있어서, 오른쪽으로 돌아간 약실은 화면 밖으로 잘려 나간다.
    /// 잘린 채로 걸쳐 있으면 "묻힌" 것처럼 보이므로, 경계에 닿기 전에 알파로 사라지고
    /// 반대편(아래)에서 알파로 다시 나타나게 한다. 판정은 세로가 아니라 <b>가로 위치</b> 기준이다 —
    /// 잘리는 원인이 화면 오른쪽 끝이기 때문.
    /// </summary>
    private float EdgeFadeAt(int index, float rotorAngle)
    {
        float worldDeg = 90f + index * StepAngle + rotorAngle;
        float x = _orbitRadius * Mathf.Cos(worldDeg * Mathf.Deg2Rad);
        if (_edgeFadeWidth <= 0.01f) return x <= _edgeVisibleX ? 1f : 0f;
        return Mathf.Clamp01((_edgeVisibleX - x) / _edgeFadeWidth);
    }

    /// <summary><paramref name="index"/>번 약실이 12시에 오도록 돌린다. 지금 각도에서 가장 가까운 등가 각도로 간다.</summary>
    private void RotateToChamber(int index, bool animate, bool feed)
    {
        if (index < 0 || index >= ChamberCount) return;

        float raw = -index * StepAngle;
        // raw 와 360도 차이나는 값들 중 현재 각도에 가장 가까운 것 — 괜히 한 바퀴 되돌아가지 않게.
        float target = raw + Mathf.Round((_rotorAngle - raw) / 360f) * 360f;

        _selected = index;
        for (int i = 0; i < ChamberCount; i++)
            if (_chambers[i] != null) _chambers[i].SetLoaded(i == index);

        var choices = _shooter != null ? _shooter.Choices : null;
        BulletItemDefinition def = choices != null && index < choices.Count ? choices[index].Definition : null;
        int count = choices != null && index < choices.Count ? choices[index].Count : 0;

        if (!animate)
        {
            ApplyRotation(target);
            SetMainSlot(def, count, animate: false);
            return;
        }

        DOTween.Kill(this);
        float from = _rotorAngle;
        // OutBack 으로 살짝 지나쳤다 되돌아오며 톱니에 물리는 느낌을 낸다.
        DOTween.To(() => from, ApplyRotation, target, _snapDuration)
               .SetEase(Ease.OutBack, 1.6f)
               .SetUpdate(true)
               .SetTarget(this)
               .SetLink(gameObject)
               .OnComplete(() => ApplyRotation(target));

        // 회전이 끝나기를 기다리지 않고 "동시에" 탄이 물리기 시작한다.
        // 예전처럼 회전 → 삽입으로 이으면 두 동작 길이가 더해져 굼뜨게 느껴진다.
        SetMainSlot(def, count, animate: feed);
    }

    /// <summary>손을 뗀 자리에서 가장 가까운 "탄이 든" 약실 번호를 고른다. 빈 약실에는 멈추지 않는다.</summary>
    private int NearestFilledChamber()
    {
        if (_filled <= 0) return -1;
        int raw = Mod(Mathf.RoundToInt(-_rotorAngle / StepAngle), ChamberCount);
        if (raw < _filled) return raw;

        for (int d = 1; d <= ChamberCount; d++)
        {
            int up = Mod(raw + d, ChamberCount);
            if (up < _filled) return up;
            int down = Mod(raw - d, ChamberCount);
            if (down < _filled) return down;
        }
        return 0;
    }

    /// <summary>
    /// 손을 뗀 뒤 스내핑하고 실제 선택을 PlayerShooter에 반영한다.
    /// 회전·삽입 연출과 "찰칵" 촉감이 모두 이 순간 함께 시작한다.
    /// </summary>
    private void CommitSelection(int index)
    {
        if (index < 0) return;
        bool changed = index != _selected;
        RotateToChamber(index, animate: true, feed: changed);
        PlaySeatFeedback(changed);
        if (changed && _shooter != null) _shooter.SelectBullet(index);
    }

    // ───────────────────────── 입력 ─────────────────────────

    public void OnPointerDown(PointerEventData eventData)
    {
        _pressPosition = eventData.position;
        _dragging = false;
        if (_glow != null) UIAnim.ColorTo(_glow, GlowColor(0.9f), UIAnim.Fast);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_filled <= 1) return;   // 고를 게 없으면 돌리지 않는다.

        _dragging = true;
        DOTween.Kill(this);

        Vector2 local = ToLocal(eventData);
        float grabRadius = _root.rect.width * 0.5f * _rotateGrabRatio;

        // 가장자리를 잡았으면 손가락 각도를 그대로 따라가고, 중심 근처를 잡았으면 세로 이동으로 환산한다.
        // (중심에서는 각도가 조금만 움직여도 크게 튀어서 조작이 불안정하다.)
        _rotateMode = local.magnitude > grabRadius;
        _lastPointerAngle = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging) return;

        if (_rotateMode)
        {
            Vector2 local = ToLocal(eventData);
            if (local.sqrMagnitude < 1f) return;
            float angle = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
            ApplyRotation(_rotorAngle + Mathf.DeltaAngle(_lastPointerAngle, angle));
            _lastPointerAngle = angle;
        }
        else
        {
            // 위로 끌면 다음 약실(= 회전각 감소) 쪽으로 넘어간다.
            ApplyRotation(_rotorAngle - eventData.delta.y / _pixelsPerStep * StepAngle);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging) return;
        _dragging = false;
        CommitSelection(NearestFilledChamber());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_glow != null) UIAnim.ColorTo(_glow, GlowColor(0.5f), UIAnim.Normal);
        if (_dragging) return;   // 드래그였으면 OnEndDrag가 처리했다.

        // 거의 안 움직인 탭: 손가락이 닿은 약실로 바로 돌린다.
        if ((eventData.position - _pressPosition).magnitude > _tapSlop) return;
        int hit = ChamberAt(ToLocal(eventData));
        if (hit >= 0) CommitSelection(hit);
    }

    /// <summary>로컬 좌표에 가장 가까운 약실 번호. 어느 약실에도 닿지 않았으면 -1.</summary>
    private int ChamberAt(Vector2 local)
    {
        // 약실은 회전한 상태로 놓여 있으므로, 손가락 좌표를 실린더 좌표계로 되돌려 비교한다.
        Vector2 inRotor = Rotate(local, -_rotorAngle);
        float best = float.MaxValue;
        int bestIndex = -1;

        for (int i = 0; i < _filled; i++)
        {
            if (_chambers[i] == null) continue;
            // 벽 뒤로 사라지는 중인 약실은 탭으로 고를 수 없다(보이지도 않는데 잡히면 혼란스럽다).
            if (_chambers[i].EdgeAlpha < 0.5f) continue;
            float d = (inRotor - _chambers[i].Rect.anchoredPosition).sqrMagnitude;
            if (d < best) { best = d; bestIndex = i; }
        }

        // 약실 반지름을 넉넉히 잡아 손가락 오차를 흡수한다.
        float reach = _orbitRadius * 0.55f;
        return best <= reach * reach ? bestIndex : -1;
    }

    private Vector2 ToLocal(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _root, eventData.position, eventData.pressEventCamera, out Vector2 local);
        // 피벗이 가운데가 아니어도 중심 기준 좌표가 되도록 보정.
        Rect r = _root.rect;
        return local - new Vector2(r.center.x, r.center.y);
    }

    private Color GlowColor(float alpha)
    {
        Color c = _selected >= 0 && _shooter != null && _selected < _shooter.Choices.Count
            ? CylinderChamberView.BulletTint(_shooter.Choices[_selected].Definition)
            : UITheme.Cyan;
        return c.A(alpha);
    }

    private static Vector2 Rotate(Vector2 p, float deg)
    {
        float a = deg * Mathf.Deg2Rad, s = Mathf.Sin(a), c = Mathf.Cos(a);
        return new Vector2(p.x * c - p.y * s, p.x * s + p.y * c);
    }

    private static int Mod(int v, int m) => ((v % m) + m) % m;
}
