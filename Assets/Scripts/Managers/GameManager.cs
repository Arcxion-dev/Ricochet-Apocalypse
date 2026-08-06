using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 스테이지 진행 상태와 클리어/실패 판정을 담당하는 싱글턴 매니저.
/// - 적 추적은 "등록 방식": Enemy가 살아날 때 RegisterEnemy, 사라질 때 UnregisterEnemy를 호출한다.
///   살아있는 적이 0이 되면 스테이지 클리어로 판정한다.
/// - 콤보/퍼펙트 스코어링을 위해 발사 수(RegisterShot)와 처치 수(ReportEnemyKilled)를 추적한다.
///   콤보 = "한 발의 탄환으로 처치한 최대 몬스터 수", 퍼펙트 = "단 1발로 스테이지 클리어".
/// - 실패 판정(플레이어 사망)은 OnPlayerDeath로 들어온다.
///   민간인 피격(OnCivilianHit)은 실패가 아니라 보상 재화 차감 + 콤보 초기화 페널티로 처리한다.
///
/// 담당 범위 밖(적 AI/실제 데미지, 총알 실제 발사, 상점/재화, UI)은 public API/로그 스텁으로만 열어둔다.
/// Enemy.cs / BulletController.cs 연결은 각 담당 팀원과 협의 후 붙인다. 그전까지는 아래 ContextMenu
/// 디버그 메서드로 킬/실패를 시뮬레이션해 흐름을 검증할 수 있다.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("스테이지 클리어 시 다음 흐름")]
    [Tooltip("클리어 후 상점 씬으로 이동할지 여부 (false면 곧바로 다음 스테이지)")]
    [SerializeField] private bool _goToShopOnClear = true;

    [Header("보상 계산 (임시 밸런스)")]
    [SerializeField] private int _baseClearReward = 100;
    [SerializeField] private int _rewardPerKill = 10;
    [SerializeField] private int _rewardPerCombo = 25;
    [SerializeField] private int _perfectBonus = 200;

    [Header("민간인 피격 페널티")]
    [Tooltip("민간인 피격 시 즉시 실패하지 않고 이만큼 보상 재화(골드)를 차감하고 콤보를 초기화한다.")]
    [SerializeField] private int _civilianHitPenalty = 20;

    [Header("적 추격 지연")]
    [Tooltip("스테이지 시작 후 적은 제자리에 멈춰 있다가, 플레이어가 첫 발을 쏘면 이 시간(초) 뒤에 추격을 시작한다.")]
    [SerializeField] private float _enemyChaseDelay = 1f;

    /// <summary>true가 되면 적(EnemyAIModule)이 추격(SetDestination)을 시작한다. 스테이지 시작 시 false.</summary>
    public bool EnemiesCanChase { get; private set; }
    private Coroutine _chaseArmRoutine;

    /// <summary>
    /// 준비(Ready) 상태에서 [시작]을 누르면 true가 된다. false면 <see cref="PlayerShooter"/>가
    /// 조준선을 숨기고 격발/아이템 입력을 막는다(준비 중 조준 방지). 스테이지 진입 시 false.
    /// </summary>
    public bool StageStarted { get; private set; }

    // 살아있는 적 집합. Enemy가 등록/해제한다.
    private readonly HashSet<Enemy> _aliveEnemies = new HashSet<Enemy>();
    private bool _anyEnemyRegistered;

    // 스코어링 상태.
    private int _shotsFired;
    private int _currentBulletKills; // 현재(마지막) 탄환이 처치한 수
    private int _bestCombo;          // 한 발로 처치한 최대 수
    private int _totalKills;

    private bool _stageEnded;        // 클리어/실패 중복 트리거 방지

    // 클리어 보상으로 지급할 재화 정의. 최초 사용 시 한 번만 로드해 캐싱한다.
    private static ItemDefinition _goldDefinition;
    private static ItemDefinition GoldDefinition =>
        _goldDefinition != null ? _goldDefinition : (_goldDefinition = Resources.Load<ItemDefinition>("Currency/Gold"));

    public int ShotsFired => _shotsFired;
    public int TotalKills => _totalKills;
    public int BestCombo => _bestCombo;
    /// <summary>지금 날아가는(마지막으로 쏜) 탄환이 여기까지 처치한 수. HUD의 실시간 콤보 표시용.</summary>
    public int CurrentCombo => _currentBulletKills;
    public int AliveEnemyCount => _aliveEnemies.Count;

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
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 새 씬(스테이지)이 열릴 때마다 스테이지 상태를 초기화한다.
        ResetStageState();

        // 스테이지 진입 시점에 진행도/인벤토리를 자동 저장한다(타이틀 "이어하기"용).
        if (SceneLoader.IsStageScene(scene.name) && SaveManager.Instance != null)
        {
            SaveManager.Instance.Save();
        }
    }

    /// <summary>스테이지 진행/스코어 상태를 초기화한다.</summary>
    public void ResetStageState()
    {
        _aliveEnemies.Clear();
        _anyEnemyRegistered = false;
        _shotsFired = 0;
        _currentBulletKills = 0;
        _bestCombo = 0;
        _totalKills = 0;
        _stageEnded = false;

        // 새 스테이지는 "발사 전 정지" 상태로 시작한다(첫 발 전까지 적은 추격하지 않음).
        EnemiesCanChase = false;
        if (_chaseArmRoutine != null)
        {
            StopCoroutine(_chaseArmRoutine);
            _chaseArmRoutine = null;
        }

        // 모든 스테이지는 준비(Ready) 상태로 시작한다([시작] 전까지 조준/격발 불가).
        StageStarted = false;
    }

    // ───────────────────────── 준비 / 시작 상태 ─────────────────────────

    /// <summary>스테이지를 준비(Ready) 상태로 되돌린다(조준/격발 차단). StageReadyUI 진입 시 호출.</summary>
    public void SetStageReady() => StageStarted = false;

    /// <summary>준비 상태에서 [시작]을 눌러 플레이를 개시한다(조준/격발 허용). StageReadyUI에서 호출.</summary>
    public void StartStage()
    {
        StageStarted = true;
        Debug.Log("[GameManager] 스테이지 시작 (조준/격발 허용)");
    }

    // ───────────────────────── 적 추적 (등록 방식) ─────────────────────────

    /// <summary>Enemy가 활성화될 때 호출 (Enemy.OnEnable에서 연결 — 팀원 협의).</summary>
    public void RegisterEnemy(Enemy enemy)
    {
        if (enemy == null) return;
        if (_aliveEnemies.Add(enemy))
        {
            _anyEnemyRegistered = true;
        }
    }

    /// <summary>Enemy가 비활성화/파괴될 때 호출. 마지막 적이 사라지면 클리어 판정.</summary>
    public void UnregisterEnemy(Enemy enemy)
    {
        if (enemy == null) return;
        _aliveEnemies.Remove(enemy);
        CheckClearCondition();
    }

    private void CheckClearCondition()
    {
        if (_stageEnded) return;
        if (_anyEnemyRegistered && _aliveEnemies.Count == 0)
        {
            StageClear();
        }
    }

    // ───────────────────────── 콤보/퍼펙트 스코어링 ─────────────────────────

    /// <summary>플레이어가 한 발 발사할 때 호출. 새 탄환이므로 "현재 탄환 킬 수"를 리셋한다.</summary>
    public void RegisterShot()
    {
        _shotsFired++;
        _currentBulletKills = 0;
        Debug.Log($"[GameManager] 발사 등록 (누적 발사={_shotsFired})");

        // 첫 발을 쏘는 순간 추격 무장 타이머를 건다. 지연 후 적이 추격을 시작한다.
        if (!EnemiesCanChase && _chaseArmRoutine == null)
        {
            _chaseArmRoutine = StartCoroutine(ArmChaseAfterDelay());
        }
    }

    /// <summary>플레이어 첫 발 이후 <see cref="_enemyChaseDelay"/>초가 지나면 적 추격을 허용한다.</summary>
    private IEnumerator ArmChaseAfterDelay()
    {
        // 발사 직후 슬로우모션 연출 구간에도 "N초"가 체감상 일정하도록 실시간 대기를 쓴다.
        yield return new WaitForSecondsRealtime(_enemyChaseDelay);
        EnemiesCanChase = true;
        _chaseArmRoutine = null;
        Debug.Log($"[GameManager] 적 추격 시작 (지연 {_enemyChaseDelay}s 경과)");
    }

    /// <summary>
    /// 적 1기 처치 시 호출 (Enemy 사망 이벤트에서 연결 — 팀원 협의).
    /// 현재 탄환 킬 수를 올리고, 그 값으로 최고 콤보를 갱신한다.
    /// </summary>
    public void ReportEnemyKilled()
    {
        _totalKills++;
        _currentBulletKills++;
        if (_currentBulletKills > _bestCombo)
        {
            _bestCombo = _currentBulletKills;
        }
        Debug.Log($"[GameManager] 처치 (이번 탄환 {_currentBulletKills}킬 / 최고 콤보 {_bestCombo} / 누적 {_totalKills})");
    }

    // ───────────────────────── 클리어 / 실패 판정 ─────────────────────────

    private void StageClear()
    {
        if (_stageEnded) return;
        _stageEnded = true;

        // 클리어 순간부터는 조준/격발을 막는다(클리어 창 뒤에서 계속 쏘는 것 방지).
        StageStarted = false;

        bool isPerfect = _shotsFired == 1;
        int reward = _baseClearReward
                     + _rewardPerKill * _totalKills
                     + _rewardPerCombo * _bestCombo
                     + (isPerfect ? _perfectBonus : 0);

        var result = new StageResult(true, isPerfect, _bestCombo, _totalKills, _shotsFired, reward);
        Debug.Log($"[GameManager] 스테이지 클리어! {result}");

        // 골드 보상을 실제 재화로 지급(에셋이 없으면 경고만 남기고 넘어간다).
        if (GoldDefinition != null) InventoryManager.Instance?.Add(GoldDefinition, reward);
        else Debug.LogWarning("[GameManager] Resources/Currency/Gold 에셋을 찾을 수 없어 보상을 지급하지 못했습니다.");

        // 스테이지별 드랍테이블을 굴려 아이템을 지급하고, 클리어 UI에 표시한다.
        var drops = RollAndAwardDrops();

        // 클리어 UI를 띄우고, [확인] 시 상점(또는 결과)으로 진행한다. UI가 없으면 바로 진행.
        if (StageClearUI.Instance != null) StageClearUI.Instance.Show(result, drops, ProceedAfterClear);
        else ProceedAfterClear();
    }

    /// <summary>
    /// 현재 스테이지의 드랍테이블(<c>Resources/DropTables/{씬이름}</c> → 없으면 <c>DropTables/Default</c>)을
    /// 굴려 나온 아이템을 인벤토리에 지급하고 결과 목록을 반환한다. 테이블이 없으면 빈 목록.
    /// </summary>
    private List<DropResult> RollAndAwardDrops()
    {
        var results = new List<DropResult>();

        string sceneName = SceneManager.GetActiveScene().name;
        var table = Resources.Load<DropTableSO>($"DropTables/{sceneName}");
        if (table == null) table = Resources.Load<DropTableSO>("DropTables/Default");
        if (table == null) return results;

        results = table.Roll();
        foreach (var d in results)
        {
            if (d.Item != null) InventoryManager.Instance?.Add(d.Item, d.Quantity);
        }
        return results;
    }

    /// <summary>클리어 UI [확인] 후: 스테이지 인덱스를 다음으로 올리고 상점을 경유한다(마지막이면 결과 씬).</summary>
    private void ProceedAfterClear()
    {
        int next = SceneLoader.CurrentStageIndex + 1;
        if (next >= SceneLoader.StageCount)
        {
            SceneLoader.LoadResult();
            return;
        }

        // 준비할 다음 스테이지로 인덱스를 미리 전진시킨다(상점 '출발'은 이 인덱스를 로드한다).
        SceneLoader.SetCurrentStageIndex(next);

        if (_goToShopOnClear) SceneLoader.LoadShop();
        else SceneLoader.LoadStage(next);
    }

    private void StageFail(string reason)
    {
        if (_stageEnded) return;
        _stageEnded = true;

        var result = new StageResult(false, false, _bestCombo, _totalKills, _shotsFired, 0);
        Debug.LogWarning($"[GameManager] 스테이지 실패 ({reason}) {result}");

        // 실패 시 현재 스테이지 재시도.
        SceneLoader.ReloadStage();
    }

    /// <summary>
    /// 민간인 피격 시 (BulletController에서 연결). 즉시 실패시키지 않고,
    /// 보상 재화(골드)를 <see cref="_civilianHitPenalty"/>만큼 차감하고 콤보 수치를 초기화한다.
    /// 보유 골드가 페널티보다 적으면 0까지만 차감된다(음수 불가).
    /// </summary>
    public void OnCivilianHit()
    {
        // 클리어/실패로 이미 종료된 스테이지에서는 페널티를 적용하지 않는다.
        if (_stageEnded) return;

        // 1) 보상 재화(골드) 차감.
        int removed = 0;
        if (GoldDefinition != null)
        {
            removed = InventoryManager.Instance?.Remove(GoldDefinition, _civilianHitPenalty) ?? 0;
        }
        else
        {
            Debug.LogWarning("[GameManager] Resources/Currency/Gold 에셋을 찾을 수 없어 골드를 차감하지 못했습니다.");
        }

        // 2) 콤보 초기화(현재 탄환 콤보 + 스테이지 최고 콤보).
        _currentBulletKills = 0;
        _bestCombo = 0;

        Debug.LogWarning($"[GameManager] 민간인 피격! 골드 -{removed} 차감, 콤보 초기화");
    }

    /// <summary>플레이어 사망 시 실패 (Player.DecreaseHP 사망 분기에서 연결).</summary>
    public void OnPlayerDeath()
    {
        StageFail("플레이어 사망");
    }

    // ───────────────────────── 디버그 시뮬레이션 (팀원 연동 전 테스트용) ─────────────────────────

    [ContextMenu("Debug/적 1기 처치 시뮬레이션")]
    private void DebugKillOneEnemy()
    {
        // 등록된 적이 없으면 임시로 하나 등록해 흐름만 확인.
        Enemy target = null;
        foreach (var e in _aliveEnemies) { target = e; break; }

        ReportEnemyKilled();
        if (target != null)
        {
            UnregisterEnemy(target);
        }
        else
        {
            Debug.Log("[GameManager] (디버그) 등록된 적이 없어 킬 스코어만 증가시켰습니다.");
        }
    }

    [ContextMenu("Debug/민간인 피격 (골드 차감 + 콤보 초기화)")]
    private void DebugCivilianHit() => OnCivilianHit();

    [ContextMenu("Debug/플레이어 사망 (실패)")]
    private void DebugPlayerDeath() => OnPlayerDeath();
}

/// <summary>스테이지 종료 결과 요약 (클리어/실패, 퍼펙트, 콤보, 보상).</summary>
public readonly struct StageResult
{
    public readonly bool IsClear;
    public readonly bool IsPerfect;
    public readonly int Combo;
    public readonly int TotalKills;
    public readonly int ShotsFired;
    public readonly int Reward;

    public StageResult(bool isClear, bool isPerfect, int combo, int totalKills, int shotsFired, int reward)
    {
        IsClear = isClear;
        IsPerfect = isPerfect;
        Combo = combo;
        TotalKills = totalKills;
        ShotsFired = shotsFired;
        Reward = reward;
    }

    public override string ToString()
        => $"[클리어={IsClear}, 퍼펙트={IsPerfect}, 콤보={Combo}, 처치={TotalKills}, 발사={ShotsFired}, 보상={Reward}]";
}
