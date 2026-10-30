using System;
using UnityEngine;

/// <summary>
/// Everything that should survive closing the game.
/// Add new fields with sensible defaults; JsonUtility leaves missing fields at their default
/// so older saves keep loading.
/// </summary>
[Serializable]
public class SaveData
{
    public int version = 1;

    [Header("Progress")]
    public int currency;
    public int highScore;
    public int runsPlayed;
    public float bestDistance;

    [Header("Upgrades")]
    public int engineLevel;
    public int rimsLevel;
    public bool radioPurchased;

    [Header("Settings")]
    public float musicVolume = 1f;
    public float radioVolume = 1f;
    public float sfxVolume = 1f;
    public bool hapticsEnabled = true;
}

/// <summary>
/// Tiny PlayerPrefs-backed save system. PlayerPrefs works on every platform Unity ships to
/// (including Android/iOS/WebGL), so we avoid file IO permission issues on mobile.
/// </summary>
public static class SaveSystem
{
    private const string SaveKey = "MzansiRush.Save";

    private static SaveData _data;
    private static bool _dirty;

    public static SaveData Data
    {
        get
        {
            if (_data == null) Load();
            return _data;
        }
    }

    public static void Load()
    {
        _data = null;

        if (PlayerPrefs.HasKey(SaveKey))
        {
            try
            {
                _data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Save data was unreadable, starting fresh. {e.Message}");
            }
        }

        _data ??= new SaveData();
        _dirty = false;
    }

    /// <summary>Mark data as changed; it is written on the next <see cref="Flush"/> (scene change, app pause/quit).</summary>
    public static void MarkDirty() => _dirty = true;

    /// <summary>Write immediately. Use for important changes such as money and purchases.</summary>
    public static void Save()
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Data));
        PlayerPrefs.Save();
        _dirty = false;
    }

    public static void Flush()
    {
        if (_dirty) Save();
    }

    public static void ResetProgress()
    {
        // Keep the player's settings, wipe progress.
        SaveData old = Data;
        _data = new SaveData
        {
            musicVolume = old.musicVolume,
            radioVolume = old.radioVolume,
            sfxVolume = old.sfxVolume,
            hapticsEnabled = old.hapticsEnabled
        };
        Save();
    }
}
