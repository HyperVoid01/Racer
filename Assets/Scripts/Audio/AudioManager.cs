using UnityEngine;
using System;

public class AudioManager : MonoBehaviour
{
    public Sound[] sounds;
    
    // Settings
    public float menuVolume = 1.0f;
    public float radioVolume = 1.0f;
    public float sfxVolume = 1.0f;

    public static AudioManager Instance;
    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        foreach (Sound s in sounds)
        {
            s.source = gameObject.AddComponent<AudioSource>();
            s.source.clip = s.clip;
            
            s.source.volume = s.volume;
            s.source.pitch = s.pitch;
            s.source.loop = s.loop;
        }
    }

    void Start()
    {
        // Plays on game start
        Play("Main Menu Music");
    }

    public void Play(string name)
    {
        Sound s = Array.Find(sounds, sound => sound.name == name);
        s.source.Play();
    }

    public void Stop(string name)
    {
        Sound s = Array.Find(sounds, sound => sound.name == name);
        s.source.Stop();
    }

    public void Pause(string name)
    {
        Sound s = Array.Find(sounds, sound => sound.name == name);
        s.source.Pause();
    }

    public void UnPause(string name)
    {
        Sound s = Array.Find(sounds, sound => sound.name == name);
        s.source.UnPause();
    }

    public void StopAll()
    {
        foreach (Sound s in sounds)
        {
            s.source.Stop();
        }
    }

    public void PauseAll()
    {
        foreach (Sound s in sounds)
        {
            s.source.Pause();
        }
    }

    public void UnPauseAll()
    {
        foreach (Sound s in sounds)
        {
            s.source.UnPause();
        }
    }

    public void ChangeVolume(string soundName, float volume)
    {
        Sound s = Array.Find(sounds, sound => sound.name == soundName);
        
        s.source.volume = volume;
    }
}
