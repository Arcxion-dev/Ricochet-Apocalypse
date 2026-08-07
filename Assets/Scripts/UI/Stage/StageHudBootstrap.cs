using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// <see cref="StageHud"/>(잔탄·탄환 실린더 등 인게임 "총알 확인 UI")를 게임 시작 시 한 번만
/// 생성해 씬 전환에도 유지시키는 부트스트랩. 스테이지마다 프리팹을 일일이 배치하지 않아도
/// 모든 씬에서 HUD가 동작한다.
///
/// - 프리팹은 <c>Resources/UI/StageHud</c>에서 로드한다(코드 전용 부트스트랩이라 Resources 필요).
/// - StageHud는 매 씬 Player/PlayerShooter를 폴링해 스테이지 밖(= 타이틀/상점 등)에서는
///   스스로 통째로 숨으므로, 단일 영구 인스턴스로 전 씬을 커버할 수 있다.
///
/// 참고: 옛 개발용 씬(예: Scenes/Stage1)에 StageHud가 직접 배치돼 있으면 그 씬에서만 중복될 수 있다.
/// 본 게임 흐름(Title + Stage_NN)에는 배치본이 없어 문제 없다.
/// </summary>
public static class StageHudBootstrap
{
    private const string ResourcePath = "UI/StageHud";

    private static bool _spawned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (_spawned) return;

        // 이미 씬에 배치된 HUD가 있으면 그걸 쓰고 새로 만들지 않는다(개발용 씬 대비).
        if (Object.FindObjectOfType<StageHud>() != null)
        {
            _spawned = true;
            return;
        }

        var prefab = Resources.Load<GameObject>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogError($"[StageHudBootstrap] '{ResourcePath}' 프리팹을 찾지 못했습니다. Resources/UI/StageHud.prefab 위치를 확인하세요.");
            return;
        }

        var instance = Object.Instantiate(prefab);
        instance.name = "StageHud (Persistent)";
        Object.DontDestroyOnLoad(instance);
        _spawned = true;

        EnsureEventSystem();
    }

    /// <summary>
    /// HUD 버튼(설정/인벤/이동/퀵슬롯)이 클릭을 받으려면 EventSystem이 필요하다.
    /// 타이틀 경유 시엔 SceneNavigatorUI가 이미 영구 EventSystem을 만들지만,
    /// 스테이지 씬으로 바로 진입한 경우엔 없을 수 있으므로 없을 때만 하나 만든다(중복 방지).
    /// </summary>
    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        if (Object.FindObjectOfType<EventSystem>() != null) return;

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>(); // 프로젝트가 레거시 Input Manager 사용.
        Object.DontDestroyOnLoad(es);
    }
}
