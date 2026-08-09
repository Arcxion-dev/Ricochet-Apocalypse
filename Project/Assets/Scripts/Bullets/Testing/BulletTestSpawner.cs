using UnityEngine;

/// <summary>
/// BulletTest 씬 전용 테스트 스포너.
/// 발사자/총기 시스템이 없으므로 키보드 1~9번으로 각 효과가 부착된 총알을 직접 발사합니다.
/// 방향은 마우스 커서 위치를 향합니다 (2D 탑다운 기준, 카메라는 직교/Orthographic 가정).
///
/// 사용법:
/// 1. 빈 GameObject에 이 컴포넌트를 부착
/// 2. Bullet Prefab에 BulletPrefab 할당
/// 3. Bullet SOs 배열에 순서대로 Bullet_1_ArmorPiercing ~ Bullet_9_Frost 할당 (인덱스 0 = 1번 키)
/// 4. Play 모드에서 1~9번 키를 누르면 해당 효과 총알이 발사됨
/// 0번 키는 효과 없는 기본 총알(Bullet_Default)을 발사해 기본 이동/튕김 테스트용으로 사용합니다.
/// </summary>
public class BulletTestSpawner : MonoBehaviour
{
    [Header("발사 설정")]
    [SerializeField] private BulletController bulletPrefab;
    [SerializeField] private Transform firePoint;

    [Header("1~9번 키에 대응하는 BulletSO (인덱스 0 = 1번 키)")]
    [SerializeField] private BulletSO[] bulletSOs = new BulletSO[9];

    [Header("0번 키 - 효과 없는 기본 총알 (기본 이동/튕김 테스트용)")]
    [SerializeField] private BulletSO defaultBulletSO;

    [Header("유도탄(5번) 테스트용 타겟 (비워두면 씬에서 Enemy 레이어 첫 오브젝트를 자동 탐색)")]
    [SerializeField] private Transform homingTestTarget;

    private Camera _mainCamera;

    private void Awake()
    {
        _mainCamera = Camera.main;
        if (firePoint == null) firePoint = transform;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0))
        {
            Fire(defaultBulletSO);
        }

        for (int i = 0; i < 9; i++)
        {
            KeyCode key = KeyCode.Alpha1 + i;
            KeyCode keypad = KeyCode.Keypad1 + i;
            if (Input.GetKeyDown(key) || Input.GetKeyDown(keypad))
            {
                Fire(bulletSOs[i]);
            }
        }
    }

    private void Fire(BulletSO data)
    {
        if (data == null)
        {
            Debug.LogWarning("[BulletTestSpawner] 해당 슬롯에 BulletSO가 할당되지 않았습니다.");
            return;
        }
        if (bulletPrefab == null)
        {
            Debug.LogWarning("[BulletTestSpawner] Bullet Prefab이 할당되지 않았습니다.");
            return;
        }

        Vector2 direction = GetDirectionToMouse();

        BulletController bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);

        Transform target = null;
        if (data.HasEffect<HomingEffectSO>())
        {
            target = homingTestTarget != null ? homingTestTarget : FindNearestEnemy();
        }

        bullet.Init(data, direction, target);

        Debug.Log($"[BulletTestSpawner] 발사: {data.name}, 방향: {direction}");
    }

    private Vector2 GetDirectionToMouse()
    {
        if (_mainCamera == null) return Vector2.right;

        Vector3 mouseWorld = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = firePoint.position.z;
        Vector2 dir = (Vector2)(mouseWorld - firePoint.position);
        return dir == Vector2.zero ? Vector2.right : dir.normalized;
    }

    private Transform FindNearestEnemy()
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        var all = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        Transform nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var t in all)
        {
            if (t.gameObject.layer != enemyLayer) continue;
            float d = Vector2.Distance(t.position, firePoint.position);
            if (d < nearestDist)
            {
                nearestDist = d;
                nearest = t;
            }
        }
        return nearest;
    }
}
