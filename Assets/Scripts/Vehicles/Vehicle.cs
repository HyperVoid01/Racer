using UnityEngine;

/// <summary>
/// Traffic. Cars drive in the same direction as the taxi but slower, so the taxi closes in on them.
/// <see cref="SpeedFactor"/> is how fast a car approaches relative to the taxi's speed:
/// 1 = effectively parked, lower = driving faster and pulling away.
/// While the taxi is stopped (or crashed), traffic drives on ahead.
/// </summary>
public class Vehicle : ScrollingObject
{
    [Tooltip("How quickly the car's speed reacts to changes (higher = snappier).")]
    [SerializeField] private float deceleration = 4f;
    [Tooltip("Speed traffic drives away at while the taxi is stopped.")]
    [SerializeField] private float driveAwaySpeed = 15f;

    private float _velocityZ;

    public float SpeedFactor { get; private set; }

    protected override void OnEnable()
    {
        base.OnEnable();

        VehicleSpawner spawner = VehicleSpawner.Instance;
        SpeedFactor = spawner != null
            ? Random.Range(spawner.vehicleMinSpeed, spawner.vehicleMaxSpeed)
            : Random.Range(0.3f, 1f);

        _velocityZ = TargetVelocity();
    }

    public override void Scroll(float worldDelta, float deltaTime)
    {
        float blend = 1f - Mathf.Exp(-deceleration * deltaTime);
        _velocityZ = Mathf.Lerp(_velocityZ, TargetVelocity(), blend);
        CachedTransform.position += new Vector3(0f, 0f, _velocityZ * deltaTime);
    }

    private float TargetVelocity()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return 0f;

        return gm.State == GameState.Driving
            ? -SpeedFactor * gm.GameSpeed
            : SpeedFactor * driveAwaySpeed;
    }

    /// <summary>Called by the front sensor: never drive faster than the car ahead (avoids rear-ending it).</summary>
    public void MatchSpeed(Vehicle frontVehicle)
    {
        if (frontVehicle == null || frontVehicle == this) return;
        SpeedFactor = Mathf.Max(SpeedFactor, frontVehicle.SpeedFactor);
    }

    // Kept for compatibility with the original API.
    public void Decelerate(Vehicle frontVehicle) => MatchSpeed(frontVehicle);
}
