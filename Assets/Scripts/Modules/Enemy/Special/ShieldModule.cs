using System;
using UnityEngine;

/// <summary>
/// 방패 모듈: 본체 체력과 별개인 방패 체력 풀을 둔다.
/// EnemyController가 최종 대미지를 본체에 적용하기 직전에 이 모듈로 먼저 흘려보낸다.
/// 한 번의 피격 대미지가 남은 방패 체력보다 커도 초과분은 버려지고(오버킬 방지) 본체로 넘어가지 않는다.
/// 방패가 깨진 뒤에는 <see cref="IsBroken"/>이 true가 되어 이후 피격은 일반 몹과 동일하게 본체로 직행한다.
/// </summary>
public class ShieldModule : MonoBehaviour
{
    [Header("방패 설정")]
    [SerializeField] private float shieldHealth = 10f;
    private float currentShieldHealth;

    public float MaxShieldHealth => shieldHealth;
    public float CurrentShieldHealth => currentShieldHealth;
    public bool IsBroken { get; private set; }

    public event Action<float, float> OnShieldChanged;
    public event Action OnShieldBroken;

    private void Awake()
    {
        currentShieldHealth = shieldHealth;
    }

    /// <summary>방패 체력에서 대미지를 흡수한다. 남은 체력을 초과하는 분량은 버려진다.</summary>
    public void AbsorbDamage(float amount)
    {
        if (IsBroken || amount <= 0f) return;

        currentShieldHealth = Mathf.Max(0f, currentShieldHealth - amount);
        OnShieldChanged?.Invoke(currentShieldHealth, shieldHealth);

        if (currentShieldHealth <= 0f)
        {
            IsBroken = true;
            OnShieldBroken?.Invoke();
            Debug.Log($"[ShieldModule] {name} 방패 파괴됨 - 이후 일반 몹과 동일하게 피격");
        }
    }
}
