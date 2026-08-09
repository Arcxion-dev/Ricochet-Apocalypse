using System;
using UnityEngine;

/// <summary>
/// 잘라둔 폭발 이펙트 스프라이트 시트를 프레임 애니메이션으로 재생하는 폭발 연출.
/// 파티클/Animator 없이, 지정한 지속시간 동안 프레임을 순서대로 넘기며 "생겼다 사라지는" 폭발을 보여주고 스스로 정리한다.
///
/// - PNG는 Resources 아래에 Multiple 모드로 슬라이스되어 있어(폭팔이펙트_0 … _N),
///   <see cref="Resources.LoadAll"/>로 모든 프레임을 한 번에 로드해 접미사 숫자 순으로 정렬한다.
/// - 폭발탄(<see cref="ExplosiveEffectSO"/>) / 폭격지원 / 수류탄(<see cref="UsableItemSO"/>)이 터질 때
///   <see cref="Spawn"/>으로 지점에 1회 생성한다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ExplosionSpriteEffect : MonoBehaviour
{
    // Resources 폴더 기준 경로(확장자 제외). 폴더명에 공백/한글이 있어도 그대로 사용한다.
    private const string SpriteResourcePath = "실제 사용할 리소스/VFX/폭팔이펙트";

    // 슬라이스된 프레임들은 텍스처마다 고정이라 한 번만 로드해 캐시한다.
    private static Sprite[] _frames;
    private static bool _framesLoaded;

    private SpriteRenderer _sr;
    private Sprite[] _clip;
    private float _duration;
    private float _elapsed;

    /// <summary>
    /// 지점에 폭발 애니메이션을 1회 생성한다.
    /// </summary>
    /// <param name="position">월드 좌표.</param>
    /// <param name="worldSize">이펙트 지름(월드 유닛). 보통 폭발 반경 * 2 정도를 넘긴다.</param>
    /// <param name="duration">프레임 전체를 재생하는 총 시간(초). 이 시간에 걸쳐 생겼다 사라진다.</param>
    public static void Spawn(Vector3 position, float worldSize, float duration = 1.2f)
    {
        var frames = LoadFrames();
        if (frames == null || frames.Length == 0) return;

        var go = new GameObject("ExplosionSpriteEffect");
        go.transform.position = position;
        var fx = go.AddComponent<ExplosionSpriteEffect>();
        fx.Init(frames, worldSize, duration);
    }

    private static Sprite[] LoadFrames()
    {
        if (!_framesLoaded)
        {
            var all = Resources.LoadAll<Sprite>(SpriteResourcePath);
            if (all == null || all.Length == 0)
                Debug.LogWarning($"[ExplosionSpriteEffect] 폭발 스프라이트를 찾을 수 없습니다: Resources/{SpriteResourcePath}");
            else
                Array.Sort(all, (a, b) => FrameIndex(a.name).CompareTo(FrameIndex(b.name)));

            _frames = all;
            _framesLoaded = true;
        }
        return _frames;
    }

    // "폭팔이펙트_12" → 12. LoadAll 순서가 보장되지 않으므로 접미사 숫자로 프레임 순서를 맞춘다.
    private static int FrameIndex(string spriteName)
    {
        int us = spriteName.LastIndexOf('_');
        if (us >= 0 && us < spriteName.Length - 1 && int.TryParse(spriteName.Substring(us + 1), out int n))
            return n;
        return 0;
    }

    private void Init(Sprite[] frames, float worldSize, float duration)
    {
        _clip = frames;
        _duration = Mathf.Max(0.05f, duration);

        _sr = GetComponent<SpriteRenderer>();
        _sr.sprite = frames[0];
        _sr.sortingOrder = 500; // 타일맵/적/총알 위에 보이도록.

        // 프레임은 모두 같은 크기이므로 첫 프레임 기준으로 원하는 월드 지름에 맞춰 스케일한다(비율 유지).
        float spriteWorld = Mathf.Max(frames[0].bounds.size.x, frames[0].bounds.size.y);
        float scale = spriteWorld > 0.0001f ? worldSize / spriteWorld : 1f;
        transform.localScale = Vector3.one * scale;
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        float t = _elapsed / _duration; // 0..1
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        // 경과 비율을 프레임 인덱스로 환산해 순서대로 넘긴다(마지막 프레임에서 소멸).
        int frame = Mathf.Clamp(Mathf.FloorToInt(t * _clip.Length), 0, _clip.Length - 1);
        _sr.sprite = _clip[frame];
    }
}
