/// <summary>
/// 이동 저지(슬로우/스턴) 효과를 받을 수 있는 대상이 구현하는 인터페이스.
/// Enemy 시스템 담당자가 이동 제어 컴포넌트에 이 인터페이스를 구현하면
/// 저지탄 효과가 자동으로 연동됩니다. 아직 구현체가 없으므로 저지탄은
/// 이 인터페이스 탐색에 실패할 경우 로그만 남기고 넘어갑니다.
/// </summary>
public interface ISuppressible
{
    void ApplySuppression(float duration, float slowRatio);
}
