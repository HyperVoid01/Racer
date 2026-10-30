using System;
using UnityEngine;

public enum SoundCategory
{
    /// <summary>Worked out from the name: "Track…" = Radio, "…Music" = Music, everything else = SFX.</summary>
    Auto,
    Music,
    Radio,
    Sfx
}

[Serializable]
public class Sound
{
    public string name;

    public AudioClip clip;

    [Range(0f, 1f)]
    public float volume = 1;
    [Range(0.1f, 3f)]
    public float pitch = 1;

    public bool loop = false;

    [Tooltip("Which volume slider controls this sound.")]
    public SoundCategory category = SoundCategory.Auto;

    [NonSerialized] public AudioSource source;

    // Runtime modifiers (engine pitch, cross-fades) applied on top of the authored values.
    [NonSerialized] public float volumeScale = 1f;
    [NonSerialized] public float pitchOverride = -1f;
    [NonSerialized] public bool pausedByGame;

    public SoundCategory ResolvedCategory
    {
        get
        {
            if (category != SoundCategory.Auto) return category;
            if (string.IsNullOrEmpty(name)) return SoundCategory.Sfx;
            if (name.StartsWith("Track", StringComparison.OrdinalIgnoreCase)) return SoundCategory.Radio;
            if (name.IndexOf("Music", StringComparison.OrdinalIgnoreCase) >= 0) return SoundCategory.Music;
            return SoundCategory.Sfx;
        }
    }
}
