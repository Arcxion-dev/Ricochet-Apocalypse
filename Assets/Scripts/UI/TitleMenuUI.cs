using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면 메뉴 컨트롤러. 씬에 배치된 대나무 패널(PLAY/SETTINGS/EXIT 텍스트가 그려진 스프라이트)
/// 위의 투명 버튼들을 이름으로 찾아 클릭을 연결한다.
///
/// 버튼 GameObject 이름 규약(씬 빌드 시 사용): PlayButton / SettingsButton / ExitButton.
/// - PLAY: 세이브가 있으면 그 진행도에서 이어서, 없으면 첫 스테이지부터 시작.
/// - SETTINGS: <see cref="SettingsUI"/> 패널 표시.
/// - EXIT: 게임 종료(에디터에서는 플레이 정지).
///
/// 타이틀 진입 시 세이브가 있으면 자동으로 불러와(진행도/인벤토리 복원) PLAY가 "이어하기"로 동작한다.
/// </summary>
public class TitleMenuUI : MonoBehaviour
{
    [Tooltip("비워두면 자식에서 이름(PlayButton/SettingsButton/ExitButton)으로 찾는다.")]
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _exitButton;

    private void Start()
    {
        // 세이브가 있으면 자동 복원 → PLAY가 이어하기로 동작.
        if (SaveManager.Instance != null && SaveManager.Instance.HasSave)
        {
            SaveManager.Instance.Load();
        }

        WireButtons();
    }

    private void WireButtons()
    {
        if (_playButton == null) _playButton = FindButton("PlayButton");
        if (_settingsButton == null) _settingsButton = FindButton("SettingsButton");
        if (_exitButton == null) _exitButton = FindButton("ExitButton");

        if (_playButton != null) _playButton.onClick.AddListener(OnPlay);
        if (_settingsButton != null) _settingsButton.onClick.AddListener(OnSettings);
        if (_exitButton != null) _exitButton.onClick.AddListener(OnExit);

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
        SceneLoader.LoadStage(SceneLoader.CurrentStageIndex);
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
}
