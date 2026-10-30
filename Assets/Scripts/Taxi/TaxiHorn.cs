using System;
using UnityEngine;

/// <summary>
/// Makes the horn matter. Hooting at the car ahead in your lane makes it change to a free lane
/// (pays a small bonus and shows a "Move!" popup) or, if no lane is free, speed up.
/// The push has a cooldown; the horn sound itself always plays.
///
/// Added to the taxi automatically by <see cref="TaxiController"/>, so no scene setup is needed.
/// To tweak values in the Inspector, add this component to the taxi yourself.
/// </summary>
public class TaxiHorn : MonoBehaviour
{
    [Header("Cooldown")]
    [Tooltip("Seconds before the horn can push another car.")]
    [SerializeField] private float cooldown = 1.75f;

    [Header("Who hears it")]
    [Tooltip("How far ahead (world units) a car can be and still react.")]
    [SerializeField] private float reach = 45f;
    [Tooltip("Cars closer than this can't react in time and are ignored.")]
    [SerializeField] private float minDistance = 6f;
    [Tooltip("Half width of the area in front of the taxi that counts as 'your lane'.")]
    [SerializeField] private float laneHalfWidth = 1.4f;

    [Header("Reaction")]
    [Tooltip("Seconds a car takes to move across to the next lane.")]
    [SerializeField] private float laneChangeDuration = 0.55f;
    [Tooltip("When no lane is free, the car's cruise speed goes up by this much (km/h).")]
    [SerializeField] private float speedUpKmh = 15f;

    [Header("Reward")]
    [Tooltip("Fare bonus (R) for hooting a car out of your lane.")]
    [SerializeField] private int laneClearedBonus = 5;
    [SerializeField] private string laneClearedMessage = "Move!";

    [Header("Touch button (optional)")]
    [Tooltip("HOOT button to grey out during the cooldown. Left empty, a TouchButton whose name " +
             "contains \"Hoot\" or \"Horn\" is found automatically.")]
    [SerializeField] private TouchButton hornButton;

    private const int MaxButtonSearches = 20;
    private const float ButtonSearchInterval = 0.5f;

    private readonly Collider[] _hits = new Collider[16];
    private float _cooldownLeft;
    private float _nextButtonSearch;
    private int _buttonSearches;

    /// <summary>True when the next hoot will push a car.</summary>
    public bool IsReady => _cooldownLeft <= 0f;

    /// <summary>0 right after hooting, 1 when ready again.</summary>
    public float Readiness => cooldown > 0f ? 1f - Mathf.Clamp01(_cooldownLeft / cooldown) : 1f;

    /// <summary>Raised when a hoot clears a car out of the taxi's lane.</summary>
    public event Action<Vehicle> LaneCleared;

    private void Update()
    {
        FindHornButton();

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.IsPaused || _cooldownLeft <= 0f) return;

        _cooldownLeft = Mathf.Max(0f, _cooldownLeft - Time.deltaTime);
        if (hornButton != null) hornButton.SetReadiness(Readiness);
    }

    /// <summary>Plays the horn and, if off cooldown, pushes the car ahead.</summary>
    public void Hoot()
    {
        AudioManager.Play("Taxi Hooting");

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Driving || !IsReady) return;

        Vehicle target = FindVehicleAhead();
        if (target == null) return; // Nobody to hoot at: no cooldown, so the next hoot isn't wasted.

        _cooldownLeft = cooldown;
        if (hornButton != null) hornButton.SetReadiness(0f);

        VehicleSpawner spawner = VehicleSpawner.Instance;
        if (spawner != null && spawner.TryGetFreeLaneBeside(target, out float laneX))
        {
            target.ChangeLane(laneX, laneChangeDuration);

            if (laneClearedBonus > 0) gm.AddScore(laneClearedBonus);
            if (UIController.Instance != null) UIController.Instance.ShowMessage(laneClearedMessage);
            Haptics.Light();
            LaneCleared?.Invoke(target);
        }
        else
        {
            target.SpeedUp(speedUpKmh); // 1 world unit/s = 1 km/h on the HUD
        }
    }

    /// <summary>Nearest car in the taxi's lane between <see cref="minDistance"/> and <see cref="reach"/> ahead.</summary>
    private Vehicle FindVehicleAhead()
    {
        // Kinematic cars are moved through their transforms; make sure the query sees where they are now.
        Physics.SyncTransforms();

        Vector3 taxi = transform.position;
        float depth = Mathf.Max(1f, reach - minDistance);
        var center = new Vector3(taxi.x, taxi.y, taxi.z + minDistance + depth * 0.5f);
        var halfExtents = new Vector3(laneHalfWidth, 3f, depth * 0.5f);

        int count = Physics.OverlapBoxNonAlloc(center, halfExtents, _hits, Quaternion.identity,
            Physics.AllLayers, QueryTriggerInteraction.Collide);

        Vehicle nearest = null;
        float nearestZ = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (!_hits[i].CompareTag("Vehicle")) continue;

            Vehicle vehicle = _hits[i].GetComponentInParent<Vehicle>();
            if (vehicle == null || vehicle.IsChangingLane) continue;

            float z = vehicle.transform.position.z;
            if (z < nearestZ)
            {
                nearestZ = z;
                nearest = vehicle;
            }
        }
        return nearest;
    }

    /// <summary>Touch controls can be created after the taxi, so look for the button for a few seconds.</summary>
    private void FindHornButton()
    {
        if (hornButton != null || _buttonSearches >= MaxButtonSearches || Time.unscaledTime < _nextButtonSearch) return;

        _nextButtonSearch = Time.unscaledTime + ButtonSearchInterval;
        _buttonSearches++;

#if UNITY_2023_1_OR_NEWER
        TouchButton[] buttons = FindObjectsByType<TouchButton>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        TouchButton[] buttons = FindObjectsOfType<TouchButton>(true);
#endif
        foreach (TouchButton button in buttons)
        {
            string n = button.name;
            if (n.IndexOf("hoot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("horn", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                hornButton = button;
                hornButton.SetReadiness(Readiness);
                return;
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 taxi = transform.position;
        float depth = Mathf.Max(1f, reach - minDistance);
        Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.8f);
        Gizmos.DrawWireCube(new Vector3(taxi.x, taxi.y, taxi.z + minDistance + depth * 0.5f),
            new Vector3(laneHalfWidth * 2f, 6f, depth));
    }
#endif
}