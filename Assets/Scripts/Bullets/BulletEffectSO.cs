using UnityEngine;

/// <summary>
/// 모든 총알 속성 효과(철갑탄, 폭발탄, 분열탄 ...)의 베이스 클래스.
/// 각 효과는 ScriptableObject 에셋으로 만들어져 BulletSO의 리스트에 담깁니다.
/// 인스펙터에서 여러 개를 자유롭게 추가/조합할 수 있도록 하기 위해
/// "하나의 거대한 enum 스위치" 대신 "효과 하나당 SO 하나" 구조로 설계했습니다.
///
/// 실제 동작(적 데미지 계산, 장애물 파괴 등)은 아직 대상 시스템이 없으므로
/// 전부 Debug.Log로 스텁 처리되어 있습니다. 담당자가 준비되면
/// 각 On** 메서드 내부만 교체하면 됩니다.
/// </summary>
public abstract class BulletEffectSO : ScriptableObject
{
    [TextArea]
    [Tooltip("에디터에서 확인용 설명 (게임 로직에는 영향 없음)")]
    public string description;

    /// <summary>
    /// 총알이 발사되어 초기화될 때 1회 호출됩니다.
    /// (예: 유도탄의 타겟 탐색, 총알 외형 변경 등)
    /// </summary>
    public virtual void OnInit(BulletController bullet) { }

    /// <summary>
    /// 총알이 매 프레임(또는 매 물리 프레임) 갱신될 때 호출됩니다.
    /// (예: 유도탄의 추적 로직)
    /// </summary>
    public virtual void OnTick(BulletController bullet, float deltaTime) { }

    /// <summary>
    /// 총알이 적에게 적중했을 때 호출됩니다. (적 시스템 미구현 -> 스텁)
    /// </summary>
    public virtual void OnHitEnemy(BulletController bullet, Collider2D enemy) { }

    /// <summary>
    /// 총알이 벽/장애물 등에 부딪혔을 때 호출됩니다.
    /// 반환값으로 이 효과가 기본 충돌 처리(튕김 등)를 덮어쓰고 싶은지 알릴 수 있습니다.
    /// </summary>
    public virtual void OnHitObstacle(BulletController bullet, Collider2D obstacle, BulletTargetType targetType) { }

    /// <summary>
    /// 총알이 소멸(파괴)되기 직전에 호출됩니다.
    /// (예: 폭발탄의 폭발 처리, 화상탄/냉기탄의 장판 생성)
    /// </summary>
    public virtual void OnBulletDestroyed(BulletController bullet) { }
}
