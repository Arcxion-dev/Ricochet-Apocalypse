using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 스테이지 진입 시 뜨는 "준비" 오버레이(코드 생성 uGUI). 모든 스테이지는 준비 상태로 시작한다.
/// - [시작] : <see cref="GameManager.StartStage"/>로 전환해 조준/격발을 허용하고 창을 닫는다.
/// - [상점] : 준비 상태에서 상점을 다시 이용한다(<see cref="SceneLoader.LoadShop"/>).
///
/// 준비 상태 동안 <see cref="PlayerShooter"/>는 <see cref="GameManager.StageStarted"/>가 false라
/// 조준선을 숨기고 격발/아이템 입력을 막는다.
///
/// HitFeedbackManager와 동일하게 자동 부트스트랩되는 씬 무관 싱글턴이며, 스테이지 씬에서만 표시된다.
/// </summary>
public class StageReadyUI : MonoBehaviour
{
    public static StageReadyUI Instance { get; private set; }

    private Font _font;
    private Canvas _canvas;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("StageReadyUI");
        go.AddComponent<StageReadyUI>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureEventSystem();
        BuildCanvas();

        SceneManager.sceneLoaded += OnSceneLoaded;
        Evaluate(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Evaluate(scene.name);

    /// <summary>스테이지 씬이면 준비 오버레이를 켜고 게임을 준비 상태로, 아니면 숨긴다.</summary>
    private void Evaluate(string sceneName)
    {
        bool isStage = SceneLoader.IsStageScene(sceneName);
        if (isStage)
        {
            GameManager.Instance?.SetStageReady();
            Show();
        }
        else
        {
            Hide();
        }
    }

    private void Show()
    {
        _canvas.enabled = true;
        _canvas.transform.SetAsLastSibling();
    }

    private void Hide() => _canvas.enabled = false;

    private void OnStartClicked()
    {
        Hide();
        GameManager.Instance?.StartStage();
    }

    private void OnShopClicked()
    {
        Hide();
        SceneLoader.LoadShop();
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
        var canvasGO = new GameObject("StageReadyCanvas");
        canvasGO.transform.SetParent(transform, false);

        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 800;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f; // 폭 기준(세로 고정)

        canvasGO.AddComponent<GraphicRaycaster>();

        // 제스처바/노치를 피하기 위한 안전영역 루트.
        var safeGO = new GameObject("SafeArea", typeof(RectTransform));
        safeGO.transform.SetParent(_canvas.transform, false);
        var safe = (RectTransform)safeGO.transform;
        safe.anchorMin = Vector2.zero;
        safe.anchorMax = Vector2.one;
        safe.offsetMin = Vector2.zero;
        safe.offsetMax = Vector2.zero;
        safeGO.AddComponent<SafeAreaFitter>();

        // 하단 중앙 준비 패널(플레이 화면을 가리지 않도록 화면 전체를 덮지 않는다).
        var winGO = new GameObject("ReadyPanel", typeof(RectTransform));
        winGO.transform.SetParent(safe, false);
        var winImg = winGO.AddComponent<Image>();
        winImg.color = new Color(0.08f, 0.10f, 0.14f, 0.92f);
        var win = winGO.GetComponent<RectTransform>();
        win.anchorMin = new Vector2(0.5f, 0f);
        win.anchorMax = new Vector2(0.5f, 0f);
        win.pivot = new Vector2(0.5f, 0f);
        win.anchoredPosition = new Vector2(0f, 40f);
        win.sizeDelta = new Vector2(520f, 150f);

        var layout = winGO.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.padding = new RectOffset(20, 20, 14, 16);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var title = AddText(win, "준비 — 상점을 이용하거나 시작하세요", 20, new Color(1f, 0.95f, 0.7f), FontStyle.Bold);
        title.alignment = TextAnchor.MiddleCenter;

        // 버튼 두 개를 가로로 배치.
        var rowGO = new GameObject("ButtonRow", typeof(RectTransform));
        rowGO.transform.SetParent(win, false);
        var row = rowGO.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 16f;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = true;
        row.childForceExpandHeight = true;
        var rowLe = rowGO.AddComponent<LayoutElement>();
        rowLe.preferredHeight = 64f;

        var shopBtn = AddButton(rowGO.transform, "상점", new Color(0.20f, 0.28f, 0.40f, 0.98f));
        shopBtn.onClick.AddListener(OnShopClicked);

        var startBtn = AddButton(rowGO.transform, "시작", new Color(0.18f, 0.34f, 0.20f, 0.98f));
        startBtn.onClick.AddListener(OnStartClicked);
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
        text.alignment = TextAnchor.MiddleCenter;
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
}
