using System;
using UnityEngine;

/// <summary>
/// 체력 모듈: 체력을 설정하고 관리합니다.
/// Enemy, Obstacle 등 체력이 필요한 모든 객체에 공통으로 부착 가능합니다.
/// </summary>
public class HealthModule : MonoBehaviour
{
    [Header("체력 설정")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsDead => currentHealth <= 0f;

    /// <summary>(현재 체력, 최대 체력)</summary>
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
