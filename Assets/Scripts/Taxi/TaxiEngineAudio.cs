using UnityEngine;

/// <summary>
/// Continuous engine sound: the idle loop's pitch follows speed and the acceleration loop fades in
/// while the throttle is held. Also plays the braking screech and random street whistles.
/// Added automatically by TaxiController.
/// </summary>
public class TaxiEngineAudio : MonoBehaviour
{
    private const string IdleSound = "Taxi Idle";
    private const string AccelerateSound = "Taxi Accelerating";

    [SerializeField] private Vector2 idlePitch = new Vector2(0.9f, 1.35f);
    [SerializeField] private Vector2 acceleratePitch = new Vector2(0.9f, 1.2f);
    [Tooltip("Idle volume while the acceleration loop is fully faded in.")]
    [SerializeField, Range(0f, 1f)] private float idleVolumeUnderThrottle = 0.35f;
    [SerializeField] private float throttleFadeSpeed = 6f;
    [Tooltip("Only screech when braking from above minimum speed + this.")]
    [SerializeField] private float brakeSoundSpeedMargin = 3f;

    private TaxiController _taxi;
    private float _throttleBlend;
    private bool _wasBraking;
    private float _whistleTimer;
    private bool _engineRunning;

    private void Start()
    {
        _taxi = GetComponent<TaxiController>();
        GameManager.Instance.GameOverStarted += OnGameOver;

        AudioManager.Play(IdleSound);
        AudioManager.Play(AccelerateSound);
        AudioManager.SetVolumeScale(AccelerateSound, 0f);
        _engineRunning = true;

        ResetWhistleTimer();
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.GameOverStarted -= OnGameOver;

        AudioManager.Stop(IdleSound);
        AudioManager.Stop(AccelerateSound);
        AudioManager.ResetModifiers(IdleSound);
        AudioManager.ResetModifiers(AccelerateSound);
    }

    private void OnGameOver(GameOverReason reason)
    {
        _engineRunning = false;
    }

    private void Update()
    {
        if (!_engineRunning || _taxi == null) return;

        GameManager gm = GameManager.Instance;
        if (gm.IsPaused) return;

        float dt = Time.deltaTime;
        float speed01 = _taxi.Speed01;

        _throttleBlend = Mathf.MoveTowards(_throttleBlend, _taxi.IsThrottling ? 1f : 0f, throttleFadeSpeed * dt);

        AudioManager.SetPitch(IdleSound, Mathf.Lerp(idlePitch.x, idlePitch.y, speed01));
        AudioManager.SetVolumeScale(IdleSound, Mathf.Lerp(1f, idleVolumeUnderThrottle, _throttleBlend));
        AudioManager.SetPitch(AccelerateSound, Mathf.Lerp(acceleratePitch.x, acceleratePitch.y, speed01));
        AudioManager.SetVolumeScale(AccelerateSound, _throttleBlend);

        bool braking = _taxi.IsBraking && gm.GameSpeed > _taxi.MinimumSpeed + brakeSoundSpeedMargin;
        if (braking && !_wasBraking) AudioManager.Play("Taxi braking");
        _wasBraking = braking;

        if (gm.State == GameState.Driving)
        {
            _whistleTimer -= dt;
            if (_whistleTimer <= 0f)
            {
                AudioManager.Play("Whistle");
                ResetWhistleTimer();
            }
        }
    }

    private void ResetWhistleTimer()
    {
        float min = _taxi != null ? _taxi.minWhistle : 10f;
        float max = _taxi != null ? _taxi.maxWhistle : 20f;
        _whistleTimer = Random.Range(min, Mathf.Max(min, max));
    }
}
