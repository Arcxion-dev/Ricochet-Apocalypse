using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 전환을 담당하는 정적 유틸리티. UnityEngine.SceneManagement.SceneManager를 래핑한다.
/// - 실제 씬 에셋 이름은 SceneNames 상수로 참조한다 (플레이스홀더 씬, 추후 교체 가능).
/// - 스테이지는 <see cref="SceneNames.Stages"/> 배열에 나열된 개별 씬 파일(Stage1/2/3…)이다.
///   진행 인덱스(CurrentStageIndex)로 배열에서 씬 이름을 골라 로드하고, 클리어 시 다음 인덱스로
///   넘어간다. 마지막 스테이지를 넘어서면 Result 씬으로 이동한다.
///
/// 주의: 여기서 부르는 씬 이름들은 Build Settings에 등록되어 있어야 로드된다.
/// </summary>
public static class SceneLoader
{
    /// <summary>씬 에셋 이름 상수. 실제 씬 파일명과 반드시 일치해야 한다.</summary>
    public static class SceneNames
    {
        public const string Title = "Title";
        public const string Shop = "Shop";
        public const string Result = "Result";

        /// <summary>
        /// 스테이지 씬 진행 순서. **Build Settings에 등록된 "Stage{숫자}" 씬들을 번호순으로** 자동 구성한다.
        /// 새 스테이지를 추가하려면 그 씬을 Build Settings에 등록하기만 하면 된다(맵 에디터 '스테이지 복제'가 자동 등록).
        /// 하나도 없으면 기본값(Stage1~3)으로 폴백한다. (소스 수정 불필요)
        /// </summary>
        public static string[] Stages => ResolveStagesFromBuildSettings();

        private static readonly System.Text.RegularExpressions.Regex StageNameRegex =
            new System.Text.RegularExpressions.Regex(@"^Stage(\d+)$");

        private static string[] ResolveStagesFromBuildSettings()
        {
            var found = new List<KeyValuePair<int, string>>();
            int count = SceneManager.sceneCountInBuildSettings;
            for (int i = 0; i < count; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                var m = StageNameRegex.Match(name);
                if (m.Success && int.TryParse(m.Groups[1].Value, out int n))
                    found.Add(new KeyValuePair<int, string>(n, name));
            }
            found.Sort((a, b) => a.Key.CompareTo(b.Key));

            if (found.Count == 0) return new[] { "Stage1", "Stage2", "Stage3" };
            var arr = new string[found.Count];
            for (int i = 0; i < found.Count; i++) arr[i] = found[i].Value;
            return arr;
        }
    }

    /// <summary>현재 스테이지 진행 인덱스 (0부터).</summary>
    public static int CurrentStageIndex { get; private set; }

    /// <summary>
    /// 이 프로젝트는 Domain Reload가 꺼져 있어 정적 값이 플레이 세션 사이에 남는다.
    /// 플레이 시작 시 진행 인덱스를 0으로 되돌려, 이전 세션의 진행도가 새 플레이에 새지 않게 한다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnEnterPlayMode() => CurrentStageIndex = 0;

    /// <summary>세이브 불러오기 등 외부에서 진행 인덱스를 직접 설정한다(씬 로드는 하지 않음).</summary>
    public static void SetCurrentStageIndex(int index)
    {
        CurrentStageIndex = Mathf.Max(0, index);
    }

    /// <summary>등록된 전체 스테이지 수.</summary>
    public static int StageCount => SceneNames.Stages.Length;

    /// <summary>주어진 씬 이름이 스테이지 씬(Stage1/2/3…) 중 하나인지 여부.</summary>
    public static bool IsStageScene(string sceneName)
    {
        for (int i = 0; i < SceneNames.Stages.Length; i++)
        {
            if (SceneNames.Stages[i] == sceneName) return true;
        }
        return false;
    }

    /// <summary>
    /// 지금 열려 있는 씬 이름에 맞춰 진행 인덱스를 보정한다.
    /// 정상 흐름(LoadStage 경유)에선 이미 일치하므로 아무 일도 하지 않고, 에디터에서 스테이지 씬을
    /// 직접 Play한 경우에만 인덱스를 맞춰 준다(마지막 스테이지 판정/저장이 어긋나지 않도록).
    /// </summary>
    public static void SyncStageIndexToScene(string sceneName)
    {
        var stages = SceneNames.Stages;
        for (int i = 0; i < stages.Length; i++)
        {
            if (stages[i] != sceneName) continue;
            if (CurrentStageIndex != i)
            {
                Debug.Log($"[SceneLoader] 열린 씬에 맞춰 진행 인덱스 보정: {CurrentStageIndex} → {i} ({sceneName})");
                CurrentStageIndex = i;
            }
            return;
        }
    }

    /// <summary>주어진 인덱스가 마지막 스테이지인지(= 여기를 깨면 게임 최종 클리어).</summary>
    public static bool IsLastStageIndex(int index) => index >= SceneNames.Stages.Length - 1;

    /// <summary>현재 플레이 중인 스테이지가 마지막 스테이지인지.</summary>
    public static bool IsOnLastStage => IsLastStageIndex(CurrentStageIndex);

    /// <summary>타이틀로 이동하고 진행 인덱스를 초기화한다.</summary>
    public static void LoadTitle()
    {
        CurrentStageIndex = 0;
        LoadScene(SceneNames.Title);
    }

    /// <summary>
    /// 특정 스테이지 인덱스로 이동한다. 인덱스가 마지막 스테이지를 넘으면
    /// 모든 스테이지를 클리어한 것으로 보고 Result 씬으로 이동한다.
    /// </summary>
    public static void LoadStage(int index)
    {
        if (index < 0) index = 0;

        if (index >= SceneNames.Stages.Length)
        {
            // 마지막 스테이지 다음으로 넘어가려 한다 = 게임 최종 클리어.
            FinishRun(true);
            return;
        }

        CurrentStageIndex = index;
        string sceneName = SceneNames.Stages[CurrentStageIndex];
        Debug.Log($"[SceneLoader] 스테이지 {CurrentStageIndex} ({sceneName}) 로드");
        LoadScene(sceneName);
    }

    /// <summary>다음 스테이지로 이동한다.</summary>
    public static void LoadNextStage()
    {
        LoadStage(CurrentStageIndex + 1);
    }

    /// <summary>
    /// 현재(준비된) 스테이지 인덱스의 스테이지를 로드한다. 클리어 시 인덱스가 미리 전진되므로,
    /// 상점 '출발'과 준비 상태에서 상점을 다녀온 뒤 복귀는 이 메서드로 "같은 다음 스테이지"를 연다.
    /// </summary>
    public static void LaunchCurrentStage()
    {
        LoadStage(CurrentStageIndex);
    }

    /// <summary>현재 스테이지를 재시도한다 (실패 시).</summary>
    public static void ReloadStage()
    {
        string sceneName = CurrentStageIndex < SceneNames.Stages.Length
            ? SceneNames.Stages[CurrentStageIndex]
            : SceneNames.Stages[0];
        Debug.Log($"[SceneLoader] 스테이지 {CurrentStageIndex} ({sceneName}) 재시도");
        LoadScene(sceneName);
    }

    /// <summary>
    /// 현재 스테이지를 초기화한다. 콘솔에 "초기화"를 출력하고,
    /// GameManager 진행/스코어 상태를 리셋한 뒤 Stage 씬을 다시 로드한다.
    /// (씬을 다시 로드하면 GameManager.OnSceneLoaded에서 상태가 한 번 더 초기화된다.)
    /// </summary>
    public static void ResetStage()
    {
        Debug.Log("초기화");

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetStageState();
        }

        ReloadStage();
    }

    /// <summary>상점 씬으로 이동한다 (스테이지 클리어 후).</summary>
    public static void LoadShop()
    {
        LoadScene(SceneNames.Shop);
    }

    /// <summary>결과 씬으로 이동한다 (전체 종료).</summary>
    public static void LoadResult()
    {
        LoadScene(SceneNames.Result);
    }

    // ───────────────────────── 런 시작 / 종료 ─────────────────────────

    /// <summary>
    /// 한 판(런)을 끝내고 결과 씬으로 보낸다. 로그라이크 규칙상 여기서 런 데이터는 모두 지워지고,
    /// 다음 판은 1스테이지부터 빈 손으로 시작한다. 성적은 <see cref="RunResult"/>에 남아 결과 화면이 읽는다.
    /// </summary>
    /// <param name="cleared">최종 클리어면 true, 사망/실패면 false.</param>
    /// <param name="failReason">실패 사유(클리어면 무시).</param>
    public static void FinishRun(bool cleared, string failReason = null)
    {
        // 이미 종료 처리된 런이면 클리어 횟수가 중복 집계되지 않도록 결과 화면만 다시 띄운다.
        if (RunResult.LastOutcome != RunResult.Outcome.None)
        {
            LoadResult();
            return;
        }

        if (cleared)
        {
            RunResult.MarkCleared();
            Debug.Log("[SceneLoader] 게임 최종 클리어! → 결과 씬으로 이동");
        }
        else
        {
            RunResult.MarkFailed(failReason);
            Debug.Log($"[SceneLoader] 런 종료 ({failReason}) → 결과 씬으로 이동");
        }

        // 세이브의 런 데이터를 비운다(= 이어하기 불가, 다음엔 1스테이지부터).
        SaveManager.Instance?.EndRun(cleared);
        CurrentStageIndex = 0;

        LoadResult();
    }

    /// <summary>1스테이지부터 새 런을 시작한다(타이틀 "새 게임" / 결과 화면 "다시 도전").</summary>
    public static void StartNewRun()
    {
        SaveManager.Instance?.StartNewRun();
        RunResult.BeginRun();
        LoadStage(0);
    }

    /// <summary>
    /// 중간 저장해 둔 런을 이어서 시작한다. 상점에서 나갔으면 상점으로, 스테이지에서 나갔으면 그 스테이지로
    /// 돌아간다. 이어할 런이 없으면 새 런을 시작한다.
    /// </summary>
    public static void ResumeRun()
    {
        var save = SaveManager.Instance;
        if (save == null || save.Load() < 0)
        {
            StartNewRun();
            return;
        }

        string resume = save.ResumeScene;
        if (!string.IsNullOrEmpty(resume) && Application.CanStreamedLevelBeLoaded(resume))
        {
            Debug.Log($"[SceneLoader] 이어하기 → '{resume}' (스테이지 인덱스 {CurrentStageIndex})");
            LoadScene(resume);
            return;
        }

        // 복귀 씬 정보가 없거나 빌드에 없으면 저장된 스테이지 인덱스로 폴백.
        LoadStage(CurrentStageIndex);
    }

    /// <summary>이름으로 씬을 로드하는 저수준 진입점.</summary>
    public static void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[SceneLoader] 빈 씬 이름으로 로드를 시도했습니다.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[SceneLoader] '{sceneName}' 씬을 로드할 수 없습니다. Build Settings에 등록되어 있는지 확인하세요.");
            return;
        }

        // 오버레이 전환 매니저가 있으면 페이드+로딩을 거쳐 비동기 로드한다.
        // (플레이 중에는 항상 부트스트랩되어 존재. 없으면 즉시 동기 로드로 폴백.)
        if (SceneTransition.Instance != null)
        {
            SceneTransition.Instance.Load(sceneName);
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}
