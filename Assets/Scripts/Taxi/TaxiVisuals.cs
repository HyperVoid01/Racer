using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Spawns the taxi model that matches the player's rims upgrade (base / rims / rims + spoiler).
/// Used in both the game and the upgrade showroom.
/// </summary>
public class TaxiVisuals : MonoBehaviour
{
    public static TaxiVisuals Instance { get; private set; }

    [Tooltip("Index = rims upgrade level.")]
    [SerializeField] private GameObject[] taxiPrefabs;
    [SerializeField] private float popDuration = 0.25f;

    private GameObject _currentModel;
    private Coroutine _pop;

    /// <summary>The spawned model, or null before the first refresh.</summary>
    public GameObject CurrentModel => _currentModel;

    /// <summary>Raised right after a new model is spawned, before any pop animation scales it.</summary>
    public event Action ModelChanged;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        Refresh(false);
    }

    /// <summary>Kept for compatibility: rebuilds the model with a small "pop".</summary>
    public void UpdateTaxiVisual() => Refresh(true);

    public void Refresh(bool animate)
    {
        if (taxiPrefabs == null || taxiPrefabs.Length == 0) return;

        if (_pop != null)
        {
            StopCoroutine(_pop);
            _pop = null;
        }

        if (_currentModel != null) Destroy(_currentModel);

        int index = Mathf.Clamp(UpgradeManager.Instance.RimsLevel, 0, taxiPrefabs.Length - 1);
        _currentModel = Instantiate(taxiPrefabs[index], transform, false);

        // Listeners (e.g. TaxiWheels) read the model at its real scale, so notify before the pop starts.
        ModelChanged?.Invoke();

        if (animate && isActiveAndEnabled) _pop = StartCoroutine(Pop(_currentModel.transform));
    }

    private IEnumerator Pop(Transform model)
    {
        Vector3 baseScale = model.localScale;
        float t = 0f;
        while (t < popDuration && model != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / popDuration);
            // Quick overshoot: 0.85 -> ~1.08 -> 1.
            float scale = 1f + Mathf.Sin(k * Mathf.PI) * 0.08f - (1f - k) * 0.15f;
            model.localScale = baseScale * scale;
            yield return null;
        }
        if (model != null) model.localScale = baseScale;
        _pop = null;
    }
}
