using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면 메뉴 컨트롤러. 씬에 배치된 대나무 패널(PLAY/SETTINGS/EXIT 텍스트가 그려진 스프라이트)
/// 위의 투명 버튼들을 이름으로 찾아 클릭을 연결한다.
///
/// 버튼 GameObject 이름 규약(씬 빌드 시 사용): PlayButton / SettingsButton / ExitButton.
/// (선택) NewGameButton 이 있으면 "새로 시작"으로 연결한다.
/// - PLAY: 중간 저장해 둔 런이 있으면 그 지점(스테이지 또는 상점)에서 <b>이어하기</b>,
///         없으면 1스테이지부터 <b>새 게임</b>.
/// - SETTINGS: <see cref="SettingsUI"/> 패널 표시.
/// - EXIT: 게임 종료(에디터에서는 플레이 정지).
///
/// 로그라이크라 죽으면 런 데이터가 지워지므로(<see cref="SceneLoader.FinishRun"/>), 사망 후 타이틀에
/// 오면 이어할 런이 없어 PLAY는 자동으로 1스테이지부터 시작하는 새 게임이 된다.
/// </summary>
public class TitleMenuUI : MonoBehaviour
{
    [Tooltip("비워두면 자식에서 이름(PlayButton/SettingsButton/ExitButton)으로 찾는다.")]
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _exitButton;

    [Tooltip("(선택) 이어하기와 별개로 항상 1스테이지부터 시작하는 버튼. 없으면 무시.")]
    [SerializeField] private Button _newGameButton;

    [Header("이어하기 안내")]
    [Tooltip("이어할 런이 있을 때 화면 하단에 안내 문구를 띄운다.")]
    [SerializeField] private bool _showContinueHint = true;

    /// <summary>중간 저장해 둔 런이 있어 PLAY가 "이어하기"로 동작하는지.</summary>
    private bool _hasActiveRun;

    /// <summary>이어하기로 돌아갈 스테이지 번호(1부터). 안내 문구용.</summary>
    private int _resumeStageNumber = 1;

    private void Start()
    {
        // 파일을 한 번만 읽어 "이어할 런이 있는지"와 그 지점을 확인한다(복원은 PLAY를 눌러야 한다).
        var saved = SaveManager.Instance != null ? SaveManager.Instance.Peek() : null;
        _hasActiveRun = saved != null && saved.hasActiveRun;
        if (_hasActiveRun) _resumeStageNumber = saved.currentStageIndex + 1;

        // 이어할 런이 없으면 이전 판의 잔여 상태(인벤토리/진행도)를 확실히 비운다.
        if (!_hasActiveRun) SaveManager.Instance?.StartNewRun();

        WireButtons();
        if (_showContinueHint && _hasActiveRun) ShowContinueHint();
    }

    private void WireButtons()
    {
        if (_playButton == null) _playButton = FindButton("PlayButton");
        if (_settingsButton == null) _settingsButton = FindButton("SettingsButton");
        if (_exitButton == null) _exitButton = FindButton("ExitButton");
        if (_newGameButton == null) _newGameButton = FindButton("NewGameButton");

        if (_playButton != null) _playButton.onClick.AddListener(OnPlay);
        if (_settingsButton != null) _settingsButton.onClick.AddListener(OnSettings);
        if (_exitButton != null) _exitButton.onClick.AddListener(OnExit);
        if (_newGameButton != null) _newGameButton.onClick.AddListener(OnNewGame);

        if (_playButton == null || _settingsButton == null || _exitButton == null)
            Debug.LogWarning("[TitleMenuUI] 일부 버튼을 찾지 못했습니다. 씬의 버튼 이름(PlayButton/SettingsButton/ExitButton)을 확인하세요.");
    }

    private Button FindButton(string goName)
    {
        foreach (var btn in GetComponentsInChildren<Button>(true))
        {
            if (btn.gameObject.name == goName) return btn;
        }
        return null;
    }

    // ───────────────────────── 버튼 동작 ─────────────────────────

    private void OnPlay()
    {
        SettingsUI.Hide();

        if (_hasActiveRun)
        {
            Debug.Log("[TitleMenuUI] 이어하기");
            SceneLoader.ResumeRun();
        }
        else
        {
            Debug.Log("[TitleMenuUI] 새 게임 (1스테이지부터)");
            SceneLoader.StartNewRun();
        }
    }

    /// <summary>저장된 런을 버리고 1스테이지부터 새로 시작한다.</summary>
    private void OnNewGame()
    {
        SettingsUI.Hide();
        Debug.Log("[TitleMenuUI] 새로 시작 (저장된 런 폐기)");
        SceneLoader.StartNewRun();
    }

    private void OnSettings()
    {
        SettingsUI.Show();
    }

    private void OnExit()
    {
        Debug.Log("[TitleMenuUI] 게임 종료");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ───────────────────────── 이어하기 안내 문구 ─────────────────────────

    /// <summary>PLAY가 이어하기로 동작한다는 걸 알리는 작은 라벨을 화면 하단에 만든다.</summary>
    private void ShowContinueHint()
    {
        var canvasGO = new GameObject("ContinueHintCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 800;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        var go = new GameObject("ContinueHint", typeof(RectTransform));
        go.transform.SetParent(canvasGO.transform, false);
        var text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 22;
        text.color = new Color(0.95f, 0.90f, 0.62f, 0.95f);
        text.alignment = TextAnchor.LowerCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.text = $"PLAY = 이어하기 (스테이지 {_resumeStageNumber} 부터)";

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(0f, 40f);
        rt.anchoredPosition = new Vector2(0f, 28f);
    }
}
