using UnityEngine;

public class Player : Entity
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
