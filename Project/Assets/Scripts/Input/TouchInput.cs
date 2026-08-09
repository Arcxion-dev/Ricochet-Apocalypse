using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>이 프레임의 터치 한 점(플랫폼 <see cref="Touch"/>를 테스트하기 쉬운 값 타입으로 감쌈).</summary>
public struct TouchPoint
{
    public int fingerId;
    public Vector2 position;
    public TouchPhase phase;
    public bool overUI; // 이 손가락이 UI(EventSystem) 위인지 — 미리 계산해 담는다.
}

/// <summary>터치 소스 추상화. 기본은 실제 <see cref="Input"/>이며, 테스트는 가짜 소스를 주입한다.</summary>
public interface ITouchProvider
{
    int Count { get; }
    TouchPoint Get(int index);
}

/// <summary>
/// 모바일 터치 입력 공용 헬퍼(Legacy Input Manager 기준). PlayerShooter(조준/격발·아이템 투척)와
/// CameraPanController(핀치 줌·두 손가락 팬)가 손가락을 fingerId로 추적하고 UI 위 터치를 거르는 데
/// 함께 쓴다. 모든 터치 읽기가 <see cref="Provider"/>를 통하므로, 테스트에서 합성 제스처를 주입할 수 있다.
/// </summary>
public static class TouchInput
{
    /// <summary>현재 터치 소스. 테스트가 가짜 provider로 바꿔 합성 제스처를 먹인다(끝나면 <see cref="UseRealInput"/>).</summary>
    public static ITouchProvider Provider = new RealTouchProvider();

    public static void UseRealInput() => Provider = new RealTouchProvider();

    public static int Count => Provider.Count;
    public static TouchPoint Get(int index) => Provider.Get(index);

    /// <summary>지정 fingerId의 터치를 이번 프레임에서 찾는다(있으면 true).</summary>
    public static bool TryGet(int fingerId, out TouchPoint tp)
    {
        int n = Provider.Count;
        for (int i = 0; i < n; i++)
        {
            var t = Provider.Get(i);
            if (t.fingerId == fingerId) { tp = t; return true; }
        }
        tp = default;
        return false;
    }

    /// <summary>이번 프레임 새로 눌린(Began) 손가락 중 UI 위가 아닌 첫 번째를 반환(있으면 true).</summary>
    public static bool TryGetBeganOffUI(out TouchPoint tp)
    {
        int n = Provider.Count;
        for (int i = 0; i < n; i++)
        {
            var t = Provider.Get(i);
            if (t.phase == TouchPhase.Began && !t.overUI) { tp = t; return true; }
        }
        tp = default;
        return false;
    }

    /// <summary>실제 <see cref="Input"/> 터치를 읽는 기본 provider. UI 히트는 EventSystem으로 채운다.</summary>
    public sealed class RealTouchProvider : ITouchProvider
    {
        public int Count => Input.touchCount;

        public TouchPoint Get(int index)
        {
            var t = Input.GetTouch(index);
            var es = EventSystem.current;
            return new TouchPoint
            {
                fingerId = t.fingerId,
                position = t.position,
                phase = t.phase,
                overUI = es != null && es.IsPointerOverGameObject(t.fingerId),
            };
        }
    }

    /// <summary>테스트용 주입 provider. <see cref="Points"/>를 프레임마다 세팅해 합성 제스처를 재현한다.</summary>
    public sealed class FakeTouchProvider : ITouchProvider
    {
        public readonly List<TouchPoint> Points = new List<TouchPoint>();
        public int Count => Points.Count;
        public TouchPoint Get(int index) => Points[index];
    }
}
