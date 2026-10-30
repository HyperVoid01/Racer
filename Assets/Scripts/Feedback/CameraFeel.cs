using UnityEngine;

/// <summary>
/// Camera juice for the run: field of view widens with speed, the camera drifts slightly with the taxi's
/// lane and shakes on impacts (trauma-based, Perlin noise). Added to the main camera by GameManager.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFeel : MonoBehaviour
{
    [Header("Speed")]
    [SerializeField] private float fovBoostAtTopSpeed = 9f;
    [SerializeField] private float pullBackAtTopSpeed = 0.8f;
    [SerializeField] private float speedResponse = 3f;

    [Header("Follow")]
    [Tooltip("Fraction of the taxi's sideways position the camera follows.")]
    [SerializeField, Range(0f, 1f)] private float followX = 0.25f;
    [SerializeField] private float followResponse = 4f;

    [Header("Shake")]
    [SerializeField] private float maxShakeOffset = 0.35f;
    [SerializeField] private float maxShakeRoll = 2.5f;
    [SerializeField] private float shakeFrequency = 22f;
    [SerializeField] private float traumaDecay = 1.6f;

    private Camera _camera;
    private float _baseFov;
    private Vector3 _basePosition;
    private Quaternion _baseRotation;
    private float _speedBlend;
    private float _followOffset;
    private float _trauma;
    private float _noiseSeed;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _baseFov = _camera.fieldOfView;
        _basePosition = transform.localPosition;
        _baseRotation = transform.localRotation;
        _noiseSeed = Random.value * 100f;
    }

    private void Start()
    {
        if (GameManager.Instance != null) GameManager.Instance.Impact += AddTrauma;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.Impact -= AddTrauma;
    }

    /// <summary>Add shake. 0.2 = small bump, 1 = big crash. Trauma is squared, so small values stay subtle.</summary>
    public void AddTrauma(float amount)
    {
        _trauma = Mathf.Clamp01(_trauma + amount);
    }

    private void LateUpdate()
    {
        GameManager gm = GameManager.Instance;
        TaxiController taxi = TaxiController.Instance;
        float dt = Time.unscaledDeltaTime;
        if (gm == null || gm.IsPaused) return;

        float speed01 = taxi != null ? taxi.Speed01 : 0f;
        _speedBlend = Mathf.Lerp(_speedBlend, speed01 * speed01, 1f - Mathf.Exp(-speedResponse * dt));
        _camera.fieldOfView = _baseFov + fovBoostAtTopSpeed * _speedBlend;

        if (taxi != null && !taxi.HasCrashed)
        {
            float targetFollow = taxi.transform.position.x * followX;
            _followOffset = Mathf.Lerp(_followOffset, targetFollow, 1f - Mathf.Exp(-followResponse * dt));
        }

        float shake = _trauma * _trauma;
        float time = Time.unscaledTime * shakeFrequency;
        Vector3 shakeOffset = new Vector3(
            (Mathf.PerlinNoise(_noiseSeed, time) - 0.5f) * 2f,
            (Mathf.PerlinNoise(_noiseSeed + 10f, time) - 0.5f) * 2f,
            0f) * (maxShakeOffset * shake);
        float roll = (Mathf.PerlinNoise(_noiseSeed + 20f, time) - 0.5f) * 2f * maxShakeRoll * shake;

        _trauma = Mathf.Max(0f, _trauma - traumaDecay * dt);

        transform.localPosition = _basePosition +
                                  new Vector3(_followOffset, 0f, -pullBackAtTopSpeed * _speedBlend) +
                                  shakeOffset;
        transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, roll);
    }
}
