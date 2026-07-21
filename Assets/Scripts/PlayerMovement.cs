using UnityEngine;

/// <summary>
/// WASD로 플레이어 구체를 상하좌우(XY 평면)로 이동시킨다.
/// - 방향키는 카메라 팬(CameraPanController) 전용이라 여기서는 WASD만 읽는다.
/// - 탑뷰 2D 기준으로 회전 없이 위치만 옮긴다(Z 고정).
/// - 이동 속도는 인스펙터에서 조절 가능한 프로퍼티(_moveSpeed, 기본 3.0).
///
/// 입력은 Legacy Input Manager 기준. 물리 대신 Transform 이동을 사용한다
/// (임시 비주얼 단계라 Rigidbody/충돌 이동은 아직 도입하지 않음).
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    [Header("이동")]
    [Tooltip("플레이어 이동 속도 (월드 유닛/초). 밸런싱 후 수정 가능.")]
    [SerializeField] private float _moveSpeed = 3.0f;

    /// <summary>이동 속도(월드 유닛/초). 런타임에 읽고 쓸 수 있다.</summary>
    public float MoveSpeed
    {
        get => _moveSpeed;
        set => _moveSpeed = Mathf.Max(0f, value);
    }

    private void Update()
    {
        // WASD만 플레이어를 움직인다(방향키는 카메라 전용).
        float x = 0f;
        float y = 0f;
        if (Input.GetKey(KeyCode.A)) x -= 1f;
        if (Input.GetKey(KeyCode.D)) x += 1f;
        if (Input.GetKey(KeyCode.S)) y -= 1f;
        if (Input.GetKey(KeyCode.W)) y += 1f;
        if (x == 0f && y == 0f) return;

        // 대각선 이동이 빨라지지 않도록 정규화.
        Vector3 move = new Vector3(x, y, 0f).normalized * (_moveSpeed * Time.deltaTime);
        transform.position += move; // Z와 회전은 유지
    }
}
