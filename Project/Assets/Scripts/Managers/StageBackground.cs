using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 스테이지 씬마다 배경 스프라이트(배경1/배경2 중 랜덤 하나)를 인게임 플레이 배경으로 깔아준다.
/// - 스프라이트는 <c>Resources/실제 사용할 리소스/배경/배경1·배경2</c>에서 로드한다.
/// - 스테이지에 진입할 때마다 둘 중 하나를 무작위로 골라 표시하고, 그 외 씬(타이틀/상점/결과)에선 숨긴다.
/// - 카메라를 따라다니며 뷰포트를 가득 채우도록 매 프레임 위치·크기를 맞춰(팬/줌과 무관하게 빈틈 없음),
///   타일맵 바닥(sortingOrder -10)보다 뒤(sortingOrder -100)에 그려진다.
///
/// 씬 배치 없이 부트스트랩으로 상주한다(스테이지마다 배경 오브젝트를 일일이 두지 않아도 됨).
/// (도메인 리로드 OFF 대비: static "생성함" 플래그 대신 현재 인스턴스 유무를 런타임 검사한다 —
///  <see cref="StageBgm"/>·StageHudBootstrap과 동일한 이유. [[enter-playmode-disable-domain-reload]])
/// </summary>
public class StageBackground : MonoBehaviour
{
    private const string BgFolder = "실제 사용할 리소스/배경/";
    private static readonly string[] BgNames = { "배경1", "배경2" };

    /// <summary>타일맵 바닥(-10)·맵 배경모듈(-100)보다 확실히 뒤에 오도록 하는 정렬 순서.</summary>
    private const int BgSortingOrder = -200;

    /// <summary>뷰포트를 완전히 덮도록 곱하는 여유 배율(줌/부동소수 오차로 가장자리에 틈이 생기지 않게).</summary>
    private const float CoverMargin = 1.05f;

    private static StageBackground _instance;

    private SpriteRenderer _sr;
    private Sprite[] _sprites;
    private Camera _cam;
    private bool _visible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null || FindObjectOfType<StageBackground>() != null) return;
        var go = new GameObject("StageBackground");
        DontDestroyOnLoad(go);
        go.AddComponent<StageBackground>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // 배경 스프라이트 로드.
        _sprites = new Sprite[BgNames.Length];
        for (int i = 0; i < BgNames.Length; i++)
        {
            _sprites[i] = Resources.Load<Sprite>(BgFolder + BgNames[i]);
            if (_sprites[i] == null)
                Debug.LogWarning($"[StageBackground] Resources/{BgFolder}{BgNames[i]} 스프라이트를 찾을 수 없습니다.");
        }

        // 배경 렌더러 생성(자식 오브젝트). 항상 맨 뒤에 그린다.
        var visual = new GameObject("BackgroundSprite");
        visual.transform.SetParent(transform, false);
        _sr = visual.AddComponent<SpriteRenderer>();
        _sr.sortingLayerName = "Default";
        _sr.sortingOrder = BgSortingOrder;
        _sr.enabled = false;

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

    /// <summary>스테이지면 둘 중 하나를 랜덤으로 골라 표시하고, 아니면 숨긴다.</summary>
    private void Apply(Scene scene)
    {
        bool isStage = !string.IsNullOrEmpty(scene.name) && scene.name.StartsWith("Stage");
        _cam = Camera.main; // 씬마다 카메라가 새로 생기므로 다시 잡는다.

        if (!isStage)
        {
            _visible = false;
            if (_sr != null) _sr.enabled = false;
            return;
        }

        // 스테이지 진입 시마다 배경1/배경2 중 하나를 무작위로 선택.
        var pick = PickRandomSprite();
        if (pick == null)
        {
            _visible = false;
            if (_sr != null) _sr.enabled = false;
            return;
        }

        _sr.sprite = pick;
        _sr.enabled = true;
        _visible = true;
        FitToCamera(); // 첫 프레임부터 올바른 크기/위치로.
    }

    private Sprite PickRandomSprite()
    {
        // null(로드 실패)인 슬롯은 제외하고 고른다.
        int valid = 0;
        for (int i = 0; i < _sprites.Length; i++) if (_sprites[i] != null) valid++;
        if (valid == 0) return null;

        int target = Random.Range(0, valid);
        for (int i = 0; i < _sprites.Length; i++)
        {
            if (_sprites[i] == null) continue;
            if (target == 0) return _sprites[i];
            target--;
        }
        return null;
    }

    private void LateUpdate()
    {
        if (!_visible) return;
        if (_cam == null) _cam = Camera.main;
        FitToCamera();
    }

    /// <summary>배경을 카메라 정면에 두고 현재 뷰포트(줌 반영)를 가득 덮도록 크기를 맞춘다.</summary>
    private void FitToCamera()
    {
        if (_cam == null || _sr == null || _sr.sprite == null) return;

        // 카메라 XY를 따라가며, Z는 카메라보다 앞(월드에서 더 먼 쪽)에 둔다.
        Vector3 camPos = _cam.transform.position;
        _sr.transform.position = new Vector3(camPos.x, camPos.y, camPos.z + 10f);

        float worldH = _cam.orthographic
            ? _cam.orthographicSize * 2f
            : 2f * (_sr.transform.position.z - camPos.z) * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float worldW = worldH * _cam.aspect;

        Vector2 spriteSize = _sr.sprite.bounds.size; // 스케일 1일 때의 월드 크기.
        if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;

        // 화면을 완전히 덮도록(cover) 가로/세로 배율 중 큰 값을 사용(빈틈 방지, 일부 크롭 허용).
        float scale = Mathf.Max(worldW / spriteSize.x, worldH / spriteSize.y) * CoverMargin;
        _sr.transform.localScale = new Vector3(scale, scale, 1f);
    }
}
