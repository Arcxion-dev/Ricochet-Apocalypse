using UnityEngine;

public abstract class Entity : MonoBehaviour
{
    public int health = 100;
    
    protected abstract void DecreaseHP(int _amount);

}
