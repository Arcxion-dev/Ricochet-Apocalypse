using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임 진행 상태를 JSON 파일로 저장/불러오는 싱글턴 매니저.
/// 저장 위치: <c>Application.persistentDataPath/save.json</c> (플랫폼별 유저 데이터 폴더).
///
/// 저장 대상:
/// - 진행도: 현재 스테이지 인덱스 / 도달한 최고 스테이지.
/// - 인벤토리: <see cref="InventoryManager"/>의 슬롯을 (id, 분류, 수량)으로 직렬화.
/// - 무기 파츠: 현재 씬 <see cref="PlayerShooter"/>가 "켜 둔(ON)" 파츠 이름 목록.
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
        public int currentStageIndex;
        public int highestStageReached;
        public List<ItemSave> inventory = new List<ItemSave>();
        public List<string> activeWeaponParts = new List<string>();
        public long savedAtTicks;
    }

    [Serializable]
    public class ItemSave
    {
        public string id;
        public int category;
        public int quantity;
    }

    private static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

    /// <summary>세이브 파일이 존재하는지(타이틀의 "이어하기" 활성화 판단용).</summary>
    public bool HasSave => File.Exists(FilePath);

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

    /// <summary>현재 게임 상태를 파일로 저장한다.</summary>
    public void Save()
    {
        var data = new SaveData
        {
            currentStageIndex = SceneLoader.CurrentStageIndex,
            highestStageReached = Mathf.Max(ReadHighestFromDisk(), SceneLoader.CurrentStageIndex),
            savedAtTicks = DateTime.UtcNow.Ticks,
        };

        CaptureInventory(data.inventory);
        CaptureWeaponParts(data.activeWeaponParts);

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

    private int ReadHighestFromDisk()
    {
        var existing = ReadFromDisk();
        return existing != null ? existing.highestStageReached : 0;
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
    /// 세이브를 불러와 진행도/인벤토리/파츠를 복원한다.
    /// 반환값: 이어서 로드할 스테이지 인덱스(없으면 -1).
    /// </summary>
    public int Load()
    {
        var data = ReadFromDisk();
        if (data == null)
        {
            Debug.LogWarning("[SaveManager] 불러올 세이브가 없습니다.");
            return -1;
        }

        RestoreInventory(data.inventory);
        _pendingActiveParts = data.activeWeaponParts; // 다음 씬 로드 시 적용.
        SceneLoader.SetCurrentStageIndex(data.currentStageIndex);

        Debug.Log($"[SaveManager] 불러오기 완료 (스테이지 {data.currentStageIndex}, 최고 {data.highestStageReached})");
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
        SceneLoader.SetCurrentStageIndex(0);
        _pendingActiveParts = null;
        if (InventoryManager.Instance != null) InventoryManager.Instance.Clear();
    }
}
