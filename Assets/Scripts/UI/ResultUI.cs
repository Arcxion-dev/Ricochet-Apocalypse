using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 결과(Result) 씬의 화면. 한 판(런)이 끝났을 때 <see cref="SceneLoader.FinishRun"/>가 이 씬으로 보내며,
/// 결과 내용은 <see cref="RunResult"/>에서 읽는다.
///
/// - <b>게임 최종 클리어</b>: 마지막 스테이지를 깨면 표시. 누적 클리어 횟수도 함께 보여준다.
/// - <b>게임 오버</b>: 플레이어 사망(또는 민간인 피격)으로 런이 끝났을 때 표시. 로그라이크 규칙상
///   저장된 런 데이터는 이미 지워진 상태라, "다시 도전"은 항상 1스테이지부터 빈 손으로 시작한다.
///
/// 결과 씬에 아무것도 배치하지 않아도 되도록 다른 런타임 UI들과 같은 방식으로 자동 부트스트랩하고,
/// 결과 씬일 때만 캔버스를 켠다.
/// </summary>
public class ResultUI : MonoBehaviour
{
    private static ResultUI _instance;

    private Canvas _canvas;
    private Font _font;
    private Text _title;
    private Text _subtitle;
    private Text _stats;

    /// <summary>첫 씬 로드 후 인스턴스가 없으면 하나 만들어 둔다(씬 세팅 불필요).</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("ResultUI");
        go.AddComponent<ResultUI>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureEventSystem();
        BuildCanvas();

        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        if (_instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply(scene.name);

    /// <summary>결과 씬일 때만 화면을 켜고 내용을 채운다.</summary>
    private void Apply(string sceneName)
    {
        bool show = sceneName == SceneLoader.SceneNames.Result;
        if (_canvas != null) _canvas.enabled = show;
        if (!show) return;

        // 스테이지에서 정지 상태로 넘어왔을 수 있으니 시간을 되돌려 놓는다.
        Time.timeScale = 1f;
        Refresh();
    }

    private void Refresh()
    {
        bool cleared = RunResult.LastOutcome == RunResult.Outcome.Cleared;
        bool failed = RunResult.LastOutcome == RunResult.Outcome.Failed;

        if (cleared)
        {
            _title.text = "게임 최종 클리어!";
            _title.color = new Color(1f, 0.86f, 0.42f);

            int clearCount = SaveManager.Instance != null ? SaveManager.Instance.ClearCount : 0;
            _subtitle.text = clearCount > 1
                ? $"모든 스테이지를 돌파했습니다.   (통산 {clearCount}회 클리어)"
                : "모든 스테이지를 돌파했습니다.";
        }
        else if (failed)
        {
            _title.text = "게임 오버";
            _title.color = new Color(0.92f, 0.44f, 0.38f);
            _subtitle.text = string.IsNullOrEmpty(RunResult.FailReason)
                ? "이번 판은 여기까지. 다음 판은 1스테이지부터 다시 시작합니다."
                : $"{RunResult.FailReason} — 다음 판은 1스테이지부터 다시 시작합니다.";
        }
        else
        {
            // 런이 끝나지 않았는데 결과 씬으로 온 경우(디버그 이동 등).
            _title.text = "결과";
            _title.color = new Color(0.95f, 0.93f, 0.82f);
            _subtitle.text = "진행 중인 판의 기록입니다.";
        }

        int stageCount = SceneLoader.StageCount;
        _stats.text =
            $"클리어한 스테이지    {RunResult.StagesCleared} / {stageCount}\n" +
            $"처치한 적            {RunResult.TotalKills}\n" +
            $"발사한 탄환          {RunResult.TotalShots}\n" +
            $"최고 콤보            {RunResult.BestCombo}\n" +
            $"퍼펙트 스테이지      {RunResult.PerfectStages}\n" +
            $"획득 골드            {RunResult.TotalReward}";
    }

    // ───────────────────────── 버튼 동작 ─────────────────────────

    /// <summary>1스테이지부터 새 판을 시작한다(로그라이크: 소지품 없이 처음부터).</summary>
    private void OnRetry() => SceneLoader.StartNewRun();

    private void OnBackToTitle() => SceneLoader.LoadTitle();

    private void OnQuit()
    {
        Debug.Log("[ResultUI] 게임 종료");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ───────────────────────── UI 구성 ─────────────────────────

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<StandaloneInputModule>();
    }

    private void BuildCanvas()
    {
        var canvasGO = new GameObject("ResultCanvas");
        canvasGO.transform.SetParent(transform, false);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 900;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        canvasGO.AddComponent<GraphicRaycaster>();

        var dim = new GameObject("Dim", typeof(RectTransform));
        dim.transform.SetParent(canvasGO.transform, false);
        var dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0.04f, 0.05f, 0.06f, 0.92f);
        StretchFull(dim.GetComponent<RectTransform>());

        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(dim.transform, false);
        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0.14f, 0.17f, 0.14f, 0.98f);
        var pRt = panel.GetComponent<RectTransform>();
        pRt.anchorMin = pRt.anchorMax = new Vector2(0.5f, 0.5f);
        pRt.pivot = new Vector2(0.5f, 0.5f);
        pRt.sizeDelta = new Vector2(720f, 0f);

        var layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(36, 36, 28, 28);
        layout.spacing = 14f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childAlignment = TextAnchor.UpperCenter;

        var fitter = panel.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _title = AddLabel(pRt, "결과", 46, FontStyle.Bold, new Color(0.95f, 0.93f, 0.82f), TextAnchor.MiddleCenter, 64f);
        _subtitle = AddLabel(pRt, string.Empty, 20, FontStyle.Normal, new Color(0.78f, 0.82f, 0.70f), TextAnchor.MiddleCenter, 30f);
        _stats = AddLabel(pRt, string.Empty, 24, FontStyle.Normal, new Color(0.90f, 0.90f, 0.84f), TextAnchor.UpperLeft, 210f);

        AddButton(pRt, "처음부터 다시 도전", new Color(0.25f, 0.35f, 0.22f), OnRetry);
        AddButton(pRt, "타이틀로 돌아가기", new Color(0.28f, 0.30f, 0.42f), OnBackToTitle);
        AddButton(pRt, "게임 종료", new Color(0.42f, 0.20f, 0.18f), OnQuit);
    }

    private Text AddLabel(Transform parent, string text, int size, FontStyle style, Color color, TextAnchor anchor, float height)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = _font; t.text = text; t.fontSize = size; t.fontStyle = style;
        t.color = color; t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.lineSpacing = 1.25f;
        var le = go.AddComponent<LayoutElement>(); le.preferredHeight = height;
        return t;
    }

    private Button AddButton(Transform parent, string label, Color color, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject($"Button_{label}", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        var le = go.AddComponent<LayoutElement>(); le.preferredHeight = 54f;

        var tGO = new GameObject("Text", typeof(RectTransform));
        tGO.transform.SetParent(go.transform, false);
        var t = tGO.AddComponent<Text>();
        t.font = _font; t.text = label; t.fontSize = 26; t.color = Color.white;
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
