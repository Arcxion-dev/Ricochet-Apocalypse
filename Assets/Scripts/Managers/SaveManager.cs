using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임 진행 상태를 JSON 파일로 저장/불러오는 싱글턴 매니저.
/// 저장 위치: <c>Application.persistentDataPath/save.json</c> (플랫폼별 유저 데이터 폴더).
///
/// 세이브는 두 종류의 정보를 한 파일에 담는다.
/// - <b>런(run) 데이터</b>: 지금 진행 중인 한 판의 상태 — 스테이지 인덱스, 이어할 씬, 인벤토리,
///   무기 파츠, 런 누계 성적(<see cref="RunResult"/>). 런이 끝나면(사망/최종 클리어) 전부 지워진다.
///   → 로그라이크 규칙: 죽으면 다음 판은 1스테이지부터, 소지품 없이 다시 시작한다.
/// - <b>메타 데이터</b>: 런이 끝나도 남는 기록 — 도달 최고 스테이지, 최종 클리어 횟수.
///
/// <see cref="HasActiveRun"/>가 true일 때만 타이틀의 PLAY가 "이어하기"로 동작한다
/// (= 중간에 <see cref="Save"/>하고 로비로 나갔거나 게임을 껐던 경우).
/// (오디오/화면 설정값은 <see cref="GameSettings"/>가 PlayerPrefs로 별도 영속화한다.)
///
/// 인벤토리 복원은 id로 <see cref="ItemDefinition"/>을 찾아야 하는데, Resources 폴더의 정의 에셋을
/// 우선 사용하고 없으면 런타임 정의로 대체한다(수량은 보존, 스택 규칙만 근사).
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [Serializable]
    public class SaveData
    {
        // ── 런 데이터(런 종료 시 초기화) ─────────────────────────
        public bool hasActiveRun;
        public int currentStageIndex;
        /// <summary>이어하기로 돌아갈 씬 이름(스테이지 씬 또는 Shop). 비어 있으면 스테이지 인덱스로 계산.</summary>
        public string resumeScene = string.Empty;
        public List<ItemSave> inventory = new List<ItemSave>();
        public List<string> activeWeaponParts = new List<string>();
        public RunStatsSave runStats = new RunStatsSave();

        // ── 메타 데이터(런이 끝나도 유지) ────────────────────────
        public int highestStageReached;
        /// <summary>게임을 끝까지 깬 횟수(최종 클리어).</summary>
        public int clearCount;

        public long savedAtTicks;
    }

    /// <summary>런 누계 성적 직렬화용(<see cref="RunResult"/>와 1:1).</summary>
    [Serializable]
    public class RunStatsSave
    {
        public int stagesCleared;
        public int totalKills;
        public int totalShots;
        public int bestCombo;
        public int totalReward;
        public int perfectStages;
    }

    [Serializable]
    public class ItemSave
    {
        public string id;
        public int category;
        public int quantity;
    }

    private static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

    /// <summary>세이브 파일이 존재하는지.</summary>
    public bool HasSave => File.Exists(FilePath);

    /// <summary>
    /// 세이브 파일 내용을 복원 없이 읽기만 한다(타이틀에서 "이어하기 가능?" 같은 조회용).
    /// 파일이 없거나 깨졌으면 null.
    /// </summary>
    public SaveData Peek() => ReadFromDisk();

    /// <summary>중간에 저장하고 나간 "진행 중인 런"이 있는지(타이틀 PLAY = 이어하기 판단용).</summary>
    public bool HasActiveRun
    {
        get
        {
            var data = ReadFromDisk();
            return data != null && data.hasActiveRun;
        }
    }

    /// <summary>지금까지의 최종 클리어 횟수(결과 화면 표시용).</summary>
    public int ClearCount
    {
        get
        {
            var data = ReadFromDisk();
            return data != null ? data.clearCount : 0;
        }
    }

    /// <summary><see cref="Load"/>가 읽어 둔, 이어하기로 돌아갈 씬 이름.</summary>
    public string ResumeScene { get; private set; }

    // 씬 로드 후 적용할 파츠 상태(스테이지 씬에 PlayerShooter가 뜬 뒤 반영).
    private List<string> _pendingActiveParts;

    // id → ItemDefinition 조회 카탈로그(Resources 스캔 + 런타임 등록).
    private Dictionary<string, ItemDefinition> _catalog;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("SaveManager");
        go.AddComponent<SaveManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 불러오기로 예약된 파츠 상태가 있으면, 이 씬의 PlayerShooter에 반영한다.
        if (_pendingActiveParts != null)
        {
            var shooter = FindObjectOfType<PlayerShooter>();
            if (shooter != null)
            {
                ApplyWeaponParts(shooter, _pendingActiveParts);
                _pendingActiveParts = null;
            }
        }
    }

    // ───────────────────────── 저장 ─────────────────────────

    /// <summary>
    /// 진행 중인 런을 파일로 저장한다(중간 저장). 저장 후 게임을 꺼도 타이틀 PLAY로 이어서 할 수 있다.
    /// 스테이지/상점 진입 시 자동으로, 그리고 일시정지 메뉴의 "로비로 나가기"에서 호출된다.
    /// </summary>
    public void Save()
    {
        var previous = ReadFromDisk();

        var data = new SaveData
        {
            hasActiveRun = true,
            currentStageIndex = SceneLoader.CurrentStageIndex,
            resumeScene = ResolveResumeScene(previous),
            highestStageReached = Mathf.Max(previous != null ? previous.highestStageReached : 0, SceneLoader.CurrentStageIndex),
            clearCount = previous != null ? previous.clearCount : 0,
            runStats = CaptureRunStats(),
            savedAtTicks = DateTime.UtcNow.Ticks,
        };

        CaptureInventory(data.inventory);
        CaptureWeaponParts(data.activeWeaponParts);

        WriteToDisk(data);
    }

    /// <summary>
    /// 런을 종료 처리한다(최종 클리어 또는 사망). 런 데이터를 모두 비우고 메타 기록만 남긴다.
    /// 이후 타이틀 PLAY는 "새 게임"이 되어 1스테이지부터 시작한다.
    /// </summary>
    /// <param name="cleared">true면 최종 클리어로 집계해 <see cref="SaveData.clearCount"/>를 올린다.</param>
    public void EndRun(bool cleared)
    {
        var previous = ReadFromDisk();

        var data = new SaveData
        {
            hasActiveRun = false,
            currentStageIndex = 0,
            resumeScene = string.Empty,
            highestStageReached = Mathf.Max(previous != null ? previous.highestStageReached : 0, SceneLoader.CurrentStageIndex),
            clearCount = (previous != null ? previous.clearCount : 0) + (cleared ? 1 : 0),
            savedAtTicks = DateTime.UtcNow.Ticks,
        };

        WriteToDisk(data);

        // 메모리 상의 런 상태도 같이 정리한다(다음 런은 빈 손, 1스테이지부터).
        SceneLoader.SetCurrentStageIndex(0);
        _pendingActiveParts = null;
        ResumeScene = null;
        InventoryManager.Instance?.Clear();

        Debug.Log($"[SaveManager] 런 종료 ({(cleared ? "최종 클리어" : "실패")}) → 저장된 런 데이터 초기화");
    }

    /// <summary>
    /// 새 런을 시작할 수 있도록 메모리 상태를 초기화한다(파일의 메타 기록은 건드리지 않는다).
    /// 타이틀에서 이어할 런이 없을 때 호출한다.
    /// </summary>
    public void StartNewRun()
    {
        SceneLoader.SetCurrentStageIndex(0);
        _pendingActiveParts = null;
        ResumeScene = null;
        InventoryManager.Instance?.Clear();
        RunResult.BeginRun();
    }

    /// <summary>현재(또는 직전) 씬을 기준으로 이어하기 지점을 정한다. 스테이지/상점만 이어하기 지점이 된다.</summary>
    private string ResolveResumeScene(SaveData previous)
    {
        string active = SceneManager.GetActiveScene().name;
        if (SceneLoader.IsStageScene(active) || active == SceneLoader.SceneNames.Shop) return active;

        // 타이틀/결과 등에서 저장이 호출된 경우엔 직전 이어하기 지점을 유지한다.
        if (previous != null && !string.IsNullOrEmpty(previous.resumeScene)) return previous.resumeScene;
        return string.Empty;
    }

    private RunStatsSave CaptureRunStats() => new RunStatsSave
    {
        stagesCleared = RunResult.StagesCleared,
        totalKills = RunResult.TotalKills,
        totalShots = RunResult.TotalShots,
        bestCombo = RunResult.BestCombo,
        totalReward = RunResult.TotalReward,
        perfectStages = RunResult.PerfectStages,
    };

    private void WriteToDisk(SaveData data)
    {
        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(FilePath, json);
            Debug.Log($"[SaveManager] 저장 완료 → {FilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] 저장 실패: {e.Message}");
        }
    }

    private void CaptureInventory(List<ItemSave> into)
    {
        into.Clear();
        var inv = InventoryManager.Instance != null ? InventoryManager.Instance.Inventory : null;
        if (inv == null) return;

        foreach (ItemCategory category in Enum.GetValues(typeof(ItemCategory)))
        {
            var entries = inv.GetEntries(category);
            foreach (var entry in entries)
            {
                if (entry?.Definition == null || entry.Quantity <= 0) continue;
                into.Add(new ItemSave
                {
                    id = entry.Definition.id,
                    category = (int)entry.Definition.category,
                    quantity = entry.Quantity,
                });
            }
        }
    }

    private void CaptureWeaponParts(List<string> into)
    {
        into.Clear();
        var shooter = FindObjectOfType<PlayerShooter>();
        if (shooter == null) return;
        var parts = shooter.EquippedParts;
        if (parts == null) return;
        foreach (var part in parts)
        {
            if (part != null && shooter.IsPartActive(part))
                into.Add(part.DisplayName);
        }
    }

    // ───────────────────────── 불러오기 ─────────────────────────

    private SaveData ReadFromDisk()
    {
        if (!File.Exists(FilePath)) return null;
        try
        {
            string json = File.ReadAllText(FilePath);
            return JsonUtility.FromJson<SaveData>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] 불러오기 실패: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 진행 중인 런을 불러와 진행도/인벤토리/파츠/누계 성적을 복원한다.
    /// 반환값: 이어서 로드할 스테이지 인덱스(이어할 런이 없으면 -1).
    /// 실제로 어떤 씬으로 돌아갈지는 <see cref="ResumeScene"/>을 본다(상점에서 나갔을 수도 있으므로).
    /// </summary>
    public int Load()
    {
        var data = ReadFromDisk();
        if (data == null || !data.hasActiveRun)
        {
            Debug.Log("[SaveManager] 이어할 런이 없습니다(새 게임으로 시작).");
            return -1;
        }

        RestoreInventory(data.inventory);
        _pendingActiveParts = data.activeWeaponParts; // 다음 씬 로드 시 적용.
        SceneLoader.SetCurrentStageIndex(data.currentStageIndex);
        ResumeScene = data.resumeScene;

        var stats = data.runStats ?? new RunStatsSave();
        RunResult.Restore(stats.stagesCleared, stats.totalKills, stats.totalShots,
                          stats.bestCombo, stats.totalReward, stats.perfectStages);

        Debug.Log($"[SaveManager] 이어하기 불러오기 완료 (스테이지 {data.currentStageIndex}, 복귀 씬 '{data.resumeScene}')");
        return data.currentStageIndex;
    }

    private void RestoreInventory(List<ItemSave> saved)
    {
        if (InventoryManager.Instance == null || saved == null) return;

        InventoryManager.Instance.Clear();
        EnsureCatalog();

        foreach (var item in saved)
        {
            if (item == null || string.IsNullOrEmpty(item.id) || item.quantity <= 0) continue;

            ItemDefinition def = ResolveDefinition(item);
            if (def == null)
            {
                Debug.LogWarning($"[SaveManager] 아이템 정의를 찾지 못해 건너뜀: id='{item.id}'");
                continue;
            }
            InventoryManager.Instance.Add(def, item.quantity);
        }
    }

    private ItemDefinition ResolveDefinition(ItemSave item)
    {
        if (_catalog != null && _catalog.TryGetValue(item.id, out var def) && def != null)
            return def;

        // 카탈로그에 없으면 수량을 한 슬롯에 담을 수 있는 런타임 정의로 대체(수량 보존 우선).
        return ItemDefinition.CreateRuntime(item.id, item.id, (ItemCategory)item.category, Mathf.Max(1, item.quantity));
    }

    private void EnsureCatalog()
    {
        if (_catalog != null) return;
        _catalog = new Dictionary<string, ItemDefinition>();
        foreach (var def in Resources.LoadAll<ItemDefinition>(string.Empty))
        {
            if (def != null && !string.IsNullOrEmpty(def.id))
                _catalog[def.id] = def;
        }
    }

    private void ApplyWeaponParts(PlayerShooter shooter, List<string> activeNames)
    {
        var parts = shooter.EquippedParts;
        if (parts == null) return;
        var wanted = new HashSet<string>(activeNames);
        foreach (var part in parts)
        {
            if (part == null) continue;
            bool shouldBeActive = wanted.Contains(part.DisplayName);
            if (shooter.IsPartActive(part) != shouldBeActive)
                shooter.TogglePart(part);
        }
    }

    // ───────────────────────── 삭제 ─────────────────────────

    /// <summary>
    /// 세이브를 완전히 초기화한다: 파일 삭제 + 진행도(스테이지 인덱스) 0으로 + 인벤토리 비우기.
    /// 메타 기록(최고 스테이지/클리어 횟수)까지 사라진다.
    /// (다음에 타이틀에서 PLAY하면 첫 스테이지부터 시작한다.)
    /// </summary>
    public void DeleteSave()
    {
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
            Debug.Log("[SaveManager] 세이브 삭제 완료");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] 세이브 삭제 실패: {e.Message}");
        }

        // 현재 진행 중인 런도 초기화한다.
        StartNewRun();
    }
}
