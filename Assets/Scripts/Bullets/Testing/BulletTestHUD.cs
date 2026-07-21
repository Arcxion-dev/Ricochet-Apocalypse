using UnityEngine;

/// <summary>
/// BulletTest 씬에서 화면에 조작법과 각 키에 매핑된 효과를 표시하는 디버그 HUD.
/// OnGUI 기반의 간단한 텍스트 오버레이입니다. (테스트 전용, 실제 게임 UI 아님)
/// </summary>
public class BulletTestHUD : MonoBehaviour
{
    private readonly string[] _labels = new string[]
    {
        "0: 기본(효과 없음)",
        "1: 철갑탄",
        "2: 폭발탄",
        "3: 분열탄",
        "4: 저지탄",
        "5: 유도탄",
        "6: 중력자탄",
        "7: 전력탄",
        "8: 화상탄",
        "9: 냉기탄",
    };

    private void OnGUI()
    {
        int padding = 10;
        int lineHeight = 20;
        int width = 240;
        int height = (_labels.Length + 2) * lineHeight + padding * 2;

        GUI.Box(new Rect(padding, padding, width, height), "Bullet Test");

        int y = padding + lineHeight;
        GUI.Label(new Rect(padding + 10, y, width - 20, lineHeight), "마우스 방향으로 발사");
        y += lineHeight;

        foreach (var label in _labels)
        {
            GUI.Label(new Rect(padding + 10, y, width - 20, lineHeight), label);
            y += lineHeight;
        }
    }
}
