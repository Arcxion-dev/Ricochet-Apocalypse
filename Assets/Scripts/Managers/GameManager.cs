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
/// - 클리어 보상(골드)은 <see cref="CalculateReward"/> 참고: 기본 + 처치 + 콤보(체증) + 퍼펙트를 더한 뒤,
///   그 스테이지에서 민간인을 맞힌 횟수만큼 비율 감산한다.
/// - 실패 판정(플레이어 사망)은 OnPlayerDeath로 들어온다. 민간인 피격(OnCivilianHit)은 실패가 아니라
///   보상 감산 페널티다.
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
    [Tooltip("스테이지를 깨기만 해도 주는 기본 골드")]
    [SerializeField] private int _baseClearReward = 100;

    [Tooltip("적 1기 처치당 추가 골드")]
    [SerializeField] private int _rewardPerKill = 10;

    [Tooltip("콤보 보너스의 기본 단위. 연쇄가 길어질수록 한 킬당 이 값의 배수로 커진다.\n" +
             "(25일 때 → 2콤보 +25 / 3콤보 +75 / 4콤보 +150 / 5콤보 +250)")]
    [SerializeField] private int _rewardPerCombo = 25;

    [Tooltip("단 1발로 스테이지를 클리어했을 때 주는 보너스")]
    [SerializeField] private int _perfectBonus = 200;

    [Tooltip("민간인을 1명 맞힐 때마다 그 스테이지 보상이 줄어드는 비율 (0.2 = 20%).\n" +
             "여러 번 맞히면 누적되며 0%까지 떨어진다.")]
    [Range(0f, 1f)]
    [SerializeField] private float _civilianHitPenalty = 0.2f;

    [Header("적 추격 지연")]
    [Tooltip("스테이지 시작 후 적은 제자리에 멈춰 있다가, 플레이어가 첫 발을 쏘면 이 시간(초) 뒤에 추격을 시작한다.")]
    [SerializeField] private float _enemyChaseDelay = 1f;

    /// <summary>true가 되면 적(EnemyAIModule)이 추격(SetDestination)을 시작한다. 스테이지 시작 시 false.</summary>
    public bool EnemiesCanChase { get; private set; }
    private Coroutine _chaseArmRoutine;

    // 살아있는 적 집합. Enemy가 등록/해제한다.
    private readonly HashSet<Enemy> _aliveEnemies = new HashSet<Enemy>();
    private bool _anyEnemyRegistered;

    // 스코어링 상태.
    private int _shotsFired;
    private int _currentBulletKills; // 현재(마지막) 탄환이 처치한 수
    private int _bestCombo;          // 한 발로 처치한 최대 수
    private int _totalKills;
    private int _civilianHits;       // 이번 스테이지에서 민간인을 맞힌 횟수(보상 감산용)

    private bool _stageEnded;        // 클리어/실패 중복 트리거 방지

    // 클리어 보상으로 지급할 재화 정의. 최초 사용 시 한 번만 로드해 캐싱한다.
    private static ItemDefinition _goldDefinition;
    private static ItemDefinition GoldDefinition =>
        _goldDefinition != null ? _goldDefinition : (_goldDefinition = Resources.Load<ItemDefinition>("Currency/Gold"));

    public int ShotsFired => _shotsFired;
    public int TotalKills => _totalKills;
    public int BestCombo => _bestCombo;
    public int AliveEnemyCount => _aliveEnemies.Count;

    /// <summary>이번 스테이지에서 민간인을 맞힌 횟수.</summary>
    public int CivilianHits => _civilianHits;

    /// <summary>민간인 피격으로 이번 스테이지 보상에 적용될 배수(1 = 감산 없음, 0 = 전액 삭감).</summary>
    public float CivilianPenaltyMultiplier => Mathf.Clamp01(1f - _civilianHitPenalty * _civilianHits);

    /// <summary>지금 클리어하면 받을 골드(HUD 미리보기용). 실제 지급도 같은 계산을 쓴다.</summary>
    public int PendingReward => CalculateReward(_shotsFired == 1 && _shotsFired > 0);

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

        // 에디터에서 스테이지 씬을 직접 Play한 경우를 대비해 진행 인덱스를 씬에 맞춘다.
        if (SceneLoader.IsStageScene(scene.name)) SceneLoader.SyncStageIndexToScene(scene.name);

        // 스테이지/상점 진입 시점에 진행도·인벤토리를 자동 저장한다(타이틀 "이어하기"용 체크포인트).
        // 상점도 저장 지점으로 두어야, 상점에서 로비로 나갔을 때 깬 스테이지를 다시 하지 않는다.
        bool isCheckpoint = SceneLoader.IsStageScene(scene.name) || scene.name == SceneLoader.SceneNames.Shop;
        if (isCheckpoint && SaveManager.Instance != null)
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
        _civilianHits = 0;
        _stageEnded = false;

        // 새 스테이지는 "발사 전 정지" 상태로 시작한다(첫 발 전까지 적은 추격하지 않음).
        EnemiesCanChase = false;
        if (_chaseArmRoutine != null)
        {
            StopCoroutine(_chaseArmRoutine);
            _chaseArmRoutine = null;
        }
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

    // ───────────────────────── 보상 계산 ─────────────────────────

    /// <summary>
    /// 이번 스테이지의 클리어 보상(골드)을 계산한다.
    ///
    /// <code>
    /// 총합 = 기본 + (처치수 × 처치당) + 콤보 보너스 + (퍼펙트면 퍼펙트 보너스)
    /// 최종 = 총합 × (1 − 민간인 감산비율 × 민간인 피격 수)      // 0 미만으로는 내려가지 않음
    /// </code>
    /// </summary>
    private int CalculateReward(bool isPerfect)
    {
        int gross = _baseClearReward
                    + _rewardPerKill * _totalKills
                    + CalculateComboBonus(_bestCombo)
                    + (isPerfect ? _perfectBonus : 0);

        int final = Mathf.RoundToInt(gross * CivilianPenaltyMultiplier);
        return Mathf.Max(0, final);
    }

    /// <summary>
    /// 최고 콤보에 대한 보너스. 한 발로 2기 이상 잡았을 때부터 붙고, 연쇄가 길어질수록
    /// "추가 1킬의 가치"가 점점 커진다(체증). 추가 n번째 킬 = <see cref="_rewardPerCombo"/> × n.
    ///
    /// 단위가 25일 때: 1콤보 0 / 2콤보 25 / 3콤보 75 / 4콤보 150 / 5콤보 250 / 6콤보 375.
    /// (1콤보는 그냥 한 마리 잡은 것이므로 콤보로 치지 않는다 — 처치 보상으로만 계산된다.)
    /// </summary>
    private int CalculateComboBonus(int combo)
    {
        int extraKills = combo - 1; // 첫 킬을 뺀 "연쇄로 추가된" 킬 수.
        if (extraKills <= 0) return 0;

        return _rewardPerCombo * extraKills * (extraKills + 1) / 2;
    }

    /// <summary>보상이 어떻게 나왔는지 항목별로 풀어 쓴다(밸런스 조정용 로그).</summary>
    private string DescribeRewardBreakdown(bool isPerfect, int finalReward)
    {
        int comboBonus = CalculateComboBonus(_bestCombo);
        int killBonus = _rewardPerKill * _totalKills;
        int perfect = isPerfect ? _perfectBonus : 0;
        int gross = _baseClearReward + killBonus + comboBonus + perfect;

        string line = $"  보상: 기본 {_baseClearReward} + 처치 {killBonus}({_totalKills}기) " +
                      $"+ 콤보 {comboBonus}({_bestCombo}콤보)";
        if (perfect > 0) line += $" + 퍼펙트 {perfect}";
        line += $" = {gross}";

        if (_civilianHits > 0)
        {
            int cut = Mathf.RoundToInt((1f - CivilianPenaltyMultiplier) * 100f);
            line += $"  →  민간인 {_civilianHits}명 피격으로 {cut}% 감소  →  최종 {finalReward} 골드";
        }
        else
        {
            line += $"  →  최종 {finalReward} 골드";
        }
        return line;
    }

    // ───────────────────────── 클리어 / 실패 판정 ─────────────────────────

    private void StageClear()
    {
        if (_stageEnded) return;
        _stageEnded = true;

        bool isPerfect = _shotsFired == 1;
        int reward = CalculateReward(isPerfect);

        var result = new StageResult(true, isPerfect, _bestCombo, _totalKills, _shotsFired, reward, _civilianHits);
        Debug.Log($"[GameManager] 스테이지 클리어! {result}\n{DescribeRewardBreakdown(isPerfect, reward)}");

        // 런 누계 성적에 이번 스테이지를 더한다(결과 화면이 읽는다).
        RunResult.ReportStageCleared(result);

        // 보상을 실제 재화로 지급(에셋이 없으면 경고만 남기고 넘어간다).
        if (GoldDefinition != null) InventoryManager.Instance?.Add(GoldDefinition, reward);
        else Debug.LogWarning("[GameManager] Resources/Currency/Gold 에셋을 찾을 수 없어 보상을 지급하지 못했습니다.");

        // 마지막 스테이지를 깼다면 게임 최종 클리어 → 상점을 거치지 않고 결과 화면으로.
        if (SceneLoader.IsOnLastStage)
        {
            SceneLoader.FinishRun(true);
            return;
        }

        // 클리어 후 상점 또는 다음 스테이지로.
        if (_goToShopOnClear) SceneLoader.LoadShop();
        else SceneLoader.LoadNextStage();
    }

    /// <summary>
    /// 스테이지 실패. 로그라이크 규칙이라 실패 = 런 종료이며, 결과 화면을 거쳐 다음 판은 1스테이지부터
    /// 빈 손으로 다시 시작한다(<see cref="SceneLoader.FinishRun"/>가 세이브의 런 데이터를 지운다).
    /// </summary>
    private void StageFail(string reason)
    {
        if (_stageEnded) return;
        _stageEnded = true;

        var result = new StageResult(false, false, _bestCombo, _totalKills, _shotsFired, 0, _civilianHits);
        Debug.LogWarning($"[GameManager] 스테이지 실패 ({reason}) {result}");

        RunResult.ReportStageFailed(result);
        SceneLoader.FinishRun(false, reason);
    }

    /// <summary>
    /// 민간인 피격 (BulletController에서 연결). 스테이지 실패가 아니라 <b>보상 페널티</b>다.
    /// 한 번 맞힐 때마다 이번 스테이지 클리어 보상이 <see cref="_civilianHitPenalty"/>만큼 줄어들고,
    /// 여러 번 맞히면 누적돼 최대 0골드까지 떨어진다.
    /// </summary>
    public void OnCivilianHit()
    {
        // 이미 클리어/실패로 끝난 스테이지에서는 보상이 확정됐으므로 더 세지 않는다.
        if (_stageEnded) return;

        _civilianHits++;
        Debug.LogWarning($"[GameManager] 민간인 피격 {_civilianHits}회 → 이번 스테이지 보상 " +
                         $"{Mathf.RoundToInt((1f - CivilianPenaltyMultiplier) * 100f)}% 감소 " +
                         $"(예상 보상 {PendingReward} 골드)");
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

    [ContextMenu("Debug/민간인 피격 (보상 감소)")]
    private void DebugCivilianHit() => OnCivilianHit();

    [ContextMenu("Debug/플레이어 사망 (실패)")]
    private void DebugPlayerDeath() => OnPlayerDeath();
}

/// <summary>스테이지 종료 결과 요약 (클리어/실패, 퍼펙트, 콤보, 민간인 피격, 최종 보상).</summary>
public readonly struct StageResult
{
    public readonly bool IsClear;
    public readonly bool IsPerfect;
    public readonly int Combo;
    public readonly int TotalKills;
    public readonly int ShotsFired;
    /// <summary>민간인 피격 감산까지 반영된 최종 지급 골드.</summary>
    public readonly int Reward;
    public readonly int CivilianHits;

    public StageResult(bool isClear, bool isPerfect, int combo, int totalKills, int shotsFired, int reward, int civilianHits = 0)
    {
        IsClear = isClear;
        IsPerfect = isPerfect;
        Combo = combo;
        TotalKills = totalKills;
        ShotsFired = shotsFired;
        Reward = reward;
        CivilianHits = civilianHits;
    }

    public override string ToString()
        => $"[클리어={IsClear}, 퍼펙트={IsPerfect}, 콤보={Combo}, 처치={TotalKills}, 발사={ShotsFired}, " +
           $"민간인피격={CivilianHits}, 보상={Reward}]";
}
