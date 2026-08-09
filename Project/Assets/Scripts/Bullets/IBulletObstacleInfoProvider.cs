/// <summary>
/// 장애물 담당자가 구현해야 하는 인터페이스.
/// 장애물 오브젝트에 이 인터페이스를 구현한 컴포넌트를 붙이면
/// BulletController가 태그 기반 임시 판정 대신 이 값을 사용합니다.
///
/// 예시:
/// public class WallObstacle : MonoBehaviour, IBulletObstacleInfoProvider
/// {
///     public BulletTargetType TargetType => BulletTargetType.Wall;
/// }
/// </summary>
public interface IBulletObstacleInfoProvider
{
    BulletTargetType TargetType { get; }
}
