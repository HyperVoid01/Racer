using System;
using UnityEngine;

/// <summary>
/// Delivery timer. Starts when passengers board, stops at the next taxi stop.
/// Once it runs out, the fare drops every <see cref="latePenaltyInterval"/> seconds,
/// and after <see cref="maxPenaltyTime"/> seconds late the passengers give up (game over).
/// </summary>
public class Timer : MonoBehaviour
{
    public static Timer Instance { get; private set; }

    [SerializeField] private float timeLimit = 20f;
    [SerializeField] private int latePenaltyFee = 2;
    [SerializeField] private float latePenaltyInterval = 1f;
    [SerializeField] private float maxPenaltyTime = 20f;
    [Tooltip("Seconds remaining at which the HUD starts warning the player.")]
    [SerializeField] private float warningTime = 5f;

    private float _penaltyAccumulator;

    public float TimeRemaining { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsLate => IsActive && TimeRemaining < 0f;
    public bool IsWarning => IsActive && TimeRemaining >= 0f && TimeRemaining <= warningTime;
    public float TimeLimit => timeLimit;
    public float MaxPenaltyTime => maxPenaltyTime;

    /// <summary>Raised each time a late fee is charged.</summary>
    public event Action<int> LateFeeCharged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        GameManager.Instance.GameOverStarted += OnGameOver;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.GameOverStarted -= OnGameOver;
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!IsActive) return;

        TimeRemaining -= Time.deltaTime;
        if (TimeRemaining >= 0f) return;

        _penaltyAccumulator += Time.deltaTime;
        while (_penaltyAccumulator >= latePenaltyInterval)
        {
            _penaltyAccumulator -= latePenaltyInterval;
            GameManager.Instance.AddScore(-latePenaltyFee);
            LateFeeCharged?.Invoke(latePenaltyFee);
        }

        if (TimeRemaining <= -maxPenaltyTime)
        {
            StopTimer();
            GameManager.Instance.EndGame(GameOverReason.PassengersGaveUp);
        }
    }

    public void StartTimer()
    {
        TimeRemaining = timeLimit;
        // The first fee is charged the moment the timer runs out, like the original.
        _penaltyAccumulator = latePenaltyInterval;
        IsActive = true;
    }

    public void StopTimer()
    {
        IsActive = false;
    }

    private void OnGameOver(GameOverReason reason) => StopTimer();
}
