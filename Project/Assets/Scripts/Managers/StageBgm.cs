using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 스테이지 씬에 진입하면 인게임 BGM(The_Unseen_Aim)을 루프 재생하고, 그 외 씬(타이틀/상점/결과)에선 멈춘다.
/// 씬 배치 없이 부트스트랩으로 상주한다. 실제 볼륨은 <see cref="SoundManager"/>가 설정의 BGM 슬라이더에 연동한다.
/// (도메인 리로드 OFF 대비: 부트스트랩에서 기존 인스턴스 유무를 런타임 검사해 중복 생성을 막는다.)
/// </summary>
public class StageBgm : MonoBehaviour
{
    private const string ClipResourcePath = "Sound/The_Unseen_Aim";

    private static StageBgm _instance;
    private AudioClip _clip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null || FindObjectOfType<StageBgm>() != null) return;
        var go = new GameObject("StageBgm");
        DontDestroyOnLoad(go);
        go.AddComponent<StageBgm>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        _clip = Resources.Load<AudioClip>(ClipResourcePath);
        if (_clip == null) Debug.LogWarning($"[StageBgm] Resources/{ClipResourcePath} 오디오를 찾을 수 없습니다.");

        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply(SceneManager.GetActiveScene()); // 부트스트랩 시점의 현재 씬도 즉시 반영.
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_instance == this) _instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) Apply(scene);
    }

    private void Apply(Scene scene)
    {
        var sm = SoundManager.Instance;
        if (sm == null) return; // SoundManager가 아직 없으면(스킵) 다음 씬 로드에서 다시 시도.

        bool isStage = !string.IsNullOrEmpty(scene.name) && scene.name.StartsWith("Stage");
        if (isStage && _clip != null) sm.PlayMusicClip(_clip, true);
        else sm.StopMusic();
    }
}
