using UnityEngine;

public abstract class Entity : MonoBehaviour
{
    public int health = 100;

    /// <summary>
    /// 외부 시스템(총알 등)이 이 오브젝트에 피해를 줄 때 사용하는 공개 진입점.
    /// 내부적으로 DecreaseHP를 호출합니다.
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        DecreaseHP(amount);
    }
    
    protected abstract void DecreaseHP(int _amount);

}
