using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 설정(SETTINGS) 패널. 코드로 캔버스를 생성하는 런타임 패널이며, 타이틀의 SETTINGS 버튼이
/// <see cref="Show"/>로 띄운다(다른 씬의 일시정지 메뉴 등에서도 재사용 가능).
///
/// 구성:
/// - 마스터/BGM/SFX 볼륨 슬라이더 → <see cref="GameSettings"/>에 연결(마스터는 실제 반영, BGM/SFX는 저장).
/// - 전체화면 토글, 해상도 이전/다음 → <see cref="GameSettings"/>로 실제 반영.
/// - 세이브 삭제(<see cref="SaveManager"/>), 닫기.
/// </summary>
public class SettingsUI : MonoBehaviour
{
    private static SettingsUI _instance;

    private Canvas _canvas;
    private CanvasGroup _group;
    private RectTransform _panelRect;
    private Font _font;
    private Text _masterVal, _bgmVal, _sfxVal, _fullscreenLabel, _resLabel;
    private Resolution[] _resolutions;
    private int _resIndex;
    private bool _visible;

    /// <summary>설정 패널을 띄운다(없으면 생성).</summary>
    public static void Show()
    {
        if (_instance == null)
        {
            var go = new GameObject("SettingsUI");
            _instance = go.AddComponent<SettingsUI>();
        }
        _instance.SetVisible(true);
    }

    /// <summary>설정 패널을 닫는다.</summary>
    public static void Hide()
    {
        if (_instance != null) _instance.SetVisible(false);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureEventSystem();
        BuildResolutions();
        BuildCanvas();

        // 초기 상태는 연출 없이 즉시 숨김.
        _visible = false;
        if (_canvas != null) _canvas.enabled = false;
        if (_group != null) { _group.alpha = 0f; _group.blocksRaycasts = false; }
    }

    private void SetVisible(bool visible)
    {
        if (_canvas == null) return;
        if (_visible == visible) return;
        _visible = visible;

        if (visible)
        {
            SyncFromSettings();
            _canvas.enabled = true;
            _canvas.transform.SetAsLastSibling();
            UIAnim.ShowPopup(_group, _panelRect, UIAnim.Normal, 0.88f);
        }
        else
        {
            UIAnim.HidePopup(_group, _panelRect, UIAnim.Fast, () => { if (_canvas != null) _canvas.enabled = false; });
        }
    }

    // ───────────────────────── 구성 ─────────────────────────

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<StandaloneInputModule>();
    }

    private void BuildResolutions()
    {
        _resolutions = Screen.resolutions;
        if (_resolutions == null || _resolutions.Length == 0)
            _resolutions = new[] { Screen.currentResolution };

        // 저장된 해상도와 가장 가까운 인덱스를 초기값으로.
        _resIndex = _resolutions.Length - 1;
        for (int i = 0; i < _resolutions.Length; i++)
        {
            if (_resolutions[i].width == GameSettings.ResWidth && _resolutions[i].height == GameSettings.ResHeight)
            {
                _resIndex = i;
                break;
            }
        }
    }

    private void BuildCanvas()
    {
        var canvasGO = new GameObject("SettingsCanvas");
        canvasGO.transform.SetParent(transform, false);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 1200;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f; // 폭 기준 → 세로로 긴 폰에서 좌우가 잘리지 않도록
        canvasGO.AddComponent<GraphicRaycaster>();
        _group = canvasGO.AddComponent<CanvasGroup>();

        // 반투명 배경(뒤 클릭 차단). 노치까지 덮어야 하므로 SafeArea 밖.
        var dim = CreateStretch(canvasGO.transform, "Dim");
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.6f);

        // 중앙 패널.
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(dim, false);
        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0.16f, 0.19f, 0.15f, 0.98f);
        var pRt = panel.GetComponent<RectTransform>();
        pRt.anchorMin = pRt.anchorMax = new Vector2(0.5f, 0.5f);
        pRt.pivot = new Vector2(0.5f, 0.5f);
        pRt.sizeDelta = new Vector2(620f, 620f);
        _panelRect = pRt;

        var layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 24, 24);
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childAlignment = TextAnchor.UpperCenter;

        // 패널 높이를 내용에 맞춰 자동 조절 → 항상 화면 중앙에 다 들어오도록(하단 잘림 방지).
        var fitter = panel.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        AddLabel(pRt, "설정", 34, FontStyle.Bold, TextAnchor.MiddleCenter, 48f);

        _masterVal = AddSliderRow(pRt, "마스터 볼륨", GameSettings.MasterVolume, v => GameSettings.SetMasterVolume(v));
        _bgmVal = AddSliderRow(pRt, "BGM 볼륨", GameSettings.BgmVolume, v => GameSettings.SetBgmVolume(v));
        _sfxVal = AddSliderRow(pRt, "SFX 볼륨", GameSettings.SfxVolume, v => GameSettings.SetSfxVolume(v));

        _fullscreenLabel = AddToggleRow(pRt, "전체화면", () =>
        {
            GameSettings.SetFullscreen(!GameSettings.Fullscreen);
            SyncFromSettings();
        });

        _resLabel = AddStepperRow(pRt, "해상도",
            onPrev: () => { CycleResolution(-1); },
            onNext: () => { CycleResolution(+1); });

        AddButton(pRt, "세이브 삭제", new Color(0.42f, 0.20f, 0.18f), () =>
        {
            if (SaveManager.Instance != null) SaveManager.Instance.DeleteSave();
            SetVisible(false);
            // 진행도가 0으로 초기화됐으니 타이틀로 돌아간다(다시 PLAY하면 첫 스테이지부터).
            SceneLoader.LoadTitle();
        });

        AddButton(pRt, "닫기", new Color(0.25f, 0.35f, 0.22f), () => SetVisible(false));
    }

    private void SyncFromSettings()
    {
        if (_masterVal != null) _masterVal.text = Percent(GameSettings.MasterVolume);
        if (_bgmVal != null) _bgmVal.text = Percent(GameSettings.BgmVolume);
        if (_sfxVal != null) _sfxVal.text = Percent(GameSettings.SfxVolume);
        if (_fullscreenLabel != null) _fullscreenLabel.text = GameSettings.Fullscreen ? "전체화면: 켜짐" : "전체화면: 꺼짐";
        if (_resLabel != null && _resolutions.Length > 0)
        {
            var r = _resolutions[Mathf.Clamp(_resIndex, 0, _resolutions.Length - 1)];
            _resLabel.text = $"해상도: {r.width} x {r.height}";
        }
    }

    private void CycleResolution(int dir)
    {
        if (_resolutions == null || _resolutions.Length == 0) return;
        _resIndex = (_resIndex + dir + _resolutions.Length) % _resolutions.Length;
        var r = _resolutions[_resIndex];
        GameSettings.SetResolution(r.width, r.height);
        SyncFromSettings();
    }

    private static string Percent(float v) => $"{Mathf.RoundToInt(v * 100f)}%";

    // ───────────────────────── 위젯 헬퍼 ─────────────────────────

    private RectTransform CreateStretch(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return rt;
    }

    private Text AddLabel(Transform parent, string text, int size, FontStyle style, TextAnchor anchor, float height)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = _font; t.text = text; t.fontSize = size; t.fontStyle = style;
        t.color = new Color(0.95f, 0.93f, 0.82f); t.alignment = anchor;
        var le = go.AddComponent<LayoutElement>(); le.preferredHeight = height;
        return t;
    }

    /// <summary>라벨 + 슬라이더 + 값(%) 한 행. 반환값은 값 텍스트(동기화용).</summary>
    private Text AddSliderRow(Transform parent, string label, float initial, UnityEngine.Events.UnityAction<float> onChanged)
    {
        var row = new GameObject($"Row_{label}", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var le = row.AddComponent<LayoutElement>(); le.preferredHeight = 56f;

        var caption = AddLabel(row.transform, label, 22, FontStyle.Normal, TextAnchor.LowerLeft, 24f);
        var capRt = caption.rectTransform;
        capRt.anchorMin = new Vector2(0f, 1f); capRt.anchorMax = new Vector2(1f, 1f);
        capRt.pivot = new Vector2(0f, 1f); capRt.sizeDelta = new Vector2(0f, 24f);
        capRt.anchoredPosition = Vector2.zero;

        var valText = AddLabel(row.transform, Percent(initial), 22, FontStyle.Bold, TextAnchor.LowerRight, 24f);
        var valRt = valText.rectTransform;
        valRt.anchorMin = new Vector2(0f, 1f); valRt.anchorMax = new Vector2(1f, 1f);
        valRt.pivot = new Vector2(1f, 1f); valRt.sizeDelta = new Vector2(0f, 24f);
        valRt.anchoredPosition = Vector2.zero;

        var slider = BuildSlider(row.transform, initial);
        slider.onValueChanged.AddListener(v => { onChanged(v); valText.text = Percent(v); });
        return valText;
    }

    private Slider BuildSlider(Transform parent, float initial)
    {
        var sliderGO = new GameObject("Slider", typeof(RectTransform));
        sliderGO.transform.SetParent(parent, false);
        var sRt = sliderGO.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0f, 0f); sRt.anchorMax = new Vector2(1f, 0f);
        sRt.pivot = new Vector2(0.5f, 0f); sRt.sizeDelta = new Vector2(0f, 20f);
        sRt.anchoredPosition = new Vector2(0f, 2f);

        var slider = sliderGO.AddComponent<Slider>();
        slider.minValue = 0f; slider.maxValue = 1f;

        // 배경.
        var bg = new GameObject("Background", typeof(RectTransform));
        bg.transform.SetParent(sliderGO.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(1f, 1f, 1f, 0.15f);
        StretchFull(bg.GetComponent<RectTransform>());

        // Fill Area / Fill.
        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderGO.transform, false);
        var faRt = fillArea.GetComponent<RectTransform>();
        faRt.anchorMin = new Vector2(0f, 0.25f); faRt.anchorMax = new Vector2(1f, 0.75f);
        faRt.offsetMin = new Vector2(0f, 0f); faRt.offsetMax = new Vector2(0f, 0f);
        var fill = new GameObject("Fill", typeof(RectTransform));
        fill.transform.SetParent(fillArea.transform, false);
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.55f, 0.72f, 0.35f, 1f);
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f); fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.sizeDelta = new Vector2(10f, 0f);

        // Handle Slide Area / Handle.
        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderGO.transform, false);
        var haRt = handleArea.GetComponent<RectTransform>();
        haRt.anchorMin = new Vector2(0f, 0f); haRt.anchorMax = new Vector2(1f, 1f);
        haRt.offsetMin = new Vector2(8f, 0f); haRt.offsetMax = new Vector2(-8f, 0f);
        var handle = new GameObject("Handle", typeof(RectTransform));
        handle.transform.SetParent(handleArea.transform, false);
        var handleImg = handle.AddComponent<Image>();
        handleImg.color = new Color(0.92f, 0.9f, 0.78f, 1f);
        var handleRt = handle.GetComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(18f, 24f);

        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.value = initial;
        return slider;
    }

    private Text AddToggleRow(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        var btn = AddButton(parent, $"{label}", new Color(0.22f, 0.28f, 0.20f), onClick);
        return btn.GetComponentInChildren<Text>();
    }

    private Text AddStepperRow(Transform parent, string label, UnityEngine.Events.UnityAction onPrev, UnityEngine.Events.UnityAction onNext)
    {
        var row = new GameObject($"Stepper_{label}", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var le = row.AddComponent<LayoutElement>(); le.preferredHeight = 44f;

        var prev = MakeMiniButton(row.transform, "◀", new Vector2(0f, 0.5f), new Vector2(44f, 40f), new Vector2(4f, 0f), onPrev);
        var next = MakeMiniButton(row.transform, "▶", new Vector2(1f, 0.5f), new Vector2(44f, 40f), new Vector2(-4f, 0f), onNext);

        var t = AddLabel(row.transform, label, 22, FontStyle.Bold, TextAnchor.MiddleCenter, 40f);
        var tRt = t.rectTransform;
        tRt.anchorMin = new Vector2(0f, 0f); tRt.anchorMax = new Vector2(1f, 1f);
        tRt.offsetMin = new Vector2(52f, 0f); tRt.offsetMax = new Vector2(-52f, 0f);
        return t;
    }

    private Button MakeMiniButton(Transform parent, string text, Vector2 anchor, Vector2 size, Vector2 offset, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject($"Mini_{text}", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.3f, 0.38f, 0.26f, 1f);
        var btn = go.AddComponent<Button>();
        go.AddComponent<UIButtonMotion>();
        btn.onClick.AddListener(onClick);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor;
        rt.sizeDelta = size; rt.anchoredPosition = offset;

        var tGO = new GameObject("Text", typeof(RectTransform));
        tGO.transform.SetParent(go.transform, false);
        var t = tGO.AddComponent<Text>();
        t.font = _font; t.text = text; t.fontSize = 22; t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        StretchFull(tGO.GetComponent<RectTransform>());
        return btn;
    }

    private Button AddButton(Transform parent, string label, Color color, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject($"Button_{label}", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        go.AddComponent<UIButtonMotion>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        var le = go.AddComponent<LayoutElement>(); le.preferredHeight = 46f;

        var tGO = new GameObject("Text", typeof(RectTransform));
        tGO.transform.SetParent(go.transform, false);
        var t = tGO.AddComponent<Text>();
        t.font = _font; t.text = label; t.fontSize = 24; t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        StretchFull(tGO.GetComponent<RectTransform>());
        return btn;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
