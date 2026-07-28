using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 사운드(BGM/SFX)를 문자열 id로 등록해두고 재생하는 싱글턴 매니저.
/// - 인스펙터에 (id, AudioClip, volume, loop) 목록을 등록해두면 다른 스크립트는
///   SoundManager.Instance.PlaySfx("id") / PlayMusic("id")로만 호출한다.
/// - SFX는 동시에 여러 개가 겹쳐 재생될 수 있어 AudioSource 풀을 순환 사용한다.
/// - BGM은 한 번에 하나만 재생한다고 보고 전용 AudioSource 하나를 쓴다.
/// </summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [System.Serializable]
    public class Sound
    {
        public string id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        public bool loop;
    }

    [Header("SFX")]
    [SerializeField] private Sound[] _sfxSounds;
    [SerializeField] private int _sfxPoolSize = 8;
    [Range(0f, 1f)] [SerializeField] private float _sfxVolume = 1f;

    [Header("Music")]
    [SerializeField] private Sound[] _musicSounds;
    [Range(0f, 1f)] [SerializeField] private float _musicVolume = 1f;

    private Dictionary<string, Sound> _sfxLookup;
    private Dictionary<string, Sound> _musicLookup;

    private AudioSource[] _sfxPool;
    private int _nextSfxIndex;
    private AudioSource _musicSource;
    private Sound _currentMusic;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _sfxLookup = BuildLookup(_sfxSounds);
        _musicLookup = BuildLookup(_musicSounds);

        _sfxPool = new AudioSource[Mathf.Max(1, _sfxPoolSize)];
        for (int i = 0; i < _sfxPool.Length; i++)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            _sfxPool[i] = source;
        }

        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
    }

    private static Dictionary<string, Sound> BuildLookup(Sound[] sounds)
    {
        var lookup = new Dictionary<string, Sound>();
        if (sounds == null) return lookup;

        foreach (var sound in sounds)
        {
            if (sound == null || string.IsNullOrEmpty(sound.id)) continue;
            if (!lookup.TryAdd(sound.id, sound))
                Debug.LogWarning($"[SoundManager] 중복된 사운드 id: {sound.id}");
        }
        return lookup;
    }

    // ───────────────────────── SFX ─────────────────────────

    /// <summary>등록된 id의 SFX를 재생한다. 여러 SFX가 겹쳐 재생될 수 있다.</summary>
    public void PlaySfx(string id) => PlaySfx(id, 1f);

    /// <summary>피치를 지정해 재생한다(예: 헤드샷음을 같은 클립으로 더 높게).</summary>
    public void PlaySfx(string id, float pitch)
    {
        if (!_sfxLookup.TryGetValue(id, out var sound) || sound.clip == null)
        {
            Debug.LogWarning($"[SoundManager] SFX id를 찾을 수 없음: {id}");
            return;
        }
        if (_sfxPool == null || _sfxPool.Length == 0) return;

        int idx = _nextSfxIndex;
        _nextSfxIndex = (_nextSfxIndex + 1) % _sfxPool.Length;

        // 씬 전환/중복 매니저 등으로 풀의 AudioSource가 파괴됐을 수 있다. 파괴됐으면 다시 만들어(자가 복구)
        // 사운드 예외가 게임플레이(예: 총알 벽 튕김)를 끊지 않도록 방어한다.
        var source = _sfxPool[idx];
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            _sfxPool[idx] = source;
        }
        source.pitch = pitch;
        source.PlayOneShot(sound.clip, sound.volume * _sfxVolume);
    }

    public void SetSfxVolume(float volume) => _sfxVolume = Mathf.Clamp01(volume);

    // ───────────────────────── Music ─────────────────────────

    /// <summary>등록된 id의 BGM을 재생한다. 이미 같은 곡이 재생 중이면 무시한다.</summary>
    public void PlayMusic(string id)
    {
        if (!_musicLookup.TryGetValue(id, out var sound) || sound.clip == null)
        {
            Debug.LogWarning($"[SoundManager] Music id를 찾을 수 없음: {id}");
            return;
        }

        if (_currentMusic == sound && _musicSource.isPlaying) return;

        _currentMusic = sound;
        _musicSource.clip = sound.clip;
        _musicSource.loop = sound.loop;
        _musicSource.volume = sound.volume * _musicVolume;
        _musicSource.Play();
    }

    public void StopMusic()
    {
        _musicSource.Stop();
        _currentMusic = null;
    }

    public void SetMusicVolume(float volume)
    {
        _musicVolume = Mathf.Clamp01(volume);
        if (_currentMusic != null)
            _musicSource.volume = _currentMusic.volume * _musicVolume;
    }
}
