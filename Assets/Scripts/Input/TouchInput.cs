using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 모바일 터치 입력 공용 헬퍼(Legacy Input Manager 기준).
/// PlayerShooter(조준/격발·아이템 투척)와 CameraPanController(핀치 줌·두 손가락 팬)가
/// 손가락을 fingerId로 추적하고 UI 위 터치를 걸러내는 데 함께 쓴다.
/// </summary>
public static class TouchInput
{
    /// <summary>지정 fingerId의 터치를 이번 프레임에서 찾는다(있으면 true).</summary>
    public static bool TryGetTouch(int fingerId, out Touch touch)
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            var t = Input.GetTouch(i);
            if (t.fingerId == fingerId) { touch = t; return true; }
        }
        touch = default;
        return false;
    }

    /// <summary>그 손가락이 지금 UI(EventSystem) 위에 있는지.</summary>
    public static bool IsFingerOverUI(int fingerId)
    {
        var es = EventSystem.current;
        return es != null && es.IsPointerOverGameObject(fingerId);
    }

    /// <summary>이번 프레임 새로 눌린(Began) 손가락 중 UI 위가 아닌 첫 번째를 반환(있으면 true).</summary>
    public static bool TryGetBeganOffUI(out Touch touch)
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            var t = Input.GetTouch(i);
            if (t.phase == TouchPhase.Began && !IsFingerOverUI(t.fingerId))
            {
                touch = t;
                return true;
            }
        }
        touch = default;
        return false;
    }
}
