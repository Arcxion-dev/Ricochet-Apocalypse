using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 게임 중(스테이지/상점) ESC로 여는 일시정지 메뉴. 다른 런타임 UI들과 같은 방식으로
/// 코드에서 캔버스를 만들고, 어느 씬에서 Play를 눌러도 존재하도록 자동 부트스트랩된다.
///
/// 메뉴 항목:
/// - <b>계속하기</b>: 메뉴를 닫고 게임 재개.
/// - <b>설정</b>: <see cref="SettingsUI"/> 패널.
/// - <b>저장하고 로비로 나가기</b>: 지금까지의 런(스테이지 진행/인벤토리/파츠/성적)을 저장하고 타이틀로.
/// - <b>저장하고 게임 종료</b>: 같은 내용을 저장하고 애플리케이션 종료.
///   → 둘 다 다음에 타이틀에서 PLAY를 누르면 저장된 지점(그 스테이지 또는 상점)에서 이어서 시작한다.
///
/// 주의: 여기서 저장하는 건 "진행 중인 런"이며, 죽으면(로그라이크) 그 런 데이터는 지워지고
/// 다음 판은 1스테이지부터 다시 시작한다(<see cref="SceneLoader.FinishRun"/>).
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    private static PauseMenuUI _instance;

    /// <summary>일시정지 메뉴가 열려 있는지(다른 시스템이 입력을 막는 데 쓴다).</summary>
    public static bool IsOpen => _instance != null && _instance._canvas != null && _instance._canvas.enabled;

    [SerializeField] private KeyCode _toggleKey = KeyCode.Escape;

    private Canvas _canvas;
    private Font _font;
    private Text _statusLabel;

    /// <summary>첫 씬 로드 후 인스턴스가 없으면 하나 만들어 둔다(씬 세팅 불필요).</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("PauseMenuUI");
        go.AddComponent<PauseMenuUI>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureEventSystem();
        BuildCanvas();
        SetVisible(false);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (_instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // 씬이 바뀌면(로비로 나가기 등) 메뉴는 항상 닫힌 상태로 시작한다.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SetVisible(false);

    private void Update()
    {
        if (!Input.GetKeyDown(_toggleKey)) return;

        if (IsOpen) { SetVisible(false); return; }
        if (!CanOpenHere()) return;

        SetVisible(true);
    }

    /// <summary>메뉴가 열려 있는 동안은 게임을 멈춘 상태로 붙잡아 둔다(다른 시스템이 되돌려도 유지).</summary>
    private void LateUpdate()
    {
        if (IsOpen) Time.timeScale = 0f;
    }

    /// <summary>지금 이 씬/상황에서 ESC로 일시정지 메뉴를 열어도 되는지.</summary>
    private bool CanOpenHere()
    {
        string scene = SceneManager.GetActiveScene().name;
        bool inGame = SceneLoader.IsStageScene(scene) || scene == SceneLoader.SceneNames.Shop;
        if (!inGame) return false; // 타이틀/결과 화면엔 자체 메뉴가 있다.

        // 다른 전체화면 UI가 ESC를 이미 쓰고 있으면 양보한다.
        if (InventoryUI.IsOpen || WeaponPartsUI.IsOpen) return false;

        // 클리어 결과 창이 떠 있는 동안은 막는다. 이 시점엔 보상이 이미 지급됐는데 세이브의
        // 이어하기 지점은 아직 그 스테이지라, 여기서 나갔다 들어오면 같은 스테이지를 다시 깨서
        // 보상을 중복으로 받을 수 있다([확인]을 눌러 다음 지점으로 넘어간 뒤에 나가게 한다).
        if (StageClearUI.Instance != null && StageClearUI.Instance.IsShowing) return false;
        if (PlayerShooter.Active != null && PlayerShooter.Active.IsInSelectionMode) return false;
        if (SceneTransition.Instance != null && SceneTransition.Instance.IsTransitioning) return false;

        return true;
    }

    private void SetVisible(bool visible)
    {
        if (_canvas == null) return;
        if (_canvas.enabled == visible) return;

        _canvas.enabled = visible;

        if (visible)
        {
            RefreshStatus();
            Time.timeScale = 0f;
        }
        else
        {
            SettingsUI.Hide();
            Time.timeScale = 1f;
        }
    }

    private void RefreshStatus()
    {
        if (_statusLabel == null) return;

        int stageNo = SceneLoader.CurrentStageIndex + 1;
        int stageCount = SceneLoader.StageCount;
        string where = SceneManager.GetActiveScene().name == SceneLoader.SceneNames.Shop
            ? "상점"
            : $"스테이지 {stageNo} / {stageCount}";

        _statusLabel.text = $"{where}   |   보유 골드 {ShopManager.CurrentGold}";
    }

    // ───────────────────────── 버튼 동작 ─────────────────────────

    private void OnResume() => SetVisible(false);

    private void OnSettings() => SettingsUI.Show();

    /// <summary>진행 중인 런을 저장하고 타이틀(로비)로 나간다.</summary>
    private void OnSaveAndExitToLobby()
    {
        SaveRun();
        SetVisible(false);
        SceneLoader.LoadTitle();
    }

    /// <summary>진행 중인 런을 저장하고 게임을 종료한다.</summary>
    private void OnSaveAndQuit()
    {
        SaveRun();
        SetVisible(false);

        Debug.Log("[PauseMenuUI] 저장 후 게임 종료");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SaveRun()
    {
        if (SaveManager.Instance == null)
        {
            Debug.LogWarning("[PauseMenuUI] SaveManager가 없어 저장하지 못했습니다.");
            return;
        }
        SaveManager.Instance.Save();
    }

    // ───────────────────────── UI 구성 ─────────────────────────

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<StandaloneInputModule>(); // 레거시 Input Manager 사용.
    }

    private void BuildCanvas()
    {
        var canvasGO = new GameObject("PauseMenuCanvas");
        canvasGO.transform.SetParent(transform, false);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 1100; // 게임 UI 위, 설정 패널(1200) 아래.

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // 반투명 배경(뒤 클릭 차단).
        var dim = new GameObject("Dim", typeof(RectTransform));
        dim.transform.SetParent(canvasGO.transform, false);
        var dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.65f);
        StretchFull(dim.GetComponent<RectTransform>());

        // 중앙 패널.
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(dim.transform, false);
        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0.16f, 0.19f, 0.15f, 0.98f);
        var pRt = panel.GetComponent<RectTransform>();
        pRt.anchorMin = pRt.anchorMax = new Vector2(0.5f, 0.5f);
        pRt.pivot = new Vector2(0.5f, 0.5f);
        pRt.sizeDelta = new Vector2(560f, 0f);

        var layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 24, 24);
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childAlignment = TextAnchor.UpperCenter;

        var fitter = panel.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        AddLabel(pRt, "일시정지", 36, FontStyle.Bold, new Color(0.95f, 0.93f, 0.82f), 52f);
        _statusLabel = AddLabel(pRt, string.Empty, 20, FontStyle.Normal, new Color(0.78f, 0.82f, 0.70f), 30f);

        AddButton(pRt, "계속하기", new Color(0.25f, 0.35f, 0.22f), OnResume);
        AddButton(pRt, "설정", new Color(0.22f, 0.28f, 0.20f), OnSettings);
        AddButton(pRt, "저장하고 로비로 나가기", new Color(0.28f, 0.30f, 0.42f), OnSaveAndExitToLobby);
        AddButton(pRt, "저장하고 게임 종료", new Color(0.42f, 0.20f, 0.18f), OnSaveAndQuit);

        AddLabel(pRt, "저장한 지점에서 이어서 시작합니다. (사망 시엔 1스테이지부터 다시 시작)",
                 16, FontStyle.Italic, new Color(0.70f, 0.72f, 0.64f), 26f);
    }

    private Text AddLabel(Transform parent, string text, int size, FontStyle style, Color color, float height)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = _font; t.text = text; t.fontSize = size; t.fontStyle = style;
        t.color = color; t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
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
        var le = go.AddComponent<LayoutElement>(); le.preferredHeight = 52f;

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
