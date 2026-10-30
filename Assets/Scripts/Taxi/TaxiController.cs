using System.Collections;
using UnityEngine;

/// <summary>
/// Player taxi. The taxi stays at Z = 0 and the world scrolls past it: this script controls the scroll speed
/// (<see cref="GameManager.GameSpeed"/>) and the taxi's sideways position.
/// Reads input only through <see cref="TaxiInput"/> so keyboard, gamepad and touch all work.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class TaxiController : MonoBehaviour
{
    public static TaxiController Instance { get; private set; }

    [Header("Taxi Base Stats")]
    [SerializeField] private float baseAcceleration = 0.4f;
    [SerializeField] private float baseDeceleration = 1f;
    [SerializeField] private float baseBraking = 4f;
    [SerializeField] private float minimumSpeed = 10f;
    [SerializeField] private float baseTopSpeed = 40f;
    [SerializeField] private float baseStrafeSpeed = 3f;
    [SerializeField] private float wallCollisionSpeedDecrease = 2.5f;
    [SerializeField] private float crashModifier = 0.5f;

    [Header("Handling")]
    [Tooltip("Below this speed the taxi can't change lanes (it isn't moving enough to steer).")]
    [SerializeField] private float minSteerSpeed = 5f;
    [Tooltip("How quickly sideways speed reaches its target. Higher = snappier, lower = floatier.")]
    [SerializeField] private float lateralResponse = 10f;
    [Tooltip("Acceleration (units/s²) used to get back up to the minimum speed after a stop.")]
    [SerializeField] private float recoverToMinimumRate = 15f;
    [Tooltip("Hard limits on X as a safety net in case a wall trigger is ever missed.")]
    [SerializeField] private Vector2 xLimits = new Vector2(-16f, 8.6f);

    [Header("Crash")]
    [SerializeField] private float crashUpwardImpulse = 4f;
    [SerializeField] private float crashSpin = 6f;

    [Header("Ambience")]
    public float minWhistle = 10f;
    public float maxWhistle = 20f;

    private Rigidbody _rigidbody;
    private Collider _collider;
    private TaxiHorn _horn;
    private int _leftWallContacts;
    private int _rightWallContacts;

    public float Acceleration { get; private set; }
    public float Deceleration { get; private set; }
    public float Braking { get; private set; }
    public float TopSpeed { get; private set; }
    public float StrafeSpeed { get; private set; }
    public float MinimumSpeed => minimumSpeed;

    /// <summary>Current sideways speed in units/s (negative = left).</summary>
    public float LateralVelocity { get; private set; }
    public bool IsThrottling { get; private set; }
    public bool IsBraking { get; private set; }
    public bool HasCrashed { get; private set; }

    public float Speed01 => TopSpeed > 0f ? Mathf.Clamp01(GameManager.Instance.GameSpeed / TopSpeed) : 0f;

    private void Awake()
    {
        Instance = this;

        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();

        // Driven by script until it crashes. Kinematic bodies still receive trigger events.
        _rigidbody.isKinematic = true;
        _rigidbody.useGravity = false;

        if (!TryGetComponent(out TaxiEngineAudio _)) gameObject.AddComponent<TaxiEngineAudio>();
        if (!TryGetComponent(out _horn)) _horn = gameObject.AddComponent<TaxiHorn>();

        TaxiVisuals visuals = GetComponentInChildren<TaxiVisuals>();
        if (visuals != null && !visuals.TryGetComponent(out TaxiFeel _))
        {
            visuals.gameObject.AddComponent<TaxiFeel>();
        }
    }

    private void Start()
    {
        UpgradeManager upgrades = UpgradeManager.Instance;
        Acceleration = baseAcceleration + upgrades.AccelerationModifier;
        Deceleration = Mathf.Max(0.05f, baseDeceleration - upgrades.DecelerationModifier);
        Braking = baseBraking + upgrades.BrakingModifier;
        TopSpeed = baseTopSpeed + upgrades.TopSpeedModifier;
        StrafeSpeed = baseStrafeSpeed + upgrades.StrafeSpeedModifier;

        GameManager.Instance.GameOverStarted += OnGameOver;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.GameOverStarted -= OnGameOver;
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm.IsGameOver || gm.IsPaused) return;

        if (TaxiInput.ConsumeHornPress()) _horn.Hoot();

        float dt = Time.deltaTime;

        if (gm.State == GameState.AtStop)
        {
            // The stop controls the speed; just settle the steering.
            IsThrottling = IsBraking = false;
            LateralVelocity = Mathf.Lerp(LateralVelocity, 0f, 1f - Mathf.Exp(-lateralResponse * dt));
            return;
        }

        UpdateSpeed(gm, dt);
        UpdateSteering(gm, dt);
    }

    private void UpdateSpeed(GameManager gm, float dt)
    {
        float previous = gm.GameSpeed;
        float speed = previous;

        IsThrottling = TaxiInput.Throttle;
        IsBraking = !IsThrottling && TaxiInput.Brake;

        if (IsThrottling) speed = Mathf.Lerp(speed, TopSpeed, Acceleration * dt);
        else if (IsBraking) speed -= Braking * dt;
        else speed -= Deceleration * dt;

        // Changing lanes scrubs a little speed, as in the original.
        float steer = Mathf.Abs(TaxiInput.Steer);
        if (steer > 0.01f && previous >= minSteerSpeed) speed -= Deceleration * steer * dt;

        if (speed < minimumSpeed)
        {
            // Normally clamp to the minimum, but after a stop ramp up to it instead of jumping.
            speed = Mathf.Max(speed, Mathf.Min(minimumSpeed, previous + recoverToMinimumRate * dt));
        }

        gm.GameSpeed = speed;
    }

    private void UpdateSteering(GameManager gm, float dt)
    {
        float steer = gm.GameSpeed >= minSteerSpeed ? TaxiInput.Steer : 0f;

        if (steer < 0f && _leftWallContacts > 0) steer = 0f;
        if (steer > 0f && _rightWallContacts > 0) steer = 0f;

        float target = steer * StrafeSpeed;
        LateralVelocity = Mathf.Lerp(LateralVelocity, target, 1f - Mathf.Exp(-lateralResponse * dt));

        // Never keep sliding into a wall we're touching.
        if ((LateralVelocity < 0f && _leftWallContacts > 0) || (LateralVelocity > 0f && _rightWallContacts > 0))
        {
            LateralVelocity = 0f;
        }

        Vector3 position = transform.position;
        position.x = Mathf.Clamp(position.x + LateralVelocity * dt, xLimits.x, xLimits.y);
        transform.position = position;
    }

    private void OnTriggerEnter(Collider other)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.IsGameOver) return;

        if (other.CompareTag("KillZone") || other.CompareTag("Vehicle"))
        {
            gm.EndGame(GameOverReason.Crashed);
            return;
        }

        if (other.CompareTag("LeftWall"))
        {
            _leftWallContacts++;
            HitWall(gm);
        }
        else if (other.CompareTag("RightWall"))
        {
            _rightWallContacts++;
            HitWall(gm);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Counting contacts (rather than a bool cleared by a separate "entrance" trigger) stays correct
        // when the taxi slides from one road piece's wall onto the next.
        if (other.CompareTag("LeftWall")) _leftWallContacts = Mathf.Max(0, _leftWallContacts - 1);
        else if (other.CompareTag("RightWall")) _rightWallContacts = Mathf.Max(0, _rightWallContacts - 1);
    }

    private void HitWall(GameManager gm)
    {
        LateralVelocity = 0f;
        if (gm.State != GameState.Driving) return;

        gm.GameSpeed -= wallCollisionSpeedDecrease;
        gm.ReportImpact(0.25f);
        Haptics.Light();
    }

    private void OnGameOver(GameOverReason reason)
    {
        IsThrottling = IsBraking = false;

        if (reason == GameOverReason.Crashed) Crash();
        else StartCoroutine(CoastToStop());
    }

    private void Crash()
    {
        if (HasCrashed) return;
        HasCrashed = true;

        GameManager gm = GameManager.Instance;
        float speed = gm.GameSpeed;
        gm.GameSpeed = 0f;

        // Become a real physics body so the taxi tumbles on the road instead of falling through it.
        if (_collider != null) _collider.isTrigger = false;
        _rigidbody.isKinematic = false;
        _rigidbody.constraints = RigidbodyConstraints.None;
        _rigidbody.useGravity = true;

        // The world stops, so the taxi carries the momentum forwards.
        Vector3 direction = new Vector3(Random.Range(-0.5f, 0.5f) + LateralVelocity * 0.1f, 0.35f, 1f).normalized;
        _rigidbody.AddForce(direction * (speed * crashModifier) + Vector3.up * crashUpwardImpulse, ForceMode.Impulse);
        _rigidbody.AddTorque(Random.onUnitSphere * crashSpin, ForceMode.Impulse);

        LateralVelocity = 0f;
    }

    private IEnumerator CoastToStop()
    {
        GameManager gm = GameManager.Instance;
        while (gm != null && gm.GameSpeed > 0f)
        {
            gm.GameSpeed = Mathf.MoveTowards(gm.GameSpeed, 0f, Mathf.Max(gm.GameSpeed * 2f, 8f) * Time.deltaTime);
            yield return null;
        }
    }
}