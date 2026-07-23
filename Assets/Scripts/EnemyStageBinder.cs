using UnityEngine;

/// <summary>
/// 적을 <see cref="GameManager"/> 의 스테이지 진행 판정에 연결하는 글루 컴포넌트.
/// 팀원 소유의 <see cref="Enemy"/>/<see cref="Entity"/> 코드를 수정하지 않고, 적 오브젝트에
/// 이 컴포넌트만 얹어 "등록 방식" 클리어 판정과 처치 스코어링을 붙인다.
///
/// - 스폰 시(Start) <see cref="GameManager.RegisterEnemy"/> 로 등록한다(살아있는 적 집합에 추가).
/// - 파괴 시(OnDestroy) 처치로 간주해 <see cref="GameManager.ReportEnemyKilled"/> +
///   <see cref="GameManager.UnregisterEnemy"/> 를 호출한다. 마지막 적이 사라지면 GameManager가
///   스테이지 클리어로 판정한다.
/// - 씬 언로드로 인한 파괴(씬 전환)는 처치가 아니므로 <c>gameObject.scene.isLoaded</c> 로 걸러낸다.
/// </summary>
[RequireComponent(typeof(Enemy))]
public class EnemyStageBinder : MonoBehaviour
{
    private Enemy _enemy;
    private bool _registered;

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    private void Start()
    {
        // GameManager.Awake(Instance 설정) 이후 시점이라 Start에서 등록한다.
        if (GameManager.Instance != null && _enemy != null)
        {
            GameManager.Instance.RegisterEnemy(_enemy);
            _registered = true;
        }
    }

    private void OnDestroy()
    {
        if (!_registered) return;

        // 씬 언로드(씬 전환)로 인한 파괴는 처치가 아니므로 무시한다.
        if (!gameObject.scene.isLoaded) return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReportEnemyKilled();
            GameManager.Instance.UnregisterEnemy(_enemy);
        }
    }
}
