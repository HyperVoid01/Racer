using UnityEngine;

/// <summary>
/// In-taxi radio (an upgrade). Plays the station tracks, moves to the next track when one ends, and has
/// play / pause / next / previous buttons. Pausing the game doesn't override the player's own radio pause.
/// </summary>
public class Radio : MonoBehaviour
{
    private static readonly string[] Tracks = { "Track1", "Track2", "Track3", "Track4", "Track5" };

    [Tooltip("Go to the next song when one finishes (instead of looping it).")]
    [SerializeField] private bool autoAdvance = true;

    private int _index;
    private bool _pausedByPlayer;
    private float _trackStartedAt;

    public string CurrentTrack => Tracks[_index];

    private void Start()
    {
        if (!UpgradeManager.Instance.RadioPurchased)
        {
            gameObject.SetActive(false);
            return;
        }

        foreach (string track in Tracks) AudioManager.SetLoop(track, !autoAdvance);

        _index = Random.Range(0, Tracks.Length);
        PlayTrack(_index);
    }

    private void OnDestroy()
    {
        foreach (string track in Tracks)
        {
            AudioManager.Stop(track);
            AudioManager.ResetModifiers(track);
        }
    }

    private void Update()
    {
        if (!autoAdvance || _pausedByPlayer) return;

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.IsPaused || gm.IsGameOver) return;

        // Small grace period so a clip that is still loading isn't mistaken for one that finished.
        if (Time.unscaledTime - _trackStartedAt > 1f && !AudioManager.IsPlaying(CurrentTrack))
        {
            PlayTrack(_index + 1);
        }
    }

    public void PlayTrack(int index)
    {
        AudioManager.Stop(CurrentTrack);
        _index = (index % Tracks.Length + Tracks.Length) % Tracks.Length;
        AudioManager.Play(CurrentTrack);
        _pausedByPlayer = false;
        _trackStartedAt = Time.unscaledTime;
    }

    public void StopTrack()
    {
        AudioManager.Stop(CurrentTrack);
    }

    public void Forward()
    {
        AudioManager.Play("Button Select");
        PlayTrack(_index + 1);
    }

    public void Rewind()
    {
        // The original wrapped at < 0 instead of < 1, so one press in five played nothing.
        AudioManager.Play("Button Select");
        PlayTrack(_index - 1);
    }

    /// <summary>Pause button.</summary>
    public void Pause()
    {
        AudioManager.Pause(CurrentTrack);
        _pausedByPlayer = true;
    }

    /// <summary>Play button.</summary>
    public void UnPause()
    {
        if (_pausedByPlayer)
        {
            AudioManager.UnPause(CurrentTrack);
            _pausedByPlayer = false;
        }
        else if (!AudioManager.IsPlaying(CurrentTrack))
        {
            PlayTrack(_index);
        }
    }
}
