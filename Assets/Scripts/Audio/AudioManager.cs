using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central audio player. Sounds are set up in the inspector on the MainMenu scene object and looked up by name.
/// The static API is safe to call from any scene: if the manager isn't loaded (e.g. GameLoop played directly
/// in the editor) calls simply do nothing.
/// Volume = authored volume x category slider (Music / Radio / SFX) x runtime scale.
/// </summary>
public class AudioManager : MonoBehaviour
{
    private static AudioManager _instance;
    private static readonly HashSet<string> WarnedMissing = new HashSet<string>();

    public Sound[] sounds;

    private readonly Dictionary<string, Sound> _lookup = new Dictionary<string, Sound>();

    public static bool Exists => _instance != null;

    // Kept for compatibility with older code; prefer the static API.
    public static AudioManager Instance => _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        foreach (Sound s in sounds)
        {
            if (s == null || string.IsNullOrEmpty(s.name)) continue;

            s.source = gameObject.AddComponent<AudioSource>();
            s.source.playOnAwake = false;
            s.source.clip = s.clip;
            s.source.pitch = s.pitch;
            s.source.loop = s.loop;
            s.source.spatialBlend = 0f;

            if (!_lookup.ContainsKey(s.name)) _lookup.Add(s.name, s);
            else Debug.LogWarning($"[AudioManager] Duplicate sound name '{s.name}'.", this);
        }

        ApplyAllVolumes();
    }

    private void Start()
    {
        Play("Main Menu Music");
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // ---------------------------------------------------------------- Playback

    public static void Play(string name)
    {
        if (TryGet(name, out Sound s))
        {
            s.pausedByGame = false;
            s.source.Play();
        }
    }

    public static void Stop(string name)
    {
        if (TryGet(name, out Sound s))
        {
            s.pausedByGame = false;
            s.source.Stop();
        }
    }

    public static void Pause(string name)
    {
        if (TryGet(name, out Sound s)) s.source.Pause();
    }

    public static void UnPause(string name)
    {
        if (TryGet(name, out Sound s)) s.source.UnPause();
    }

    public static bool IsPlaying(string name) => TryGet(name, out Sound s) && s.source.isPlaying;

    public static void StopAll()
    {
        if (_instance == null) return;
        foreach (Sound s in _instance._lookup.Values)
        {
            s.pausedByGame = false;
            s.source.Stop();
        }
    }

    /// <summary>Pauses everything that is currently playing and remembers it, so the matching
    /// <see cref="UnPauseAll"/> doesn't resume sounds the player had paused themselves (e.g. the radio).</summary>
    public static void PauseAll()
    {
        if (_instance == null) return;
        foreach (Sound s in _instance._lookup.Values)
        {
            if (!s.source.isPlaying) continue;
            s.source.Pause();
            s.pausedByGame = true;
        }
    }

    public static void UnPauseAll()
    {
        if (_instance == null) return;
        foreach (Sound s in _instance._lookup.Values)
        {
            if (!s.pausedByGame) continue;
            s.pausedByGame = false;
            s.source.UnPause();
        }
    }

    // ---------------------------------------------------------------- Runtime modifiers

    public static void SetPitch(string name, float pitch)
    {
        if (!TryGet(name, out Sound s)) return;
        s.pitchOverride = pitch;
        s.source.pitch = pitch;
    }

    public static void SetVolumeScale(string name, float scale)
    {
        if (!TryGet(name, out Sound s)) return;
        s.volumeScale = Mathf.Clamp01(scale);
        ApplyVolume(s);
    }

    public static void SetLoop(string name, bool loop)
    {
        if (TryGet(name, out Sound s)) s.source.loop = loop;
    }

    public static void ResetModifiers(string name)
    {
        if (!TryGet(name, out Sound s)) return;
        s.volumeScale = 1f;
        s.pitchOverride = -1f;
        s.source.pitch = s.pitch;
        s.source.loop = s.loop;
        ApplyVolume(s);
    }

    // ---------------------------------------------------------------- Volume settings

    public static float GetCategoryVolume(SoundCategory category)
    {
        SaveData data = SaveSystem.Data;
        return category switch
        {
            SoundCategory.Music => data.musicVolume,
            SoundCategory.Radio => data.radioVolume,
            _ => data.sfxVolume
        };
    }

    public static void SetCategoryVolume(SoundCategory category, float volume)
    {
        volume = Mathf.Clamp01(volume);
        SaveData data = SaveSystem.Data;
        switch (category)
        {
            case SoundCategory.Music: data.musicVolume = volume; break;
            case SoundCategory.Radio: data.radioVolume = volume; break;
            default: data.sfxVolume = volume; break;
        }
        SaveSystem.MarkDirty();
        ApplyAllVolumes();
    }

    private static void ApplyAllVolumes()
    {
        if (_instance == null) return;
        foreach (Sound s in _instance._lookup.Values) ApplyVolume(s);
    }

    private static void ApplyVolume(Sound s)
    {
        if (s.source == null) return;
        s.source.volume = s.volume * GetCategoryVolume(s.ResolvedCategory) * s.volumeScale;
    }

    // ---------------------------------------------------------------- Lookup

    private static bool TryGet(string name, out Sound sound)
    {
        sound = null;
        if (_instance == null || string.IsNullOrEmpty(name)) return false;

        if (_instance._lookup.TryGetValue(name, out sound) && sound.source != null) return true;

        if (WarnedMissing.Add(name)) Debug.LogWarning($"[AudioManager] No sound named '{name}'.", _instance);
        return false;
    }
}
