using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 전환을 담당하는 정적 유틸리티. UnityEngine.SceneManagement.SceneManager를 래핑한다.
/// - 실제 씬 에셋 이름은 SceneNames 상수로 참조한다 (플레이스홀더 씬, 추후 교체 가능).
/// - 로그라이크 약 20스테이지는 데이터 주도로 확장 예정. 지금은 모든 스테이지가 동일한
///   Stage 플레이스홀더 씬을 열고, 진행 인덱스(CurrentStageIndex)만 증가시키는 배관만 둔다.
///
/// 주의: 여기서 부르는 씬 이름들은 Build Settings에 등록되어 있어야 로드된다.
/// </summary>
public static class SceneLoader
{
    /// <summary>씬 에셋 이름 상수. 실제 씬 파일명과 반드시 일치해야 한다.</summary>
    public static class SceneNames
    {
        public const string Title = "Title";
        public const string Stage = "Stage";
        public const string Shop = "Shop";
        public const string Result = "Result";
    }

    /// <summary>현재 스테이지 진행 인덱스 (0부터).</summary>
    public static int CurrentStageIndex { get; private set; }

    /// <summary>타이틀로 이동하고 진행 인덱스를 초기화한다.</summary>
    public static void LoadTitle()
    {
        CurrentStageIndex = 0;
        LoadScene(SceneNames.Title);
    }

    /// <summary>특정 스테이지 인덱스로 이동한다 (현재는 모두 동일한 Stage 씬을 로드).</summary>
    public static void LoadStage(int index)
    {
        CurrentStageIndex = Mathf.Max(0, index);
        Debug.Log($"[SceneLoader] 스테이지 {CurrentStageIndex} 로드");
        LoadScene(SceneNames.Stage);
    }

    /// <summary>다음 스테이지로 이동한다.</summary>
    public static void LoadNextStage()
    {
        LoadStage(CurrentStageIndex + 1);
    }

    /// <summary>현재 스테이지를 재시도한다 (실패 시).</summary>
    public static void ReloadStage()
    {
        Debug.Log($"[SceneLoader] 스테이지 {CurrentStageIndex} 재시도");
        LoadScene(SceneNames.Stage);
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

        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogError($"[SceneLoader] '{sceneName}' 씬을 로드할 수 없습니다. Build Settings에 등록되어 있는지 확인하세요.");
        }
    }
}
