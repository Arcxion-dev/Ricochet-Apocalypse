using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 조작 모듈: 총알이 접촉했을 때 특수한 기능이 실행되는 장애물에 부착합니다.
/// 특수 기능의 실제 내용은 추후 정의될 예정이며, 지금은 UnityEvent 훅만 제공합니다.
/// (인스펙터에서 원하는 함수를 onBulletContact에 등록해 사용하세요)
/// </summary>
public class InteractModule : MonoBehaviour
{
    public UnityEvent onBulletContact;

    public void TriggerInteraction()
    {
        // TODO: 특수 기능 구현 (스위치 작동, 폭발, 텔레포트, 아이템 드랍 등)
        onBulletContact?.Invoke();
    }
}
