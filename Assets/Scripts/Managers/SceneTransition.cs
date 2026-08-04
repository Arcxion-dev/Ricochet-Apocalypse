using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 씬 전환을 "페이드 아웃 → 로딩 화면 → 비동기 로드 → 페이드 인"으로 감싸는 오버레이 매니저.
/// - 별도 로딩 씬을 두지 않고, DontDestroyOnLoad 캔버스를 항상 화면 위에 띄워 두는 오버레이 방식이다.
/// - 어느 씬에서 Play를 눌러도 존재하도록 <see cref="Bootstrap"/> 에서 자동 생성한다(씬 세팅 불필요).
/// - 페이드/타이머는 게임 정지(TimeScale 0)와 무관하게 실시간(unscaled)으로 진행한다.
///
/// 사용: <c>SceneTransition.Instance.Load("Stage1")</c> 또는 <see cref="SceneLoader"/> 경유.
/// 코드로 캔버스를 생성하므로(기존 UI들과 동일 방식) 로딩 화면 자체는 아트 에셋에 의존하지 않는다.
/// </summary>
public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance { get; private set; }

    [Header("연출 시간(초, 실시간)")]
    [SerializeField] private float _fadeDuration = 0.35f;
    [Tooltip("로딩 화면을 최소 이 시간만큼은 보여준다(너무 빨리 지나가 깜빡이지 않도록).")]
    [SerializeField] private float _minLoadingTime = 0.6f;

    /// <summary>현재 씬 전환(페이드/로딩)이 진행 중인지.</summary>
    public bool IsTransitioning { get; private set; }

    private Canvas _canvas;
    private CanvasGroup _group;   // 검은 화면 + 로딩 패널 전체 페이드
    private GameObject _loadingPanel;
    private Image _barFill;
    private Text _percentText;

    /// <summary>첫 씬 로드 후 매니저가 없으면 하나 만들어 둔다(항상 존재).</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("SceneTransition");
        go.AddComponent<SceneTransition>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildCanvas();

        // 씬마다 EventSystem이 없으면 UI 클릭이 먹통이 된다(일부 스테이지 씬엔 EventSystem이 없음).
        // 항상 살아있는 이 매니저가 씬 로드마다 하나 보장한다(이미 있으면 건너뜀 → 중복 없음).
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureEventSystem();
    }

    private void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureEventSystem();

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>(); // 레거시 Input Manager 사용.
    }

    private void Start()
    {
        // 게임 시작 시: 검은 화면 + 로딩 표시에서 시작해 페이드 인(요청된 "시작할 때 로딩 창").
        StartCoroutine(IntroRoutine());
    }

    // ───────────────────────── 공개 API ─────────────────────────

    /// <summary>페이드+로딩을 거쳐 지정한 씬을 비동기 로드한다.</summary>
    public void Load(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[SceneTransition] 빈 씬 이름으로 로드를 시도했습니다.");
            return;
        }
        if (IsTransitioning)
        {
            Debug.LogWarning($"[SceneTransition] 이미 전환 중이라 '{sceneName}' 요청을 무시합니다.");
            return;
        }
        StartCoroutine(TransitionRoutine(sceneName));
    }

    // ───────────────────────── 전환 루틴 ─────────────────────────

    private IEnumerator IntroRoutine()
    {
        SetAlpha(1f);
        _group.blocksRaycasts = true;
        ShowLoading(true);
        SetProgress(1f);
        yield return WaitRealtime(_minLoadingTime);
        ShowLoading(false);
        yield return Fade(1f, 0f);
        _group.blocksRaycasts = false;
    }

    private IEnumerator TransitionRoutine(string sceneName)
    {
        IsTransitioning = true;
        _group.blocksRaycasts = true;

        // 1) 검은 화면으로 페이드 아웃.
        yield return Fade(0f, 1f);

        // 2) 로딩 화면 표시 + 비동기 로드 시작(마지막 활성화는 보류).
        ShowLoading(true);
        SetProgress(0f);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Debug.LogError($"[SceneTransition] '{sceneName}' 씬을 로드할 수 없습니다. Build Settings 등록을 확인하세요.");
            ShowLoading(false);
            yield return Fade(1f, 0f);
            _group.blocksRaycasts = false;
            IsTransitioning = false;
            yield break;
        }
        op.allowSceneActivation = false;

        float elapsed = 0f;
        // 비동기 로드는 0.9에서 멈추고 activation을 기다린다.
        while (op.progress < 0.9f || elapsed < _minLoadingTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float loadProgress = Mathf.Clamp01(op.progress / 0.9f);
            float timeProgress = _minLoadingTime > 0f ? Mathf.Clamp01(elapsed / _minLoadingTime) : 1f;
            SetProgress(Mathf.Min(loadProgress, timeProgress));
            yield return null;
        }

        SetProgress(1f);
        yield return null;
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        // 3) 로딩 숨기고 페이드 인.
        ShowLoading(false);
        yield return Fade(1f, 0f);

        _group.blocksRaycasts = false;
        IsTransitioning = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        if (_fadeDuration <= 0f) { SetAlpha(to); yield break; }
        float t = 0f;
        while (t < _fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(from, to, t / _fadeDuration));
            yield return null;
        }
        SetAlpha(to);
    }

    private static IEnumerator WaitRealtime(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
    }

    // ───────────────────────── UI 구성 ─────────────────────────

    private void SetAlpha(float a) => _group.alpha = Mathf.Clamp01(a);

    private void SetProgress(float p)
    {
        p = Mathf.Clamp01(p);
        if (_barFill != null) _barFill.fillAmount = p;
        if (_percentText != null) _percentText.text = $"{Mathf.RoundToInt(p * 100f)}%";
    }

    private void ShowLoading(bool visible)
    {
        if (_loadingPanel != null) _loadingPanel.SetActive(visible);
    }

    private void BuildCanvas()
    {
        var canvasGO = new GameObject("TransitionCanvas");
        canvasGO.transform.SetParent(transform, false);

        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5000; // 다른 모든 UI 위.

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f; // 폭 기준(세로 고정)

        canvasGO.AddComponent<GraphicRaycaster>();
        _group = canvasGO.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;

        // 전체를 덮는 검은 배경.
        var bg = CreateFullScreenImage(canvasGO.transform, "Fade", new Color(0.04f, 0.04f, 0.05f, 1f));

        // 로딩 패널(중앙 하단).
        _loadingPanel = new GameObject("LoadingPanel", typeof(RectTransform));
        _loadingPanel.transform.SetParent(bg.transform, false);
        var lpRt = _loadingPanel.GetComponent<RectTransform>();
        lpRt.anchorMin = new Vector2(0.5f, 0f);
        lpRt.anchorMax = new Vector2(0.5f, 0f);
        lpRt.pivot = new Vector2(0.5f, 0f);
        lpRt.anchoredPosition = new Vector2(0f, 160f);
        lpRt.sizeDelta = new Vector2(720f, 120f);

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var title = CreateText(_loadingPanel.transform, "LOADING", 40, new Color(0.95f, 0.92f, 0.8f), font);
        var titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(0f, 56f);
        titleRt.anchoredPosition = Vector2.zero;
        title.alignment = TextAnchor.MiddleCenter;
        title.fontStyle = FontStyle.Bold;

        // 진행 바 배경.
        var barBg = new GameObject("BarBg", typeof(RectTransform));
        barBg.transform.SetParent(_loadingPanel.transform, false);
        var barBgImg = barBg.AddComponent<Image>();
        barBgImg.color = new Color(1f, 1f, 1f, 0.15f);
        var barBgRt = barBg.GetComponent<RectTransform>();
        barBgRt.anchorMin = new Vector2(0f, 0f);
        barBgRt.anchorMax = new Vector2(1f, 0f);
        barBgRt.pivot = new Vector2(0.5f, 0f);
        barBgRt.sizeDelta = new Vector2(0f, 22f);
        barBgRt.anchoredPosition = new Vector2(0f, 4f);

        // 진행 바 채움.
        var barFillGO = new GameObject("BarFill", typeof(RectTransform));
        barFillGO.transform.SetParent(barBg.transform, false);
        _barFill = barFillGO.AddComponent<Image>();
        _barFill.color = new Color(0.55f, 0.72f, 0.35f, 1f);
        _barFill.type = Image.Type.Filled;
        _barFill.fillMethod = Image.FillMethod.Horizontal;
        _barFill.fillOrigin = 0;
        _barFill.fillAmount = 0f;
        var barFillRt = barFillGO.GetComponent<RectTransform>();
        barFillRt.anchorMin = Vector2.zero;
        barFillRt.anchorMax = Vector2.one;
        barFillRt.offsetMin = Vector2.zero;
        barFillRt.offsetMax = Vector2.zero;

        // 퍼센트 텍스트.
        _percentText = CreateText(_loadingPanel.transform, "0%", 20, new Color(0.85f, 0.85f, 0.85f), font);
        var pRt = _percentText.rectTransform;
        pRt.anchorMin = new Vector2(1f, 0f);
        pRt.anchorMax = new Vector2(1f, 0f);
        pRt.pivot = new Vector2(1f, 1f);
        pRt.sizeDelta = new Vector2(80f, 24f);
        pRt.anchoredPosition = new Vector2(0f, -2f);
        _percentText.alignment = TextAnchor.MiddleRight;

        _loadingPanel.SetActive(false);
    }

    private static Image CreateFullScreenImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return img;
    }

    private static Text CreateText(Transform parent, string message, int size, Color color, Font font)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.text = message;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }
}
