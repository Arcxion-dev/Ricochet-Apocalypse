using System;

/// <summary>
/// 총알이 벽/장애물과 부딪혔을 때 상호작용 결과를 표현합니다.
/// 장애물 측(다른 담당자)이 실제 판정 로직을 갖고 있고,
/// 총알은 이 결과를 받아서 반응(튕김/관통/소멸)만 합니다.
/// </summary>
public enum BulletHitResult
{
    Bounce,     // 튕김
    Penetrate,  // 관통 (철갑탄이 뚫을 수 있는 벽 등)
    Destroy     // 총알 소멸 (민간인, 파괴 불가능 대상 등)
}

/// <summary>
/// 총알이 무언가에 부딪혔을 때의 대상 타입.
/// 장애물 담당자가 정의할 실제 타입과 매핑되기 전까지 사용할 임시 분류입니다.
/// </summary>
public enum BulletTargetType
{
    Wall,               // 벽
    ArmoredWall,        // 장갑화된 벽
    Bush,               // 풀숲
    Tree,               // 나무
    Rock,               // 바위
    Civilian,           // 민간인
    Sandstorm,          // 모래바람
    ElectricPanel,      // 전자 패널
    HeatHaze,           // 아지랑이
    Enemy,              // 적 (아직 미구현)
    Unknown
}

/// <summary>
/// 분열탄 등에서 사용할 분열 트리거 조건.
/// </summary>
[Flags]
public enum SplitTrigger
{
    None = 0,
    OnEnemyHit = 1 << 0,   // 적중 시 분열
    OnWallBounce = 1 << 1, // 벽 튕김 시 분열
    OnTimer = 1 << 2,      // 일정 시간 후 분열
}
