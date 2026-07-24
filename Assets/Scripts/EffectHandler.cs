using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 파티클 이펙트를 오브젝트 풀링으로 관리하는 이펙트 핸들러.
/// - 인스펙터에서 이펙트 이름 + 프리팹 + 풀 개수를 등록해두면
///   Awake 시점에 미리 정해진 개수만큼 생성 후 비활성화(숨김) 상태로 대기시킨다.
/// - 외부에서는 Play(effectName, position, rotation) 형태로 호출하면
///   풀에서 비활성 오브젝트를 하나 꺼내 위치/회전을 세팅하고 재생한 뒤,
///   파티클 재생이 끝나면 자동으로 다시 숨겨서(SetActive(false)) 풀에 반환한다.
/// </summary>
public class EffectHandler : MonoBehaviour
{
    [Serializable]
    public class EffectEntry
    {
        [Tooltip("외부에서 Play(name)을 호출할 때 사용할 식별자")]
        public string effectName;

        [Tooltip("풀링할 파티클 이펙트 프리팹 (ParticleSystem 포함)")]
        public GameObject prefab;

        [Tooltip("미리 생성해둘 인스턴스 개수")]
        [Min(1)]
        public int poolSize = 5;

        [Tooltip("풀이 부족할 때 자동으로 추가 생성할지 여부 (false면 재생 요청을 무시)")]
        public bool allowGrow = false;
    }

    public static EffectHandler Instance { get; private set; }

    [SerializeField]
    private List<EffectEntry> effectEntries = new List<EffectEntry>();

    [Tooltip("생성된 풀 오브젝트들을 정리해서 담아둘 부모 트랜스폼 (비워두면 자동 생성)")]
    [SerializeField]
    private Transform poolRoot;

    // effectName -> 대기 큐(비활성 상태의 인스턴스들)
    private readonly Dictionary<string, Queue<GameObject>> pools = new Dictionary<string, Queue<GameObject>>();
    // effectName -> 원본 프리팹 (allowGrow 시 추가 생성용)
    private readonly Dictionary<string, EffectEntry> entryLookup = new Dictionary<string, EffectEntry>();
    // 재생 중인 이펙트를 감시하는 코루틴을 추적 (중복 방지 및 정리용)
    private readonly Dictionary<GameObject, Coroutine> activeReturnRoutines = new Dictionary<GameObject, Coroutine>();

    public List<string> hitName;
    public List<string> bounceName;
    public List<string> explosionName;



    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (poolRoot == null)
        {
            var rootObj = new GameObject("EffectPool_Root");
            rootObj.transform.SetParent(transform, false);
            poolRoot = rootObj.transform;
        }

        BuildPools();
    }

    /// <summary>
    /// 인스펙터에 등록된 각 이펙트 항목에 대해 poolSize만큼 미리 생성하고 비활성화한다.
    /// </summary>
    private void BuildPools()
    {
        foreach (var entry in effectEntries)
        {
            if (entry == null || entry.prefab == null || string.IsNullOrEmpty(entry.effectName))
            {
                Debug.LogWarning("[EffectHandler] 잘못된 EffectEntry가 있어 건너뜁니다.");
                continue;
            }

            if (!pools.ContainsKey(entry.effectName))
            {
                pools[entry.effectName] = new Queue<GameObject>();
            }
            entryLookup[entry.effectName] = entry;

            for (int i = 0; i < entry.poolSize; i++)
            {
                var instance = CreatePooledInstance(entry);
                pools[entry.effectName].Enqueue(instance);
            }
        }
    }

    /// <summary>
    /// 프리팹으로부터 새 인스턴스를 만들고, 생성 즉시 숨긴다(SetActive(false)).
    /// </summary>
    private GameObject CreatePooledInstance(EffectEntry entry)
    {
        var instance = Instantiate(entry.prefab, poolRoot);
        instance.name = entry.effectName;
        instance.SetActive(false); // 생성 후 즉시 숨김
        return instance;
    }

    /// <summary>
    /// 등록된 이펙트를 지정된 위치/회전으로 재생한다.
    /// 풀에서 비활성 인스턴스를 꺼내 활성화하고, ParticleSystem을 재생시킨다.
    /// 재생이 끝나면 자동으로 다시 비활성화되어 풀로 반환된다.
    /// </summary>
    /// <param name="effectName">EffectEntry에 등록한 이펙트 식별자</param>
    /// <param name="position">재생할 월드 좌표</param>
    /// <param name="rotation">재생할 회전값 (생략 시 identity)</param>
    /// <param name="parent">붙일 부모 트랜스폼 (생략 시 풀 루트에 유지)</param>
    /// <returns>재생에 사용된 GameObject. 풀이 비어있고 allowGrow=false면 null.</returns>
    public GameObject Play(string effectName, Vector3 position, Quaternion? rotation = null, Transform parent = null)
    {
        if (!pools.TryGetValue(effectName, out var queue) || !entryLookup.TryGetValue(effectName, out var entry))
        {
            Debug.LogWarning($"[EffectHandler] '{effectName}' 이펙트가 등록되어 있지 않습니다.");
            return null;
        }

        GameObject instance = GetFromPool(effectName, queue, entry);
        if (instance == null)
        {
            return null; // 풀 고갈 + allowGrow=false
        }

        var t = instance.transform;
        if (parent != null)
        {
            t.SetParent(parent, false);
        }
        else if (t.parent != poolRoot)
        {
            t.SetParent(poolRoot, false);
        }

        t.position = position;
        t.rotation = rotation ?? Quaternion.identity;

        instance.SetActive(true);

        // 기존에 대기 중이던 반환 코루틴이 있다면 정리
        if (activeReturnRoutines.TryGetValue(instance, out var existingRoutine))
        {
            StopCoroutine(existingRoutine);
            activeReturnRoutines.Remove(instance);
        }

        RestartAllParticleSystems(instance);

        float duration = GetEffectDuration(instance);
        var routine = StartCoroutine(ReturnToPoolAfter(effectName, instance, duration));
        activeReturnRoutines[instance] = routine;

        return instance;
    }

    /// <summary>편의 오버로드: 회전 없이 위치만으로 재생.</summary>
    public GameObject Play(string effectName, Vector3 position)
    {
        return Play(effectName, position, Quaternion.identity, null);
    }

    /// <summary>편의 오버로드: 특정 트랜스폼 위치에 그대로 재생 (부모로 붙임).</summary>
    public GameObject PlayAttached(string effectName, Transform target)
    {
        if (target == null) return null;
        return Play(effectName, target.position, target.rotation, target);
    }

    /// <summary>
    /// 풀에서 비활성 인스턴스를 꺼낸다. 없으면 allowGrow 여부에 따라 새로 만들거나 null을 반환.
    /// </summary>
    private GameObject GetFromPool(string effectName, Queue<GameObject> queue, EffectEntry entry)
    {
        while (queue.Count > 0)
        {
            var candidate = queue.Dequeue();
            if (candidate != null)
            {
                return candidate;
            }
        }

        if (entry.allowGrow)
        {
            return CreatePooledInstance(entry);
        }

        Debug.LogWarning($"[EffectHandler] '{effectName}' 풀이 고갈되었습니다. poolSize를 늘리거나 allowGrow를 켜세요.");
        return null;
    }

    /// <summary>
    /// 인스턴스와 모든 자식의 ParticleSystem을 처음부터 다시 재생한다.
    /// </summary>
    private void RestartAllParticleSystems(GameObject instance)
    {
        var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
            ps.Clear(true);
            ps.Simulate(0f, true, true);
            ps.Play(true);
        }
    }

    /// <summary>
    /// 인스턴스 내 ParticleSystem 중 가장 긴 총 재생 시간(duration + startLifetime 최대값)을 계산한다.
    /// </summary>
    private float GetEffectDuration(GameObject instance)
    {
        var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        float maxDuration = 0f;

        for (int i = 0; i < systems.Length; i++)
        {
            var main = systems[i].main;
            float lifetime = GetMaxStartLifetime(main.startLifetime);
            float total = main.duration + lifetime;
            if (total > maxDuration)
            {
                maxDuration = total;
            }
        }

        // 안전 마진
        return maxDuration > 0f ? maxDuration + 0.1f : 1f;
    }

    private float GetMaxStartLifetime(ParticleSystem.MinMaxCurve curve)
    {
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return curve.constant;
            case ParticleSystemCurveMode.TwoConstants:
                return Mathf.Max(curve.constantMin, curve.constantMax);
            case ParticleSystemCurveMode.Curve:
                return curve.curveMultiplier;
            case ParticleSystemCurveMode.TwoCurves:
                return curve.curveMultiplier;
            default:
                return 1f;
        }
    }

    /// <summary>
    /// 지정된 시간 후 파티클을 정지하고 오브젝트를 비활성화(숨김)하여 풀로 반환한다.
    /// </summary>
    private IEnumerator ReturnToPoolAfter(string effectName, GameObject instance, float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnToPool(effectName, instance);
    }

    /// <summary>
    /// 이펙트를 즉시 중단하고 풀로 반환하고 싶을 때 외부에서 호출할 수 있는 함수.
    /// </summary>
    public void Stop(string effectName, GameObject instance)
    {
        if (instance == null) return;

        if (activeReturnRoutines.TryGetValue(instance, out var routine))
        {
            StopCoroutine(routine);
            activeReturnRoutines.Remove(instance);
        }

        ReturnToPool(effectName, instance);
    }

    private void ReturnToPool(string effectName, GameObject instance)
    {
        if (instance == null) return;

        activeReturnRoutines.Remove(instance);

        var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        instance.SetActive(false); // 재생 종료 후 다시 숨김
        instance.transform.SetParent(poolRoot, false);

        if (pools.TryGetValue(effectName, out var queue))
        {
            queue.Enqueue(instance);
        }
        else
        {
            // 혹시 모를 예외 상황: 풀이 없으면 그냥 파괴
            Destroy(instance);
        }
    }
 


    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
    


}
