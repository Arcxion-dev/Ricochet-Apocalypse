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
///
/// 주의: 정적(static) "이미 생성함" 플래그를 두지 않는다. 이 프로젝트는 Enter Play Mode 옵션에서
/// Domain Reload를 꺼둬(DisableDomainReload) static 값이 플레이 세션 간에 유지되는데, 플레이를 멈추면
/// DontDestroyOnLoad 인스턴스는 파괴되므로 static 플래그만 true로 남아 다음 세션부터 생성을 건너뛰는
/// 버그가 생긴다. RuntimeInitializeOnLoadMethod는 도메인 리로드 여부와 무관하게 매 플레이 세션마다
/// 다시 호출되므로, "현재 씬에 인스턴스가 있는가"라는 런타임 조건만으로 판단한다.
/// </summary>
public static class StageHudBootstrap
{
    private const string ResourcePath = "UI/StageHud";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        // 이미 씬에 배치/생성된 HUD가 있으면 새로 만들지 않는다(개발용 씬·중복 방지).
        if (Object.FindObjectOfType<StageHud>() != null) return;

        var prefab = Resources.Load<GameObject>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogError($"[StageHudBootstrap] '{ResourcePath}' 프리팹을 찾지 못했습니다. Resources/UI/StageHud.prefab 위치를 확인하세요.");
            return;
        }

        var instance = Object.Instantiate(prefab);
        instance.name = "StageHud (Persistent)";
        Object.DontDestroyOnLoad(instance);

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
