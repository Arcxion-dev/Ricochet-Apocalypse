using System;
using UnityEngine;

/// <summary>
/// 게임 설정값(볼륨/화면)을 PlayerPrefs로 영속화하는 정적 저장소.
/// SETTINGS 메뉴 UI가 이 값을 읽고 쓰며, 값이 바뀌면 즉시 적용+저장한다.
///
/// 적용 범위(현 단계):
/// - 마스터 볼륨 → <see cref="AudioListener.volume"/> 로 실제 반영.
/// - 전체화면/해상도 → <see cref="Screen"/> API로 실제 반영.
/// - BGM/SFX 볼륨 → 값은 저장/노출하지만 실제 믹싱은 추후 AudioMixer 도입 시 연결(현재는 예약 필드).
/// </summary>
public static class GameSettings
{
    private const string KeyMaster = "settings.masterVolume";
    private const string KeyBgm = "settings.bgmVolume";
    private const string KeySfx = "settings.sfxVolume";
    private const string KeyFullscreen = "settings.fullscreen";
    private const string KeyResWidth = "settings.resWidth";
    private const string KeyResHeight = "settings.resHeight";

    /// <summary>설정값이 바뀔 때마다 호출(UI 동기화용).</summary>
    public static event Action Changed;

    public static float MasterVolume { get; private set; } = 1f;
    public static float BgmVolume { get; private set; } = 1f;
    public static float SfxVolume { get; private set; } = 1f;
    public static bool Fullscreen { get; private set; } = true;
    public static int ResWidth { get; private set; }
    public static int ResHeight { get; private set; }

    /// <summary>부팅 시 저장된 설정을 불러와 적용한다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void LoadAndApply()
    {
        MasterVolume = PlayerPrefs.GetFloat(KeyMaster, 1f);
        BgmVolume = PlayerPrefs.GetFloat(KeyBgm, 1f);
        SfxVolume = PlayerPrefs.GetFloat(KeySfx, 1f);
        Fullscreen = PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;
        ResWidth = PlayerPrefs.GetInt(KeyResWidth, Screen.currentResolution.width);
        ResHeight = PlayerPrefs.GetInt(KeyResHeight, Screen.currentResolution.height);

        ApplyAudio();
        // 해상도는 부팅 시 강제로 바꾸면 에디터에서 성가시므로 값만 보관하고,
        // 실제 반영은 사용자가 SETTINGS에서 명시적으로 적용할 때 수행한다.
        Changed?.Invoke();
    }

    // ───────────────────────── 볼륨 ─────────────────────────

    public static void SetMasterVolume(float v)
    {
        MasterVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KeyMaster, MasterVolume);
        ApplyAudio();
        Save();
    }

    public static void SetBgmVolume(float v)
    {
        BgmVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KeyBgm, BgmVolume);
        Save();
    }

    public static void SetSfxVolume(float v)
    {
        SfxVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KeySfx, SfxVolume);
        Save();
    }

    private static void ApplyAudio()
    {
        // 마스터 볼륨만 전역 AudioListener에 직접 반영(BGM/SFX 분리는 추후 AudioMixer로).
        AudioListener.volume = MasterVolume;
    }

    // ───────────────────────── 화면 ─────────────────────────

    public static void SetFullscreen(bool full)
    {
        Fullscreen = full;
        PlayerPrefs.SetInt(KeyFullscreen, full ? 1 : 0);
        Screen.fullScreen = full;
        Save();
    }

    public static void SetResolution(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        ResWidth = width;
        ResHeight = height;
        PlayerPrefs.SetInt(KeyResWidth, width);
        PlayerPrefs.SetInt(KeyResHeight, height);
        Screen.SetResolution(width, height, Fullscreen);
        Save();
    }

    private static void Save()
    {
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
