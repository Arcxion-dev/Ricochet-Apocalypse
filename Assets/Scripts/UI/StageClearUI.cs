using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 스테이지 클리어 시 뜨는 결과/보상 창(코드 생성 uGUI 오버레이).
/// - 결과 요약(퍼펙트/콤보/처치/발사)과 지급 골드, 드랍테이블로 획득한 아이템 목록을 보여준다.
/// - [확인] 버튼을 누르면 <see cref="Show"/>에 넘긴 onConfirm 콜백을 실행한다(보통 상점으로 이동).
///
/// HitFeedbackManager와 동일하게 <see cref="Bootstrap"/>으로 자동 생성되는 씬 무관 싱글턴이라
/// 어떤 스테이지에서든 별도 배치 없이 동작한다. 평소엔 캔버스를 숨겨둔다.
/// </summary>
public class StageClearUI : MonoBehaviour
{
    public static StageClearUI Instance { get; private set; }

    private Font _font;
    private Canvas _canvas;
    private RectTransform _window;
    private Text _titleText;
    private RectTransform _bodyRoot;
    private Button _confirmButton;
    private Action _onConfirm;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("StageClearUI");
        go.AddComponent<StageClearUI>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureEventSystem();
        BuildCanvas();
        _canvas.enabled = false; // 평소 숨김.
    }

    /// <summary>클리어 결과와 드랍 목록을 표시하고, [확인] 시 onConfirm을 호출한다.</summary>
    public void Show(StageResult result, IReadOnlyList<DropResult> drops, Action onConfirm)
    {
        _onConfirm = onConfirm;

        // 오버레이가 프리즈 뒤에 가려지지 않도록 시간 흐름을 정상화.
        Time.timeScale = 1f;

        _titleText.text = result.IsPerfect ? "스테이지 클리어!  (PERFECT)" : "스테이지 클리어!";

        ClearChildren(_bodyRoot);
        AddLine($"처치: {result.TotalKills}    최고 콤보: {result.Combo}    발사: {result.ShotsFired}",
            18, new Color(0.85f, 0.9f, 1f));
        AddLine($"획득 골드: +{result.Reward}", 20, new Color(1f, 0.9f, 0.4f), FontStyle.Bold);

        AddLine("── 드랍 보상 ──", 18, new Color(0.7f, 0.95f, 0.8f), FontStyle.Bold);
        if (drops == null || drops.Count == 0)
        {
            AddLine("(획득한 아이템 없음)", 16, new Color(0.7f, 0.7f, 0.7f));
        }
        else
        {
            foreach (var d in drops)
            {
                if (d.Item == null) continue;
                AddLine($"· {d.Item.ResolvedName}  x{d.Quantity}", 17, new Color(0.9f, 0.95f, 1f));
            }
        }

        _canvas.enabled = true;
        _canvas.transform.SetAsLastSibling();
    }

    private void OnConfirmClicked()
    {
        _canvas.enabled = false;
        var cb = _onConfirm;
        _onConfirm = null;
        cb?.Invoke();
    }

    // ───────────────────────── UI 구성 ─────────────────────────

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<StandaloneInputModule>();
        DontDestroyOnLoad(esGO);
    }

    private void BuildCanvas()
    {
        var canvasGO = new GameObject("StageClearCanvas");
        canvasGO.transform.SetParent(transform, false);

        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 850; // 다른 HUD보다 위.

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // 화면 전체를 어둡게(뒤 클릭 차단).
        var dim = new GameObject("Dim", typeof(RectTransform));
        dim.transform.SetParent(_canvas.transform, false);
        var dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.6f);
        var dimRt = dim.GetComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero; dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero; dimRt.offsetMax = Vector2.zero;

        // 중앙 결과 창.
        var winGO = new GameObject("Window", typeof(RectTransform));
        winGO.transform.SetParent(_canvas.transform, false);
        var winImg = winGO.AddComponent<Image>();
        winImg.color = new Color(0.10f, 0.12f, 0.16f, 0.97f);
        _window = winGO.GetComponent<RectTransform>();
        _window.anchorMin = new Vector2(0.5f, 0.5f);
        _window.anchorMax = new Vector2(0.5f, 0.5f);
        _window.pivot = new Vector2(0.5f, 0.5f);
        _window.sizeDelta = new Vector2(560f, 520f);

        var layout = winGO.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.padding = new RectOffset(28, 28, 24, 24);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        _titleText = AddText(_window, "스테이지 클리어!", 30, new Color(1f, 0.95f, 0.6f), FontStyle.Bold);
        _titleText.alignment = TextAnchor.MiddleCenter;

        _bodyRoot = CreateVerticalPanel(_window, "Body");

        _confirmButton = AddButton(_window, "확인", new Color(0.18f, 0.34f, 0.20f, 0.98f));
        _confirmButton.onClick.AddListener(OnConfirmClicked);
    }

    // ───────────────────────── 위젯 헬퍼 ─────────────────────────

    private void AddLine(string text, int size, Color color, FontStyle style = FontStyle.Normal)
    {
        var t = AddText(_bodyRoot, text, size, color, style);
        t.alignment = TextAnchor.MiddleCenter;
    }

    private RectTransform CreateVerticalPanel(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // 부모 VerticalLayoutGroup이 높이를 제어하므로 ContentSizeFitter 없이 flexibleHeight로 남은 공간을 채운다.
        var le = go.AddComponent<LayoutElement>();
        le.flexibleHeight = 1f;
        return go.GetComponent<RectTransform>();
    }

    private Text AddText(Transform parent, string message, int fontSize, Color color, FontStyle style = FontStyle.Normal)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<Text>();
        text.font = _font;
        text.text = message;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = fontSize + 8f;
        return text;
    }

    private Button AddButton(Transform parent, string label, Color baseColor)
    {
        var go = new GameObject($"Button_{label}", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.color = baseColor;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 56f;

        var textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);
        var text = textGO.AddComponent<Text>();
        text.font = _font;
        text.text = label;
        text.fontSize = 24;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        var trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

        return button;
    }

    private void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }
}
