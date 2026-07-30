using System;
using UnityEngine;

public abstract class Entity : MonoBehaviour
{
    public int health = 100;

    /// <summary>사망 시점(체력 0 이하)에 발생. 타격감 연출 등 외부 구독용 훅.</summary>
    public event Action OnDeath;

    /// <summary>피격 시점(데미지 amount > 0)마다 발생. 도망/소환/텔레포트 등 "피격 시" 특수 모듈이 구독.
    /// 사망을 유발한 타격에서도 발생하므로, 구독자는 필요 시 health &lt;= 0 여부로 생존 확인 후 동작해야 한다.</summary>
    public event Action<int> OnDamaged;

    /// <summary>
    /// 외부 시스템(총알 등)이 이 오브젝트에 피해를 줄 때 사용하는 공개 진입점.
    /// 내부적으로 DecreaseHP를 호출합니다.
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        DecreaseHP(amount);
        OnDamaged?.Invoke(amount);
    }

    /// <summary>하위 클래스가 사망 판정 시점에 호출해 OnDeath 구독자에게 알린다.</summary>
    protected void RaiseOnDeath() => OnDeath?.Invoke();

    protected abstract void DecreaseHP(int _amount);

}
