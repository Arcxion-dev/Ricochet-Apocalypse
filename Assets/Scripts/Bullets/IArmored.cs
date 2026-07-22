/// <summary>
/// 철갑을 두른 적임을 나타내는 인터페이스. Enemy 시스템 담당자가 철갑 몬스터에
/// 이 인터페이스를 구현하면 철갑탄의 추가 데미지 배율이 자동으로 적용됩니다.
/// </summary>
public interface IArmored
{
    bool IsArmored { get; }
}
