using UnityEngine;

/// <summary>
/// Traffic with real road speeds. Every car cruises at its own speed (world units per second, which is the
/// HUD's km/h minus <see cref="GameManager.SpeedDisplayOffset"/>). The taxi stays at Z = 0 and the world
/// scrolls past at the taxi's speed, so a car moves towards the camera at (taxi speed - car speed):
/// you overtake slower cars, faster cars pull away, and at a taxi stop traffic simply keeps driving.
///
/// A car never drives faster than a slower car close ahead of it in its lane, and goes back to its own
/// speed once that car leaves the lane. Cars behind the taxi never drive faster than the taxi, so the
/// player can't be rear-ended by a car they just overtook.
/// Cars react to the taxi's horn (see <see cref="TaxiHorn"/>) by changing lanes or speeding up.
/// </summary>
public class Vehicle : ScrollingObject
{
    [Tooltip("How quickly the car reaches a new speed (higher = snappier).")]
    [SerializeField] private float speedResponse = 3f;

    [Header("Following")]
    [Tooltip("A car counts as 'in the same lane' as another when their X positions are closer than this.")]
    [SerializeField] private float sameLaneTolerance = 1.5f;
    [Tooltip("Only match the speed of the car ahead while it is closer than this.")]
    [SerializeField] private float followDistance = 30f;

    [Header("Lane change (horn reaction)")]
    [Tooltip("How far the car turns its nose (degrees) while changing lanes.")]
    [SerializeField] private float laneChangeYaw = 12f;

    private const float DefaultMinSpeed = 5f;
    private const float DefaultMaxSpeed = 30f;

    private float _cruiseSpeed;
    private float _currentSpeed;
    private Vehicle _leader;

    private bool _changingLane;
    private float _laneFromX;
    private float _laneToX;
    private float _laneTimer;
    private float _laneDuration;

    /// <summary>The speed this car drives at when nothing is in its way (world units/s).</summary>
    public float CruiseSpeed => _cruiseSpeed;

    /// <summary>Actual road speed right now (world units/s). Add <see cref="GameManager.SpeedDisplayOffset"/> for HUD km/h.</summary>
    public float CurrentSpeed => _currentSpeed;

    /// <summary>The speed the car is heading towards: its cruise speed, limited by a slower car ahead or the taxi.</summary>
    public float TargetSpeed
    {
        get
        {
            float target = _cruiseSpeed;
            if (IsLimitedBy(_leader)) target = Mathf.Min(target, _leader.TargetSpeed, _leader.CurrentSpeed);
            if (IsBehindTaxi()) target = Mathf.Min(target, TaxiSpeed());
            return target;
        }
    }

    /// <summary>
    /// Kept for compatibility with the old relative model: how fast the car approaches as a fraction of the
    /// taxi's speed (1 = parked, 0 = same speed as the taxi).
    /// </summary>
    public float SpeedFactor
    {
        get
        {
            float taxi = TaxiSpeed();
            return taxi > 0.01f ? 1f - _currentSpeed / taxi : 0f;
        }
    }

    /// <summary>True while the car is moving across to another lane.</summary>
    public bool IsChangingLane => _changingLane;

    protected override void OnEnable()
    {
        base.OnEnable();

        // Pooled cars are re-used: forget the previous life's leader and lane change.
        _leader = null;
        _changingLane = false;
        CachedTransform.rotation = Quaternion.identity;

        VehicleSpawner spawner = VehicleSpawner.Instance;
        _cruiseSpeed = spawner != null
            ? spawner.PickCruiseSpeed()
            : Random.Range(DefaultMinSpeed, DefaultMaxSpeed);

        _currentSpeed = _cruiseSpeed;
    }

    public override void Scroll(float worldDelta, float deltaTime)
    {
        // Forget the car ahead once it has left our lane or been despawned. It's kept while merely far
        // away: the front sensor only reports it once, so we need it again if we catch up.
        if (_leader != null && !IsAheadInMyLane(_leader)) _leader = null;

        float target = TargetSpeed;
        if (IsBehindTaxi())
        {
            // Snap rather than ease, so a car can't keep momentum and roll into a braking taxi.
            _currentSpeed = Mathf.Min(_currentSpeed, target);
        }
        _currentSpeed = Mathf.Lerp(_currentSpeed, target, 1f - Mathf.Exp(-speedResponse * deltaTime));

        Vector3 position = CachedTransform.position;
        position.z += (_currentSpeed * deltaTime) - worldDelta;

        if (_changingLane) position.x = UpdateLaneChange(deltaTime);

        CachedTransform.position = position;
    }

    private static float TaxiSpeed()
    {
        GameManager gm = GameManager.Instance;
        return gm != null ? gm.GameSpeed : 0f;
    }

    private bool IsBehindTaxi()
    {
        TaxiController taxi = TaxiController.Instance;
        float taxiZ = taxi != null ? taxi.transform.position.z : 0f;
        return CachedTransform.position.z < taxiZ;
    }

    // ---------------------------------------------------------------- Following

    /// <summary>
    /// Called by the front sensor when it sees a car ahead. The car follows it only while it's actually
    /// ahead, in the same lane and close; after that it returns to its own cruise speed.
    /// </summary>
    public void MatchSpeed(Vehicle frontVehicle)
    {
        if (frontVehicle == null || frontVehicle == this || frontVehicle._leader == this) return;
        if (!IsAheadInMyLane(frontVehicle)) return;

        // If we already track a car in this lane, keep whichever is closer.
        if (_leader != null && _leader != frontVehicle && IsAheadInMyLane(_leader) &&
            _leader.CachedTransform.position.z < frontVehicle.CachedTransform.position.z)
        {
            return;
        }

        _leader = frontVehicle;
    }

    // Kept for compatibility with the original API.
    public void Decelerate(Vehicle frontVehicle) => MatchSpeed(frontVehicle);

    /// <summary>The other car is active, in front of us and in our lane (at any distance).</summary>
    private bool IsAheadInMyLane(Vehicle other)
    {
        if (other == null || !other.isActiveAndEnabled || other.IsChangingLane || _changingLane) return false;

        Vector3 mine = CachedTransform.position;
        Vector3 theirs = other.CachedTransform.position;

        return theirs.z > mine.z && Mathf.Abs(theirs.x - mine.x) <= sameLaneTolerance;
    }

    /// <summary>The car ahead is close enough that we must not drive faster than it.</summary>
    private bool IsLimitedBy(Vehicle leader)
    {
        return IsAheadInMyLane(leader) &&
               leader.CachedTransform.position.z - CachedTransform.position.z <= followDistance;
    }

    // ---------------------------------------------------------------- Horn reactions

    /// <summary>Smoothly moves the car sideways to <paramref name="targetX"/> over <paramref name="duration"/> seconds.</summary>
    public void ChangeLane(float targetX, float duration)
    {
        _laneFromX = CachedTransform.position.x;
        _laneToX = targetX;
        _laneTimer = 0f;
        _laneDuration = Mathf.Max(0.05f, duration);
        _changingLane = true;
        _leader = null; // Whoever was ahead in the old lane no longer matters.
    }

    /// <summary>Raises the cruise speed by <paramref name="extraSpeed"/> (world units/s = km/h). Still won't drive into a slower car ahead.</summary>
    public void SpeedUp(float extraSpeed)
    {
        _cruiseSpeed += Mathf.Max(0f, extraSpeed);
    }

    private float UpdateLaneChange(float deltaTime)
    {
        _laneTimer += deltaTime;
        float t = Mathf.Clamp01(_laneTimer / _laneDuration);
        float eased = t * t * (3f - 2f * t); // smoothstep

        // Nose points into the new lane, most at the middle of the move, straight again at the end.
        float direction = Mathf.Sign(_laneToX - _laneFromX);
        float yaw = Mathf.Sin(t * Mathf.PI) * laneChangeYaw * direction;
        CachedTransform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (t >= 1f)
        {
            _changingLane = false;
            CachedTransform.rotation = Quaternion.identity;
        }

        return Mathf.Lerp(_laneFromX, _laneToX, eased);
    }
}