using UnityEngine;

/// <summary>
/// 맵 객체의 총괄 컨트롤러. 맵 시작 시 블록 배치 -> 플레이어 위치 적용 순서로 초기화합니다.
/// </summary>
public class MapController : MonoBehaviour
{
    private BlockPlacementModule blockPlacementModule;
    private PlayerPositionModule playerPositionModule;

    private void Awake()
    {
        blockPlacementModule = GetComponent<BlockPlacementModule>();
        playerPositionModule = GetComponent<PlayerPositionModule>();
    }

    public void SetupMap(Transform player)
    {
        blockPlacementModule?.GenerateLayout();
        playerPositionModule?.ApplyToPlayer(player);
    }
}
