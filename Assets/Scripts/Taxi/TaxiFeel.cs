using UnityEngine;

/// <summary>
/// Purely visual body motion for the taxi model: leans and yaws into lane changes, squats under throttle,
/// dives under braking, bounces over potholes and idles at stops. Added automatically by TaxiController.
/// Gameplay (colliders, position) is never affected; only this transform's local pose changes.
/// </summary>
public class TaxiFeel : MonoBehaviour
{
    [Header("Lean")]
    [SerializeField] private float maxRoll = 4f;
    [SerializeField] private float maxYaw = 7f;
    [SerializeField] private float accelPitch = -1.5f;
    [SerializeField] private float brakePitch = 2.5f;
    [SerializeField] private float response = 8f;

    [Header("Bumps")]
    [SerializeField] private float bumpKick = 2.2f;
    [SerializeField] private float bumpStiffness = 180f;
    [SerializeField] private float bumpDamping = 14f;
    [SerializeField] private float bumpPitchPerUnit = 25f;

    [Header("Idle")]
    [SerializeField] private float idleShake = 0.012f;

    private TaxiController _taxi;
    private Vector3 _baseLocalPosition;
    private Quaternion _baseLocalRotation;
    private Vector3 _angles;
    private float _bumpOffset;
    private float _bumpVelocity;

    private void Awake()
    {
        _taxi = GetComponentInParent<TaxiController>();
        _baseLocalPosition = transform.localPosition;
        _baseLocalRotation = transform.localRotation;
    }

    private void Start()
    {
        GameManager.Instance.Impact += OnImpact;
        GameManager.Instance.GameOverStarted += OnGameOver;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.Impact -= OnImpact;
        GameManager.Instance.GameOverStarted -= OnGameOver;
    }

    private void OnImpact(float strength)
    {
        _bumpVelocity += strength * bumpKick;
    }

    private void OnGameOver(GameOverReason reason)
    {
        // The rigidbody takes over after a crash; freeze the pose where it is.
        enabled = false;
    }

    private void LateUpdate()
    {
        if (_taxi == null) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        GameManager gm = GameManager.Instance;

        float lateral = _taxi.StrafeSpeed > 0f ? Mathf.Clamp(_taxi.LateralVelocity / _taxi.StrafeSpeed, -1f, 1f) : 0f;
        float pitch = 0f;
        if (_taxi.IsThrottling) pitch = accelPitch * (1f - _taxi.Speed01 * 0.5f);
        else if (_taxi.IsBraking && gm.GameSpeed > _taxi.MinimumSpeed + 0.5f) pitch = brakePitch;

        var target = new Vector3(pitch, lateral * maxYaw, -lateral * maxRoll);
        _angles = Vector3.Lerp(_angles, target, 1f - Mathf.Exp(-response * dt));

        // Damped spring for pothole bumps.
        float accel = -bumpStiffness * _bumpOffset - bumpDamping * _bumpVelocity;
        _bumpVelocity += accel * dt;
        _bumpOffset += _bumpVelocity * dt;

        float idle = gm.State == GameState.AtStop ? Mathf.Sin(Time.time * 55f) * idleShake : 0f;

        transform.localPosition = _baseLocalPosition + new Vector3(0f, _bumpOffset + idle, 0f);
        transform.localRotation = _baseLocalRotation *
                                  Quaternion.Euler(_angles.x - _bumpOffset * bumpPitchPerUnit, _angles.y, _angles.z);
    }
}
