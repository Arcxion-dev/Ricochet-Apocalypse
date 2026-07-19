using UnityEngine;

public class Enemy : Entity
{
    protected override void DecreaseHP(int _amount)
    {

        health -= _amount;
        if (health <= 0)
        {
            Destroy(gameObject); //임시
        }
    }
}
