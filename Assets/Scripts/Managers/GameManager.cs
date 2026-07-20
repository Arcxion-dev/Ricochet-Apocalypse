using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 스테이지 진행 상태와 클리어/실패 판정을 담당하는 싱글턴 매니저.
/// - 적 추적은 "등록 방식": Enemy가 살아날 때 RegisterEnemy, 사라질 때 UnregisterEnemy를 호출한다.
///   살아있는 적이 0이 되면 스테이지 클리어로 판정한다.
/// - 콤보/퍼펙트 스코어링을 위해 발사 수(RegisterShot)와 처치 수(ReportEnemyKilled)를 추적한다.
///   콤보 = "한 발의 탄환으로 처치한 최대 몬스터 수", 퍼펙트 = "단 1발로 스테이지 클리어".
/// - 실패 판정(민간인 피격/플레이어 사망)은 OnCivilianHit / OnPlayerDeath로 들어온다.
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

    // 살아있는 적 집합. Enemy가 등록/해제한다.
    private readonly HashSet<Enemy> _aliveEnemies = new HashSet<Enemy>();
    private bool _anyEnemyRegistered;

    // 스코어링 상태.
    private int _shotsFired;
    private int _currentBulletKills; // 현재(마지막) 탄환이 처치한 수
    private int _bestCombo;          // 한 발로 처치한 최대 수
    private int _totalKills;

    private bool _stageEnded;        // 클리어/실패 중복 트리거 방지

    public int ShotsFired => _shotsFired;
    public int TotalKills => _totalKills;
    public int BestCombo => _bestCombo;
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

        bool isPerfect = _shotsFired == 1;
        int reward = _baseClearReward
                     + _rewardPerKill * _totalKills
                     + _rewardPerCombo * _bestCombo
                     + (isPerfect ? _perfectBonus : 0);

        var result = new StageResult(true, isPerfect, _bestCombo, _totalKills, _shotsFired, reward);
        Debug.Log($"[GameManager] 스테이지 클리어! {result}");

        // 클리어 후 상점 또는 다음 스테이지로.
        if (_goToShopOnClear) SceneLoader.LoadShop();
        else SceneLoader.LoadNextStage();
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

    /// <summary>민간인 피격 시 즉시 실패 (BulletController에서 연결 — 팀원 협의).</summary>
    public void OnCivilianHit()
    {
        StageFail("민간인 피격");
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

    [ContextMenu("Debug/민간인 피격 (실패)")]
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
