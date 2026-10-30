using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public enum GameState
{
    Driving,
    AtStop,
    GameOver
}

public enum GameOverReason
{
    Crashed,
    PassengersGaveUp
}

/// <summary>
/// Owns the state of a single run: speed, score, passengers, pause and game over.
/// Other systems read state from here and react to its events instead of polling flags every frame.
/// </summary>
[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    /// <summary>The HUD shows speed with this offset added, so the minimum speed doesn't read as a crawl.</summary>
    public const int SpeedDisplayOffset = 20;

    [Header("Run")]
    [FormerlySerializedAs("gameSpeed")]
    [SerializeField] private float startSpeed = 1f;
    [SerializeField] private int maxPassengers = 10;
    [SerializeField] private int farePerPassenger = 10;

    [Header("Game Over")]
    [Tooltip("Time scale used for the short slow-motion beat after a crash.")]
    [SerializeField, Range(0.05f, 1f)] private float crashSlowMotionScale = 0.3f;
    [Tooltip("Real-time seconds the crash slow motion lasts.")]
    [SerializeField] private float crashSlowMotionDuration = 0.6f;
    [Tooltip("Real-time seconds between the crash and the game over screen.")]
    [SerializeField] private float gameOverScreenDelay = 1.3f;

    private float _gameSpeed;

    public GameState State { get; private set; } = GameState.Driving;
    public bool IsPaused { get; private set; }
    public bool IsGameOver => State == GameState.GameOver;
    public bool IsAtStop => State == GameState.AtStop;
    /// <summary>True while the player is in control of the taxi.</summary>
    public bool IsDriving => State == GameState.Driving && !IsPaused;

    /// <summary>World scroll speed in units per second.</summary>
    public float GameSpeed
    {
        get => _gameSpeed;
        set => _gameSpeed = Mathf.Max(0f, value);
    }

    public int Score { get; private set; }
    public int Passengers { get; private set; }
    public int MaxPassengers => maxPassengers;
    public int FarePerPassenger => farePerPassenger;

    public GameOverReason LastGameOverReason { get; private set; }
    public bool LastRunWasHighScore { get; private set; }

    /// <summary>(newScore, delta)</summary>
    public event Action<int, int> ScoreChanged;
    public event Action<int> PassengersChanged;
    public event Action<GameState> StateChanged;
    public event Action<bool> PauseChanged;
    /// <summary>Raised the moment the run ends (crash effects, audio).</summary>
    public event Action<GameOverReason> GameOverStarted;
    /// <summary>Raised after the short crash beat, when the game over screen should appear.</summary>
    public event Action GameOverScreenRequested;
    /// <summary>Raised for bumps, wall scrapes and crashes. Strength is roughly 0..1.</summary>
    public event Action<float> Impact;

    public static int ToKmh(float speed) => Mathf.RoundToInt(speed) + SpeedDisplayOffset;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Time.timeScale = 1f;
        TaxiInput.ResetVirtual();
        GameSpeed = startSpeed;

        // Systems that only exist at runtime. Adding them here means the scene needs no extra setup.
        if (!TryGetComponent(out WorldScroller _)) gameObject.AddComponent<WorldScroller>();
    }

    private void Start()
    {
        AudioManager.StopAll();

        Camera cam = Camera.main;
        if (cam != null && !cam.TryGetComponent(out CameraFeel _))
        {
            cam.gameObject.AddComponent<CameraFeel>();
        }

        // Road pieces, potholes and traffic are now despawned by WorldScroller. The old "Destroyer" blocks
        // would only generate needless physics contacts with the road, so switch them off.
        foreach (GameObject destroyer in GameObject.FindGameObjectsWithTag("Destroyer"))
        {
            destroyer.SetActive(false);
        }

        // Stop phones dimming the screen mid-run.
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }

    private void Update()
    {
        if (TaxiInput.ConsumePausePress() && !IsGameOver)
        {
            SetPaused(!IsPaused);
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        // Backgrounding the app on mobile should never cost the player their run.
        if (pauseStatus && !IsGameOver) SetPaused(true);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        Instance = null;
        Time.timeScale = 1f;
        Screen.sleepTimeout = SleepTimeout.SystemSetting;
    }

    // ---------------------------------------------------------------- Pause

    public void SetPaused(bool paused)
    {
        if (IsGameOver || paused == IsPaused) return;

        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;

        if (paused) AudioManager.PauseAll();
        else AudioManager.UnPauseAll();

        PauseChanged?.Invoke(paused);
    }

    // ---------------------------------------------------------------- Score & passengers

    public void AddScore(int delta)
    {
        if (IsGameOver || delta == 0) return;

        int newScore = Mathf.Max(0, Score + delta);
        int applied = newScore - Score;
        if (applied == 0) return;

        Score = newScore;
        ScoreChanged?.Invoke(Score, applied);
    }

    public bool TryAddPassenger()
    {
        if (Passengers >= maxPassengers) return false;

        Passengers++;
        PassengersChanged?.Invoke(Passengers);
        return true;
    }

    public bool TryRemovePassenger()
    {
        if (Passengers <= 0) return false;

        Passengers--;
        PassengersChanged?.Invoke(Passengers);
        return true;
    }

    // ---------------------------------------------------------------- State

    public void EnterStop()
    {
        if (State != GameState.Driving) return;
        SetState(GameState.AtStop);
    }

    public void LeaveStop()
    {
        if (State != GameState.AtStop) return;
        SetState(GameState.Driving);
    }

    public void ReportImpact(float strength)
    {
        Impact?.Invoke(strength);
    }

    public void EndGame(GameOverReason reason)
    {
        if (IsGameOver) return;

        if (IsPaused)
        {
            IsPaused = false;
            Time.timeScale = 1f;
            PauseChanged?.Invoke(false);
        }

        LastGameOverReason = reason;
        SetState(GameState.GameOver);

        // Bank the fare once. The original added it to the wallet and then compared a zeroed score
        // to the high score, so the high score never updated.
        float distance = WorldScroller.Instance != null ? WorldScroller.Instance.Distance : 0f;
        LastRunWasHighScore = Statistics.Instance.RecordRun(Score, distance);

        AudioManager.StopAll();
        if (reason == GameOverReason.Crashed)
        {
            AudioManager.Play("Taxi crash");
            AudioManager.Play("Mama Crash");
            ReportImpact(1f);
            Haptics.Heavy();
        }
        else
        {
            AudioManager.Play("Mama Crash");
            Haptics.Medium();
        }

        GameOverStarted?.Invoke(reason);
        StartCoroutine(GameOverSequence(reason));
    }

    private void SetState(GameState state)
    {
        if (State == state) return;
        State = state;
        StateChanged?.Invoke(state);
    }

    private IEnumerator GameOverSequence(GameOverReason reason)
    {
        if (reason == GameOverReason.Crashed)
        {
            Time.timeScale = crashSlowMotionScale;
            yield return new WaitForSecondsRealtime(crashSlowMotionDuration);
            Time.timeScale = 1f;
        }

        float remaining = Mathf.Max(0f, gameOverScreenDelay - (reason == GameOverReason.Crashed ? crashSlowMotionDuration : 0f));
        yield return new WaitForSecondsRealtime(remaining);

        GameOverScreenRequested?.Invoke();
    }
}
