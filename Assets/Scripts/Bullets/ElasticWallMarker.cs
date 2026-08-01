using UnityEngine;

/// <summary>
/// 이 컴포넌트가 붙은 벽/장애물에 총알이 튕길 경우, 그 튕김은 총알의 튕김 횟수(maxBounceCount)를
/// 소모하지 않습니다. 즉 총알은 이 벽에서는 무제한으로 튕길 수 있습니다.
/// (다른 일반 벽에 튕길 때는 평소대로 카운트가 소모됩니다.)
///
/// 사용법: 기존 벽 프리팹(예: WallPrefab_Basic)을 복제한 뒤 이 컴포넌트만 추가로 붙이면
/// '탄성벽' 오브젝트가 됩니다. ObstacleTypeMarker는 그대로 두어도(Wall) 무방합니다 —
/// BulletController는 튕김 여부(Bounce/Penetrate/Destroy) 판정은 기존 로직을 그대로 따르되,
/// 실제 튕길 때 이 마커의 존재만 확인해 카운트 소모를 건너뜁니다.
/// </summary>
public class ElasticWallMarker : MonoBehaviour
{
}
