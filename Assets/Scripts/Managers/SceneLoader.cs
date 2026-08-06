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
        /// 스테이지 씬 진행 순서. **Build Settings에 등록된 "Stage_{숫자}" 씬들을 번호순으로** 자동 구성한다.
        /// (본 게임 플레이 스테이지 = Assets/Scenes/Stages/Stage_01~Stage_40. 옛 "Stage1~5"는 규칙이 달라 제외됨.)
        /// 새 스테이지를 추가하려면 "Stage_NN" 씬을 Build Settings에 등록하기만 하면 된다.
        /// 하나도 없으면 기본값(Stage_01~03)으로 폴백한다. (소스 수정 불필요)
        /// </summary>
        public static string[] Stages => ResolveStagesFromBuildSettings();

        private static readonly System.Text.RegularExpressions.Regex StageNameRegex =
            new System.Text.RegularExpressions.Regex(@"^Stage_(\d+)$");

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

            if (found.Count == 0) return new[] { "Stage_01", "Stage_02", "Stage_03" };
            var arr = new string[found.Count];
            for (int i = 0; i < found.Count; i++) arr[i] = found[i].Value;
            return arr;
        }
    }

    /// <summary>현재 스테이지 진행 인덱스 (0부터).</summary>
    public static int CurrentStageIndex { get; private set; }

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
            Debug.Log($"[SceneLoader] 마지막 스테이지까지 클리어 → 결과 씬으로 이동");
            LoadResult();
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
