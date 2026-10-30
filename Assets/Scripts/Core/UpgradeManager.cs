using System;
using UnityEngine;

public enum UpgradeType
{
    Engine,
    Rims,
    Radio
}

/// <summary>
/// Upgrade tuning and purchase logic. Only the upgrade *levels* are saved; every stat modifier and
/// price is derived from the level, so tuning changes apply to existing saves automatically.
/// Code defaults match the values set on the UpgradeMenu scene object, so an instance created on
/// demand (when playing GameLoop directly in the editor) behaves the same.
/// </summary>
public class UpgradeManager : MonoBehaviour
{
    private static UpgradeManager _instance;

    public static UpgradeManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject(nameof(UpgradeManager));
                _instance = go.AddComponent<UpgradeManager>();
            }
            return _instance;
        }
    }

    [Header("Stat gain per level")]
    public float accelerationModifierIncrement = 0.1f;
    public float decelerationIncrement = 0.15f;
    public float topSpeedIncrement = 7f;
    public float brakingIncrement = 2.5f;
    public float strafeSpeedIncrement = 1f;

    [Header("Upgrade Limit")]
    public int engineUpgradeLimit = 3;
    public int rimsUpgradeLimit = 2;

    [Header("Base Costs")]
    public int engineCost = 100;
    public int rimsCost = 80;
    public int radioCost = 120;

    [Header("Cost growth per level")]
    public float costMultiplier = 1.5f;

    [Header("Testing")]
    [Tooltip("Every upgrade is affordable and buying never spends cash. " +
             "Only works in the editor and development builds; ignored in release builds.")]
    [SerializeField] private bool unlimitedMoney;

    /// <summary>
    /// True when the testing toggle is on. Always false in release builds, so it can't ship enabled.
    /// The field itself stays outside the #if so the serialized layout is identical in every build.
    /// </summary>
    public bool UnlimitedMoney
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return unlimitedMoney;
#else
            return false;
#endif
        }
        set => unlimitedMoney = value;
    }

    /// <summary>Check this (not Instance) from OnDestroy, so no object is created while the app is closing.</summary>
    public static bool Exists => _instance != null;

    /// <summary>Raised after any successful purchase.</summary>
    public event Action<UpgradeType> Upgraded;

    public int EngineLevel => SaveSystem.Data.engineLevel;
    public int RimsLevel => SaveSystem.Data.rimsLevel;
    public bool RadioPurchased => SaveSystem.Data.radioPurchased;

    // Engine improves acceleration, coasting and top speed. Rims improve braking and handling.
    public float AccelerationModifier => EngineLevel * accelerationModifierIncrement;
    public float DecelerationModifier => EngineLevel * decelerationIncrement;
    public float TopSpeedModifier => EngineLevel * topSpeedIncrement;
    public float BrakingModifier => RimsLevel * brakingIncrement;
    public float StrafeSpeedModifier => RimsLevel * strafeSpeedIncrement;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        if (UnlimitedMoney)
        {
            Debug.LogWarning("[UpgradeManager] Unlimited money is ON (testing only).");
        }
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    public int GetLevel(UpgradeType type) => type switch
    {
        UpgradeType.Engine => EngineLevel,
        UpgradeType.Rims => RimsLevel,
        UpgradeType.Radio => RadioPurchased ? 1 : 0,
        _ => 0
    };

    public int GetMaxLevel(UpgradeType type) => type switch
    {
        UpgradeType.Engine => engineUpgradeLimit,
        UpgradeType.Rims => rimsUpgradeLimit,
        UpgradeType.Radio => 1,
        _ => 0
    };

    public bool IsMaxed(UpgradeType type) => GetLevel(type) >= GetMaxLevel(type);

    /// <summary>Price of the next level. Compounds and truncates per level, matching the original pricing.</summary>
    public int GetCost(UpgradeType type)
    {
        if (type == UpgradeType.Radio) return radioCost;

        int cost = type == UpgradeType.Engine ? engineCost : rimsCost;
        int level = GetLevel(type);
        for (int i = 0; i < level; i++)
        {
            cost = (int)(cost * costMultiplier);
        }
        return cost;
    }

    public bool CanAfford(UpgradeType type)
    {
        if (IsMaxed(type)) return false;
        return UnlimitedMoney || Statistics.Instance.CanAfford(GetCost(type));
    }

    public bool TryPurchase(UpgradeType type)
    {
        if (IsMaxed(type)) return false;
        if (!UnlimitedMoney && !Statistics.Instance.TrySpend(GetCost(type))) return false;

        SaveData data = SaveSystem.Data;
        switch (type)
        {
            case UpgradeType.Engine: data.engineLevel++; break;
            case UpgradeType.Rims: data.rimsLevel++; break;
            case UpgradeType.Radio: data.radioPurchased = true; break;
        }

        SaveSystem.Save();
        Upgraded?.Invoke(type);
        return true;
    }

    // ── Testing ────────────────────────────────────────────────────────────
    // Right-click the UpgradeManager component header (or its ⋮ menu) in the Inspector to run these.

    [ContextMenu("Testing/Reset Progress (keep settings)")]
    private void ResetProgressFromInspector()
    {
        SaveSystem.ResetProgress();
        Debug.Log("[UpgradeManager] Progress reset: cash, high score, runs and upgrades cleared. Settings kept.");
        if (Application.isPlaying)
        {
            Debug.Log("[UpgradeManager] Re-enter the garage or restart Play mode to see the reset reflected in the UI.");
        }
    }

    [ContextMenu("Testing/Delete Entire Save (incl. settings)")]
    private void DeleteSaveFromInspector()
    {
        PlayerPrefs.DeleteKey("MzansiRush.Save");
        PlayerPrefs.Save();
        SaveSystem.Load(); // no key any more, so this starts a fresh SaveData
        Debug.Log("[UpgradeManager] Save deleted. Everything, including volume and haptics settings, is back to default.");
    }

    // Kept for compatibility with any existing button bindings.
    public void UpgradeEngine() => TryPurchase(UpgradeType.Engine);
    public void UpgradeRims() => TryPurchase(UpgradeType.Rims);
    public void UpgradeRadio() => TryPurchase(UpgradeType.Radio);
}