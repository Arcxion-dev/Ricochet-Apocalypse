using UnityEngine;

/// <summary>
/// 사망 모듈: 총알이 접촉하는 즉시 게임오버로 이어지는 장애물(민간인 등)에 부착합니다.
/// </summary>
public class DeathModule : MonoBehaviour
{
    [Header("사망 판정 설정")]
    [SerializeField] private bool triggersGameOverOnHit = false;

    public bool TriggersGameOverOnHit => triggersGameOverOnHit;

    public void SetTriggersGameOver(bool value) => triggersGameOverOnHit = value;

    public void TriggerGameOver()
    {
        if (!triggersGameOverOnHit) return;
        // TODO: 실제 게임오버 매니저와 연결 (예: GameManager.Instance.GameOver();)
        Debug.Log($"{name}: 민간인/즉사 장애물 피격 - 게임오버 트리거");
    }
}
