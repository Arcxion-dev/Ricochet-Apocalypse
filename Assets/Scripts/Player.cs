using UnityEngine;

public class Player : Entity
{
    protected override void DecreaseHP(int _amount)
    {

        health -= _amount;
        if (health <= 0)
        {
            GameManager.Instance?.OnPlayerDeath(); // 스테이지 실패 판정
            Destroy(gameObject); //임시
        }
    }
}
