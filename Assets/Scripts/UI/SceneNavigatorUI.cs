using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// SceneLoader를 마우스로 조작하기 위한 단순한 씬 네비게이션 UI (uGUI Canvas + Button 방식).
/// - 런타임에 Screen Space Overlay Canvas와 Button들을 코드로 생성한다. 별도의 씬 UI 세팅이
///   필요 없어 이 컴포넌트만 붙이면 어느 씬에서든 동작한다.
/// - 현재 열려 있는 씬 이름에 따라 알맞은 버튼만 다시 구성한다(sceneLoaded 구독).
///     · Title : [스테이지 시작]
///     · Stage : [스테이지 초기화] [다음 스테이지] [타이틀로 돌아가기]
///     · 그 외(Shop/Result 등) : [다음 스테이지] [타이틀로 돌아가기]
///
/// 사용법: 아무 씬에나 빈 GameObject를 만들고 이 컴포넌트를 붙이면 된다.
/// (기본값 _persistAcrossScenes=true 이면 DontDestroyOnLoad로 씬 전환 후에도 유지된다.)
/// </summary>
public class SceneNavigatorUI : MonoBehaviour
{
    [Header("배치")]
    [Tooltip("씬을 전환해도 UI가 파괴되지 않고 유지될지 여부")]
    [SerializeField] private bool _persistAcrossScenes = true;

    [Tooltip("버튼 하나의 크기(px, 1920x1080 기준)")]
    [SerializeField] private Vector2 _buttonSize = new Vector2(260f, 64f);

    [Tooltip("버튼 사이 세로 간격(px)")]
    [SerializeField] private float _spacing = 12f;

    [Tooltip("화면 좌상단으로부터의 여백(px)")]
    [SerializeField] private Vector2 _margin = new Vector2(24f, 24f);

    private static SceneNavigatorUI _instance;

    private Canvas _canvas;
    private RectTransform _panel;
    private Text _statusLabel;
    private Font _font;

    private void Awake()
    {
        if (_persistAcrossScenes)
        {
            // 씬마다 UI가 중복 생성되지 않도록 단일 인스턴스만 유지.
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        EnsureEventSystem();
        BuildCanvas();

        SceneManager.sceneLoaded += OnSceneLoaded;
        RebuildButtons(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬이 바뀔 때마다 해당 씬에 맞는 버튼 구성으로 다시 만든다.
        RebuildButtons(scene.name);
    }

    // ───────────────────────── UI 구성 ─────────────────────────

    /// <summary>uGUI가 마우스 클릭을 받으려면 EventSystem이 필요하다. 없으면 하나 만든다.</summary>
    private void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<StandaloneInputModule>(); // 프로젝트가 레거시 Input Manager를 사용.

        if (_persistAcrossScenes)
        {
            DontDestroyOnLoad(esGO);
        }
    }

    /// <summary>Screen Space Overlay Canvas와 좌상단 세로 버튼 패널을 만든다.</summary>
    private void BuildCanvas()
    {
        var canvasGO = new GameObject("SceneNavigatorCanvas");
        canvasGO.transform.SetParent(transform, false);

        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 999; // 다른 UI 위에 그려지도록.

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // 좌상단에 고정되는 세로 배치 패널.
        var panelGO = new GameObject("Panel", typeof(RectTransform));
        panelGO.transform.SetParent(_canvas.transform, false);

        _panel = panelGO.GetComponent<RectTransform>();
        _panel.anchorMin = new Vector2(0f, 1f);
        _panel.anchorMax = new Vector2(0f, 1f);
        _panel.pivot = new Vector2(0f, 1f);
        _panel.anchoredPosition = new Vector2(_margin.x, -_margin.y);

        var layout = panelGO.AddComponent<VerticalLayoutGroup>();
        layout.spacing = _spacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var fitter = panelGO.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    /// <summary>현재 씬에 맞춰 상태 라벨과 버튼들을 다시 만든다.</summary>
    private void RebuildButtons(string sceneName)
    {
        if (_panel == null) return;

        // 기존 자식(라벨/버튼) 정리.
        for (int i = _panel.childCount - 1; i >= 0; i--)
        {
            Destroy(_panel.GetChild(i).gameObject);
        }

        _statusLabel = AddLabel($"씬: {sceneName}  |  스테이지 인덱스: {SceneLoader.CurrentStageIndex}");

        if (sceneName == SceneLoader.SceneNames.Title)
        {
            AddButton("스테이지 시작", () => SceneLoader.LoadStage(0));
        }
        else if (sceneName == SceneLoader.SceneNames.Stage)
        {
            AddButton("스테이지 초기화", () => SceneLoader.ResetStage());
            AddButton("다음 스테이지", () => SceneLoader.LoadNextStage());
            AddButton("타이틀로 돌아가기", () => SceneLoader.LoadTitle());
        }
        else
        {
            // Shop/Result 등 기타 씬: 진행/복귀만 노출.
            AddButton("다음 스테이지", () => SceneLoader.LoadNextStage());
            AddButton("타이틀로 돌아가기", () => SceneLoader.LoadTitle());
        }
    }

    // ───────────────────────── 위젯 생성 헬퍼 ─────────────────────────

    /// <summary>패널 상단의 상태 표시 라벨을 만든다.</summary>
    private Text AddLabel(string message)
    {
        var go = new GameObject("StatusLabel", typeof(RectTransform));
        go.transform.SetParent(_panel, false);

        var text = go.AddComponent<Text>();
        text.font = _font;
        text.text = message;
        text.fontSize = 20;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = _buttonSize.x * 1.4f;
        le.preferredHeight = 28f;

        return text;
    }

    /// <summary>클릭 시 지정한 동작을 실행하는 uGUI 버튼을 만든다.</summary>
    private Button AddButton(string label, UnityAction onClick)
    {
        var go = new GameObject($"Button_{label}", typeof(RectTransform));
        go.transform.SetParent(_panel, false);

        var image = go.AddComponent<Image>();
        image.color = new Color(0.16f, 0.18f, 0.22f, 0.92f);

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        // 마우스 오버/클릭 시 색 변화(피드백).
        var colors = button.colors;
        colors.normalColor = new Color(0.16f, 0.18f, 0.22f, 0.92f);
        colors.highlightedColor = new Color(0.26f, 0.30f, 0.38f, 1f);
        colors.pressedColor = new Color(0.10f, 0.12f, 0.16f, 1f);
        button.colors = colors;

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = _buttonSize.x;
        le.preferredHeight = _buttonSize.y;

        // 버튼 라벨(가운데 정렬, 버튼 전체를 채움).
        var textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);

        var text = textGO.AddComponent<Text>();
        text.font = _font;
        text.text = label;
        text.fontSize = 24;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;

        var trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        return button;
    }
}
