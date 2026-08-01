using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 총알 하나의 정적 데이터(스탯 + 부착된 효과 목록)를 정의하는 ScriptableObject.
/// 새 총알 종류를 만들 때는 코드를 새로 짜지 않고
/// "Create > Bullet > BulletSO" 로 에셋을 만든 뒤 EffectSO들을 리스트에 끼우면 됩니다.
/// </summary>
[CreateAssetMenu(fileName = "New BulletSO", menuName = "Bullet/BulletSO")]
public class BulletSO : ScriptableObject
{
    [Header("기본 스탯")]
    [Tooltip("총알 이동 속도 (units/sec)")]
    public float speed = 15f;

    [Tooltip("총알 기본 데미지")]
    public float damage = 10f;

    [Tooltip("총알 생존 시간(초). 0 이하면 무제한")]
    public float lifeTime = 5f;

    [Tooltip("벽에 튕길 수 있는 최대 횟수. 0이면 첫 벽 충돌 시 바로 소멸")]
    public int maxBounceCount = 1;

    [Header("스프라이트/이펙트 (선택)")]
    public Sprite bulletSprite;
    public GameObject hitVfxPrefab;
    public GameObject destroyVfxPrefab;

    [Header("부착 효과 목록")]
    [Tooltip("이 총알에 적용할 효과 SO들. 순서대로 OnInit/OnTick/OnHit... 이 호출됩니다.")]
    public List<BulletEffectSO> effects = new List<BulletEffectSO>();

    /// <summary>
    /// 특정 타입의 효과가 부착되어 있는지 확인하는 헬퍼.
    /// (예: 벽 충돌 판정 시 철갑탄 효과 보유 여부 확인용)
    /// </summary>
    public bool HasEffect<T>() where T : BulletEffectSO
    {
        foreach (var e in effects)
        {
            if (e is T) return true;
        }
        return false;
    }

    public T GetEffect<T>() where T : BulletEffectSO
    {
        foreach (var e in effects)
        {
            if (e is T typed) return typed;
        }
        return null;
    }
}
