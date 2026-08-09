using UnityEngine;

/// <summary>
/// 장애물 오브젝트에 부착하여 BulletController에게 명시적으로 타입을 알려주는 컴포넌트.
/// 인스펙터 드롭다운으로 타입을 지정하면, BulletController는 태그 추측 대신 이 값을 사용합니다.
///
/// 테스트/개발 단계의 임시 컴포넌트입니다. 장애물 담당자가 자체 시스템
/// (파괴 가능 여부, 체력, 시야 차단 등)을 만들 때 이 인터페이스만 구현하면
/// 이 컴포넌트를 대체할 수 있습니다.
/// </summary>
public class ObstacleTypeMarker : MonoBehaviour, IBulletObstacleInfoProvider
{
    [SerializeField] private BulletTargetType targetType = BulletTargetType.Wall;

    public BulletTargetType TargetType => targetType;
}
