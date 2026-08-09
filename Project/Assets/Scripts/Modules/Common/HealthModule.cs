using System;
using UnityEngine;

/// <summary>
/// 체력 모듈: 체력을 설정하고 관리합니다.
/// 친구분이 만든 Entity/Enemy 체력 시스템이 있는 오브젝트에서는 그쪽이 우선 사용되고,
/// 이 모듈은 Entity가 없는 오브젝트(예: 장애물)를 위한 예비/범용 체력 시스템으로 사용됩니다.
/// </summary>
public class HealthModule : MonoBehaviour
{
    [Header("체력 설정")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsDead => currentHealth <= 0f;

    public event Action<float, float> OnHealthChanged;
    public event Action OnDeath;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void SetMaxHealth(float value, bool refill = true)
    {
        maxHealth = value;
        if (refill) currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f) return;
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        if (IsDead) OnDeath?.Invoke();
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
