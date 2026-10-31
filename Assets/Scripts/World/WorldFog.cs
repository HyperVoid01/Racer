using UnityEngine;

/// <summary>
/// Hides the far end of the world. The fog becomes fully opaque before the line where new road pieces,
/// cars and potholes appear, and the camera background is painted with the fog colour, so fogged objects
/// blend into the horizon instead of showing up as grey shapes against the sky.
/// Added to the main camera automatically by <see cref="PlatformSpawner"/>; you can also add it yourself
/// to tweak the values in the inspector.
/// </summary>
[RequireComponent(typeof(Camera))]
public class WorldFog : MonoBehaviour
{
    [Tooltip("Colour of the fog and, if enabled below, of the sky behind it.")]
    [SerializeField] private Color fogColor = new Color(0.72f, 0.80f, 0.88f, 1f);

    [Tooltip("Paint the camera background with the fog colour. Without this, fogged objects show up " +
             "against the skybox, because URP does not fog the skybox.")]
    [SerializeField] private bool matchBackgroundToFog = true;

    [Tooltip("Fog is fully opaque this many units before the spawn line, so a newly spawned road piece " +
             "(about 53 units long) is completely hidden.")]
    [SerializeField] private float hiddenMargin = 60f;

    [Tooltip("Linear fog only: where the fog starts, as a fraction of the fully-opaque distance.")]
    [SerializeField, Range(0f, 0.95f)] private float linearStartFraction = 0.35f;

    [Tooltip("Don't render anything past the opaque fog (it would be invisible anyway). Saves GPU on mobile.")]
    [SerializeField] private bool clipFarPlane = true;

    [Tooltip("Spawn line used when there is no PlatformSpawner in the scene.")]
    [SerializeField] private float fallbackSpawnZ = 300f;

    // ln(100): fog is 99% opaque at the target distance.
    private const float OpaqueLog = 4.60517f;

    private Camera _camera;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    private void Start()
    {
        Apply();
    }

#if UNITY_EDITOR
    // Live tweaking in Play mode.
    private void OnValidate()
    {
        if (Application.isPlaying && _camera != null) Apply();
    }
#endif

    public void Apply()
    {
        float spawnZ = PlatformSpawner.Instance != null ? PlatformSpawner.Instance.SpawnAheadZ : fallbackSpawnZ;
        float opaqueDistance = Mathf.Max(20f, spawnZ - hiddenMargin - _camera.transform.position.z);

        // Keep the fog mode chosen in the scene's Lighting settings. Switching modes at runtime can hit a
        // shader variant that was stripped from the build.
        RenderSettings.fog = true;
        RenderSettings.fogColor = fogColor;
        switch (RenderSettings.fogMode)
        {
            case FogMode.Linear:
                RenderSettings.fogEndDistance = opaqueDistance;
                RenderSettings.fogStartDistance = opaqueDistance * linearStartFraction;
                break;
            case FogMode.Exponential:
                RenderSettings.fogDensity = OpaqueLog / opaqueDistance;
                break;
            case FogMode.ExponentialSquared:
                RenderSettings.fogDensity = Mathf.Sqrt(OpaqueLog) / opaqueDistance;
                break;
        }

        if (matchBackgroundToFog)
        {
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = fogColor;
        }

        if (clipFarPlane)
        {
            _camera.farClipPlane = Mathf.Max(_camera.nearClipPlane + 1f, opaqueDistance + 10f);
        }
    }
}