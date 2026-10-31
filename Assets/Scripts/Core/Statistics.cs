using System;
using UnityEngine;

/// <summary>
/// The player's wallet and records. Values live in <see cref="SaveSystem"/> so they persist between sessions.
/// The component in the MainMenu scene is optional: if a scene is played directly in the editor,
/// an instance is created on demand.
/// </summary>
public class Statistics : MonoBehaviour
{
    private static Statistics _instance;

    public static Statistics Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject(nameof(Statistics));
                _instance = go.AddComponent<Statistics>();
            }
            return _instance;
        }
    }

    /// <summary>Check this (not Instance) from OnDestroy, so no object is created while the app is closing.</summary>
    public static bool Exists => _instance != null;

    public event Action<int> CurrencyChanged;

    public int Currency => SaveSystem.Data.currency;
    public int HighScore => SaveSystem.Data.highScore;
    public int RunsPlayed => SaveSystem.Data.runsPlayed;
    public float BestDistance => SaveSystem.Data.bestDistance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        SaveSystem.ProgressReset += OnProgressReset;
    }

    private void OnDestroy()
    {
        if (_instance != this) return;
        SaveSystem.ProgressReset -= OnProgressReset;
        _instance = null;
    }

    private void OnProgressReset() => CurrencyChanged?.Invoke(Currency);

    public bool CanAfford(int cost) => Currency >= cost;

    public bool TrySpend(int cost)
    {
        if (cost < 0 || !CanAfford(cost)) return false;

        SaveSystem.Data.currency -= cost;
        SaveSystem.Save();
        CurrencyChanged?.Invoke(Currency);
        return true;
    }

    /// <summary>Banks a run's fare and updates records. Returns true when the score is a new best.</summary>
    public bool RecordRun(int score, float distance)
    {
        SaveData data = SaveSystem.Data;

        data.currency += Mathf.Max(0, score);
        data.runsPlayed++;
        data.bestDistance = Mathf.Max(data.bestDistance, distance);

        bool isNewBest = score > data.highScore;
        if (isNewBest) data.highScore = score;

        SaveSystem.Save();
        CurrencyChanged?.Invoke(Currency);
        return isNewBest;
    }
}