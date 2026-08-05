using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 프로젝트 UI 모션의 단일 소스(DOTween 위에 얹은 얇은 헬퍼 모음).
/// 지속시간/이징을 여기서만 관리해 화면마다 연출 톤이 어긋나지 않게 한다.
/// 색/폰트를 <see cref="UITheme"/>가 맡는 것과 같은 역할.
///
/// 모든 트윈은 두 가지를 항상 보장한다:
/// - <c>SetUpdate(true)</c> : 히트스톱/일시정지로 timeScale이 0이어도 UI는 계속 움직인다.
/// - <c>SetLink(owner)</c>  : 대상 GameObject가 파괴되면 트윈도 같이 죽는다(리스트 행처럼
///   매 갱신마다 파괴/재생성되는 오브젝트에서 필수).
/// </summary>
public static class UIAnim
{
    // ───────────────────────── 모션 상수 ─────────────────────────

    /// <summary>버튼 눌림처럼 즉각 반응해야 하는 것.</summary>
    public const float Fast = 0.12f;
    /// <summary>일반적인 나타남/사라짐.</summary>
    public const float Normal = 0.22f;
    /// <summary>큰 창이 열릴 때.</summary>
    public const float Slow = 0.34f;
    /// <summary>숫자 굴러가는 시간.</summary>
    public const float CountDur = 0.45f;
    /// <summary>리스트 항목 사이 간격.</summary>
    public const float StaggerStep = 0.035f;

    public const Ease EaseIn = Ease.OutCubic;    // 나타날 때
    public const Ease EaseOut = Ease.InCubic;    // 사라질 때
    public const Ease EasePop = Ease.OutBack;    // 튀어나올 때
    public const Ease EaseSoft = Ease.OutQuad;   // 값 스무딩

    /// <summary>
    /// 이 프로젝트는 Enter Play Mode에서 Domain Reload가 꺼져 있다(EditorSettings).
    /// 그래서 DOTween의 정적 상태가 이전 플레이 세션에서 그대로 넘어온다 — 죽은 오브젝트를
    /// 가리키는 트윈이 남아 두 번째 플레이부터 오동작한다. 가장 먼저 실행되는
    /// SubsystemRegistration 시점에 통째로 비우고 시작한다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        DOTween.Clear(true);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        DOTween.Init(false, true, LogBehaviour.ErrorsOnly).SetCapacity(600, 160);
        DOTween.defaultEaseType = EaseIn;
    }

    // ───────────────────────── 공통 처리 ─────────────────────────

    /// <summary>모든 트윈에 unscaled 업데이트 + 소유자 링크를 건다.</summary>
    private static T Own<T>(T tween, Component owner) where T : Tween
    {
        if (tween == null) return null;
        tween.SetUpdate(true);
        if (owner != null)
        {
            tween.SetTarget(owner);
            tween.SetLink(owner.gameObject);
        }
        return tween;
    }

    /// <summary>
    /// 색 트윈 전용 키(GameObject)로 묶는다.
    /// 같은 라벨에 숫자 카운트업(타깃=TMP_Text 컴포넌트)과 색 플래시가 동시에 걸리는 일이 잦은데,
    /// 둘 다 컴포넌트를 타깃으로 잡으면 나중에 건 쪽이 앞의 것을 죽여 버린다.
    /// 색은 GameObject를 키로 써서 서로 간섭하지 않게 한다.
    /// </summary>
    private static T OwnColor<T>(T tween, Graphic graphic) where T : Tween
    {
        if (tween == null || graphic == null) return tween;
        tween.SetUpdate(true);
        tween.SetTarget(graphic.gameObject);
        tween.SetLink(graphic.gameObject);
        return tween;
    }

    /// <summary>CanvasGroup이 없으면 붙여서 돌려준다.</summary>
    public static CanvasGroup GroupOf(Component c)
    {
        if (c == null) return null;
        var g = c.GetComponent<CanvasGroup>();
        return g != null ? g : c.gameObject.AddComponent<CanvasGroup>();
    }

    /// <summary>해당 대상에 걸린 트윈을 모두 정리한다.</summary>
    public static void Stop(Component owner, bool complete = false)
    {
        if (owner != null) DOTween.Kill(owner, complete);
    }

    // ───────────────────────── 팝업 열기/닫기 ─────────────────────────

    /// <summary>
    /// 팝업을 연다: 페이드 인 + 살짝 커지며 자리 잡음.
    /// <paramref name="content"/>는 스케일 대상(창 루트나 SafeArea). 없으면 페이드만 한다.
    /// </summary>
    public static Sequence ShowPopup(CanvasGroup group, RectTransform content, float duration = Normal, float fromScale = 0.92f)
    {
        Component owner = (Component)group ?? content;
        if (owner == null) return null;

        Stop(owner);
        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = true;
            group.interactable = true;
        }
        if (content != null) content.localScale = Vector3.one * fromScale;

        var seq = DOTween.Sequence();
        if (group != null) seq.Join(group.DOFade(1f, duration).SetEase(EaseIn));
        if (content != null) seq.Join(content.DOScale(1f, duration * 1.2f).SetEase(EasePop));
        return Own(seq, owner);
    }

    /// <summary>팝업을 닫는다. 연출이 끝난 뒤 <paramref name="onDone"/>(보통 Canvas 끄기)를 호출한다.</summary>
    public static Sequence HidePopup(CanvasGroup group, RectTransform content, float duration = Fast, Action onDone = null, float toScale = 0.95f)
    {
        Component owner = (Component)group ?? content;
        if (owner == null) { onDone?.Invoke(); return null; }

        Stop(owner);
        if (group != null)
        {
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        var seq = DOTween.Sequence();
        if (group != null) seq.Join(group.DOFade(0f, duration).SetEase(EaseOut));
        if (content != null) seq.Join(content.DOScale(toScale, duration).SetEase(EaseOut));
        seq.OnComplete(() =>
        {
            if (content != null) content.localScale = Vector3.one;
            onDone?.Invoke();
        });
        return Own(seq, owner);
    }

    /// <summary>아래에서 밀려 올라오며 나타난다(하단 고정 패널용).</summary>
    public static Sequence SlideUpIn(CanvasGroup group, RectTransform rt, float distance = 60f, float duration = Normal)
    {
        Component owner = (Component)group ?? rt;
        if (owner == null) return null;

        Stop(owner);
        Vector2 home = rt != null ? rt.anchoredPosition : Vector2.zero;
        if (rt != null) rt.anchoredPosition = home - new Vector2(0f, distance);
        if (group != null) { group.alpha = 0f; group.blocksRaycasts = true; group.interactable = true; }

        var seq = DOTween.Sequence();
        if (group != null) seq.Join(group.DOFade(1f, duration).SetEase(EaseIn));
        if (rt != null) seq.Join(rt.DOAnchorPos(home, duration * 1.2f).SetEase(EasePop));
        return Own(seq, owner);
    }

    /// <summary>아래로 내려가며 사라진다.</summary>
    public static Sequence SlideDownOut(CanvasGroup group, RectTransform rt, float distance = 60f, float duration = Fast, Action onDone = null)
    {
        Component owner = (Component)group ?? rt;
        if (owner == null) { onDone?.Invoke(); return null; }

        Stop(owner);
        Vector2 home = rt != null ? rt.anchoredPosition : Vector2.zero;
        if (group != null) { group.blocksRaycasts = false; group.interactable = false; }

        var seq = DOTween.Sequence();
        if (group != null) seq.Join(group.DOFade(0f, duration).SetEase(EaseOut));
        if (rt != null) seq.Join(rt.DOAnchorPos(home - new Vector2(0f, distance), duration).SetEase(EaseOut));
        seq.OnComplete(() =>
        {
            if (rt != null) rt.anchoredPosition = home;
            onDone?.Invoke();
        });
        return Own(seq, owner);
    }

    // ───────────────────────── 강조 ─────────────────────────

    /// <summary>값이 바뀌었을 때의 짧은 스케일 펀치.</summary>
    public static Tween Punch(Transform target, float strength = 0.18f, float duration = 0.28f)
    {
        if (target == null) return null;
        target.localScale = Vector3.one;
        return Own(target.DOPunchScale(Vector3.one * strength, duration, 8, 0.9f), target);
    }

    /// <summary>실패/거절 피드백용 좌우 흔들림.</summary>
    public static Tween Shake(RectTransform target, float strength = 14f, float duration = 0.3f)
    {
        if (target == null) return null;
        return Own(target.DOShakeAnchorPos(duration, new Vector2(strength, 0f), 14, 90f, false, true), target);
    }

    /// <summary>작게 시작해 튀어나오며 등장(카드/뱃지용).</summary>
    public static Sequence PopIn(RectTransform rt, float delay = 0f, float duration = Normal)
    {
        if (rt == null) return null;
        var group = GroupOf(rt);
        Stop(rt);
        rt.localScale = Vector3.one * 0.6f;
        group.alpha = 0f;

        var seq = DOTween.Sequence();
        seq.Join(rt.DOScale(1f, duration * 1.3f).SetEase(EasePop));
        seq.Join(group.DOFade(1f, duration).SetEase(EaseIn));
        if (delay > 0f) seq.SetDelay(delay);
        return Own(seq, rt);
    }

    /// <summary>
    /// 살짝 아래에서 올라오며 나타난다(화면 진입 시 패널 등장용).
    /// LayoutGroup 자식에는 쓰지 말 것 — anchoredPosition을 건드린다.
    /// </summary>
    public static Sequence RiseIn(RectTransform rt, float delay = 0f, float distance = 40f, float duration = Slow)
    {
        if (rt == null) return null;
        var group = GroupOf(rt);
        Stop(rt);
        Stop(group);

        Vector2 home = rt.anchoredPosition;
        rt.anchoredPosition = home - new Vector2(0f, distance);
        group.alpha = 0f;

        var seq = DOTween.Sequence();
        seq.Join(rt.DOAnchorPos(home, duration).SetEase(EaseIn));
        seq.Join(group.DOFade(1f, duration * 0.8f).SetEase(EaseIn));
        seq.OnKill(() => { if (rt != null) rt.anchoredPosition = home; });
        if (delay > 0f) seq.SetDelay(delay);
        return Own(seq, rt);
    }

    /// <summary>색을 잠깐 <paramref name="flash"/>로 튀겼다가 <paramref name="back"/>으로 되돌린다.</summary>
    public static Tween Flash(Graphic graphic, Color flash, Color back, float duration = 0.35f)
    {
        if (graphic == null) return null;
        DOTween.Kill(graphic.gameObject);
        graphic.color = flash;
        return OwnColor(graphic.DOColor(back, duration).SetEase(EaseSoft), graphic);
    }

    // ───────────────────────── 값 스무딩 ─────────────────────────

    /// <summary>Image.fillAmount를 부드럽게 따라가게 한다(체력바/로딩바).</summary>
    public static Tween FillTo(Image image, float value, float duration = Normal)
    {
        if (image == null) return null;
        Stop(image);
        return Own(image.DOFillAmount(Mathf.Clamp01(value), duration).SetEase(EaseSoft), image);
    }

    /// <summary>색을 부드럽게 전환한다(탭 선택, 슬롯 강조 등).</summary>
    public static Tween ColorTo(Graphic graphic, Color color, float duration = Normal)
    {
        if (graphic == null) return null;
        DOTween.Kill(graphic.gameObject);
        return OwnColor(graphic.DOColor(color, duration).SetEase(EaseSoft), graphic);
    }

    /// <summary>Outline 색을 부드럽게 전환한다.</summary>
    public static Tween ColorTo(Outline outline, Color color, float duration = Normal)
    {
        if (outline == null) return null;
        Stop(outline);
        return Own(outline.DOColor(color, duration).SetEase(EaseSoft), outline);
    }

    /// <summary>숫자를 from → to로 굴린다. 골드/점수처럼 "쌓이는 느낌"이 필요한 값에.</summary>
    public static Tween CountTo(TMP_Text label, int from, int to, string prefix = "", string suffix = "",
                                string numberFormat = "N0", float duration = CountDur)
    {
        if (label == null) return null;
        Stop(label);
        if (from == to || duration <= 0f)
        {
            label.text = prefix + to.ToString(numberFormat) + suffix;
            return null;
        }

        int current = from;
        var tween = DOTween.To(() => current, v =>
        {
            current = v;
            label.text = prefix + current.ToString(numberFormat) + suffix;
        }, to, duration).SetEase(EaseSoft);
        return Own(tween, label);
    }

    // ───────────────────────── 리스트 ─────────────────────────

    /// <summary>
    /// 컨테이너의 자식들을 순차적으로 등장시킨다(알파 + 스케일).
    /// 위치가 아니라 스케일만 건드리므로 LayoutGroup과 싸우지 않는다.
    /// </summary>
    public static void StaggerIn(Transform container, float step = StaggerStep, float duration = Normal, float fromScale = 0.88f)
    {
        if (container == null) return;
        int n = container.childCount;
        for (int i = 0; i < n; i++)
        {
            var child = container.GetChild(i) as RectTransform;
            if (child == null || !child.gameObject.activeSelf) continue;

            var group = GroupOf(child);
            Stop(child);
            Stop(group);
            group.alpha = 0f;
            child.localScale = Vector3.one * fromScale;

            float delay = i * step;
            Own(group.DOFade(1f, duration).SetDelay(delay).SetEase(EaseIn), group);
            Own(child.DOScale(1f, duration * 1.2f).SetDelay(delay).SetEase(EasePop), child);
        }
    }

    /// <summary>컨테이너의 i번째 자식을 펀치한다(선택 이동 피드백).</summary>
    public static void PunchChild(Transform container, int index, float strength = 0.14f)
    {
        if (container == null || index < 0 || index >= container.childCount) return;
        Punch(container.GetChild(index), strength);
    }
}
