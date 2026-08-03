using System;
using UnityEngine;

/// <summary>
/// "한 판(런)" 단위의 누적 성적과 종료 사유를 들고 있는 정적 홀더.
/// <see cref="GameManager"/>가 스테이지마다 결과를 넣어 주고, 런이 끝나면(최종 클리어/사망)
/// 결과 씬의 <see cref="ResultUI"/>가 이 값을 읽어 화면에 뿌린다.
///
/// - GameManager의 스코어(_totalKills 등)는 씬이 바뀔 때마다 초기화되는 "스테이지 단위" 값이다.
///   반면 여기 값은 런이 시작될 때 한 번만 초기화되고, 스테이지를 넘어가며 계속 누적된다.
/// - 씬을 넘어가도 유지돼야 하므로 정적으로 두되, 게임을 껐다 켜도(로비 저장 후 재접속) 이어지도록
///   <see cref="SaveManager"/>가 이 값을 세이브 파일에 함께 담고 <see cref="Restore"/>로 되돌린다.
/// </summary>
public static class RunResult
{
    /// <summary>런의 종료 상태.</summary>
    public enum Outcome
    {
        /// <summary>아직 진행 중(또는 기록 없음).</summary>
        None,
        /// <summary>마지막 스테이지까지 깨서 최종 클리어.</summary>
        Cleared,
        /// <summary>사망/민간인 피격 등으로 런 종료.</summary>
        Failed,
    }

    public static Outcome LastOutcome { get; private set; } = Outcome.None;

    /// <summary>실패로 끝났을 때의 사유(예: "플레이어 사망"). 클리어면 빈 문자열.</summary>
    public static string FailReason { get; private set; } = string.Empty;

    /// <summary>이번 런에서 클리어한 스테이지 수.</summary>
    public static int StagesCleared { get; private set; }

    public static int TotalKills { get; private set; }
    public static int TotalShots { get; private set; }

    /// <summary>런 전체에서 나온 최고 콤보(한 발로 처치한 최대 수).</summary>
    public static int BestCombo { get; private set; }

    /// <summary>클리어 보상으로 받은 골드 누계.</summary>
    public static int TotalReward { get; private set; }

    /// <summary>퍼펙트(1발 클리어)로 깬 스테이지 수.</summary>
    public static int PerfectStages { get; private set; }

    /// <summary>런이 끝난 시각(결과 화면 표시용). 진행 중이면 default.</summary>
    public static DateTime FinishedAtUtc { get; private set; }

    /// <summary>
    /// 이 프로젝트는 Enter Play Mode 옵션에서 Domain Reload가 꺼져 있어, 정적 값이 플레이 세션 사이에
    /// 그대로 남는다. 플레이를 시작할 때마다 이전 판의 성적이 새 판에 섞이지 않도록 여기서 비운다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnEnterPlayMode() => BeginRun();

    /// <summary>새 런 시작. 누적값을 모두 비운다.</summary>
    public static void BeginRun()
    {
        LastOutcome = Outcome.None;
        FailReason = string.Empty;
        StagesCleared = 0;
        TotalKills = 0;
        TotalShots = 0;
        BestCombo = 0;
        TotalReward = 0;
        PerfectStages = 0;
        FinishedAtUtc = default;
    }

    /// <summary>스테이지 하나를 클리어했을 때 그 성적을 런 누계에 더한다.</summary>
    public static void ReportStageCleared(in StageResult stage)
    {
        StagesCleared++;
        TotalKills += stage.TotalKills;
        TotalShots += stage.ShotsFired;
        TotalReward += stage.Reward;
        if (stage.Combo > BestCombo) BestCombo = stage.Combo;
        if (stage.IsPerfect) PerfectStages++;
    }

    /// <summary>실패로 끝난 스테이지의 발사/처치 수도 누계에는 반영한다(클리어 수는 늘지 않음).</summary>
    public static void ReportStageFailed(in StageResult stage)
    {
        TotalKills += stage.TotalKills;
        TotalShots += stage.ShotsFired;
        if (stage.Combo > BestCombo) BestCombo = stage.Combo;
    }

    /// <summary>런을 "최종 클리어"로 종료 처리한다.</summary>
    public static void MarkCleared()
    {
        LastOutcome = Outcome.Cleared;
        FailReason = string.Empty;
        FinishedAtUtc = DateTime.UtcNow;
    }

    /// <summary>런을 "실패"로 종료 처리한다.</summary>
    public static void MarkFailed(string reason)
    {
        LastOutcome = Outcome.Failed;
        FailReason = reason ?? string.Empty;
        FinishedAtUtc = DateTime.UtcNow;
    }

    /// <summary>세이브에서 읽어온 런 누계를 되돌린다(로비 저장 후 이어하기).</summary>
    public static void Restore(int stagesCleared, int totalKills, int totalShots, int bestCombo, int totalReward, int perfectStages)
    {
        LastOutcome = Outcome.None;
        FailReason = string.Empty;
        StagesCleared = stagesCleared;
        TotalKills = totalKills;
        TotalShots = totalShots;
        BestCombo = bestCombo;
        TotalReward = totalReward;
        PerfectStages = perfectStages;
        FinishedAtUtc = default;
    }
}
