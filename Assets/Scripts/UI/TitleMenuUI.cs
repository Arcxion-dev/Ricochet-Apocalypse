using DG.Tweening;
using TMPro;
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

    [Header("PLAY 라벨 (세이브 유무로 이어하기/새로하기 토글)")]
    [Tooltip("비워두면 PlayButton 자식 'Label'을 찾는다.")]
    [SerializeField] private TMP_Text _playLabel;
    [Tooltip("비워두면 PlayButton 자식 'Tag'를 찾는다. '▶ STAGE N' 형식.")]
    [SerializeField] private TMP_Text _playTag;

    [Header("등장 연출")]
    [Tooltip("타이틀 진입 시 순서대로 나타나는 요소들. 비워두면 SafeArea 자식 전체를 순서대로 쓴다.")]
    [SerializeField] private RectTransform[] _entranceOrder;
    [SerializeField] private RectTransform _logoLine1;
    [SerializeField] private RectTransform _logoLine2;

    private void Start()
    {
        // 세이브가 있으면 자동 복원 → PLAY가 이어하기로 동작.
        if (SaveManager.Instance != null && SaveManager.Instance.HasSave)
        {
            SaveManager.Instance.Load();
        }

        WireButtons();
        RefreshPlayButtonLabel();
        PlayEntrance();
    }

    /// <summary>
    /// 세이브 유무에 따라 PLAY 버튼 라벨을 갱신한다.
    /// - 세이브 있음 → "이어하기" + "▶ STAGE {복원된 진행 스테이지}"
    /// - 세이브 없음 → "새로하기" + "▶ STAGE 1"
    /// 세이브 삭제 후에는 SettingsUI가 타이틀을 다시 로드하므로 Start에서 이 메서드가 다시 돌아 반영된다.
    /// </summary>
    private void RefreshPlayButtonLabel()
    {
        bool hasSave = SaveManager.Instance != null && SaveManager.Instance.HasSave;

        if (_playLabel == null && _playButton != null) _playLabel = FindChildText(_playButton.transform, "Label");
        if (_playTag == null && _playButton != null) _playTag = FindChildText(_playButton.transform, "Tag");

        if (_playLabel != null) _playLabel.text = hasSave ? "이어하기" : "새로하기";
        // Start 앞부분에서 세이브가 있으면 Load()로 CurrentStageIndex가 복원돼 있고, 없으면 0(→ STAGE 1)이다.
        if (_playTag != null) _playTag.text = $"▶ STAGE {SceneLoader.CurrentStageIndex + 1}";
    }

    private static TMP_Text FindChildText(Transform parent, string childName)
    {
        var child = parent.Find(childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    // ───────────────────────── 등장 연출 ─────────────────────────

    /// <summary>로고가 먼저 자리를 잡고, 나머지 요소가 아래에서 차례로 올라온다.</summary>
    private void PlayEntrance()
    {
        var order = _entranceOrder;
        if (order == null || order.Length == 0) order = CollectDefaultOrder();

        for (int i = 0; i < order.Length; i++)
        {
            if (order[i] == null) continue;
            UIAnim.RiseIn(order[i], 0.05f + i * 0.06f, 28f, UIAnim.Slow);
        }

        // 로고는 은은하게 계속 숨 쉬게 둔다(정적인 타이틀 방지).
        Breathe(_logoLine1, 0f);
        Breathe(_logoLine2, 0.35f);
    }

    private RectTransform[] CollectDefaultOrder()
    {
        var safeArea = transform.Find("SafeArea");
        var root = safeArea != null ? safeArea : transform;
        var list = new System.Collections.Generic.List<RectTransform>();
        for (int i = 0; i < root.childCount; i++)
            if (root.GetChild(i) is RectTransform rt && rt.gameObject.activeSelf) list.Add(rt);
        return list.ToArray();
    }

    private static void Breathe(RectTransform rt, float delay)
    {
        if (rt == null) return;
        rt.DOScale(1.015f, 2.4f)
          .SetDelay(delay)
          .SetEase(Ease.InOutSine)
          .SetLoops(-1, LoopType.Yoyo)
          .SetUpdate(true)
          .SetLink(rt.gameObject);
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
