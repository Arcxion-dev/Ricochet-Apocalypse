using UnityEngine;

/// <summary>
/// 무기 파츠가 조준/사격 스탯에 기여하는 값들의 집계 구조체.
/// <see cref="PlayerShooter"/>가 장착된 모든 파츠를 순회하며 이 구조체에 값을 쌓은 뒤
/// 조준선(가이드라인)·호흡 흔들림·차징 슬로우에 반영한다.
/// </summary>
public struct WeaponStats
{
    /// <summary>호흡 흔들림 진폭 배율. 1=기본, 손잡이 파츠가 1 미만으로 낮춰 흔들림을 줄인다.</summary>
    public float swayMultiplier;

    /// <summary>차징 슬로우 타임스케일 배율. 1=기본, 네뷸라이저가 1 미만으로 낮춰 더 느리게 만든다.</summary>
    public float slowScaleMultiplier;

    /// <summary>조준 가이드라인(레이저) 표시 여부. 레이저/조준경 파츠가 있으면 true.</summary>
    public bool laserEnabled;

    /// <summary>직선 가이드 사정거리(월드 유닛). 레이저 파츠가 더한다(합연산).</summary>
    public float laserRange;

    /// <summary>벽 반사 궤적 예측을 켤지 여부. 반사 예측기 파츠가 있어야 true(직선 레이저는 기본, 반사는 이 파츠로만 활성).</summary>
    public bool reflectionEnabled;

    /// <summary>예측할 벽 반사 횟수. 반사 예측기 파츠가 더한다(가이드선 증가).</summary>
    public int predictBounces;

    /// <summary>반사 궤적 예측 사정거리 추가분(월드 유닛). 레이저 파츠가 더하며 기본 사정거리와 합연산된다.</summary>
    public float predictRange;

    /// <summary>헤드샷 배수에 곱해질 추가 비율. 0=기본. 조준경 파츠가 0.2(=+20%)를 더한다(배수 ×(1+bonus)).</summary>
    public float headshotMultiplierBonus;

    /// <summary>텅스텐 탄환: 자연 헤드샷 명중 시 다음에 맞을 상대를 확정 헤드샷으로 만드는 연쇄 활성 여부.</summary>
    public bool guaranteedHeadshotChain;

    /// <summary>탄환 기본 대미지에 곱해질 배율. 1=기본. 강선 파츠가 강화도(레벨)당 +25%씩 올린다.</summary>
    public float damageMultiplier;

    /// <summary>파츠가 하나도 없을 때의 기본값(배율 1). 직선 레이저는 기본 상시 표시라 게이팅과 무관.</summary>
    public static WeaponStats Default => new WeaponStats
    {
        swayMultiplier = 1f,
        slowScaleMultiplier = 1f,
        laserEnabled = false,
        laserRange = 0f,
        reflectionEnabled = false,
        predictBounces = 0,
        predictRange = 0f,
        headshotMultiplierBonus = 0f,
        guaranteedHeadshotChain = false,
        damageMultiplier = 1f,
    };
}

/// <summary>
/// 무기 파츠 한 종류를 정의하는 데이터 에셋의 추상 베이스.
/// <see cref="BulletEffectSO"/> 패턴과 동일하게, 종류별 concrete 서브클래스가
/// <see cref="Contribute"/>에서 자신의 효과를 <see cref="WeaponStats"/>에 더한다.
///
/// 지금은 <see cref="PlayerShooter"/>의 장착 리스트로 직접 끼우는 "독립 파츠" 방식이며,
/// 이후 인벤토리(GunPart 카테고리)의 ItemDefinition이 이 SO를 참조하도록 연동할 수 있다.
/// </summary>
public abstract class WeaponPartSO : ScriptableObject
{
    [Tooltip("파츠 표시 이름(디버그/HUD용). 비워도 동작에는 영향 없음.")]
    public string partName;

    /// <summary>UI 표시용 이름. partName이 비어 있으면 에셋 이름을 사용한다. (강선 등은 레벨을 붙여 override)</summary>
    public virtual string DisplayName => string.IsNullOrEmpty(partName) ? name : partName;

    /// <summary>이 파츠의 효과를 집계 스탯에 더한다(합연산/배율은 각 파츠가 결정).</summary>
    public abstract void Contribute(ref WeaponStats stats);
}
