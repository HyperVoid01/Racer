using UnityEngine;

/// <summary>
/// Places potholes by distance driven rather than by time, so their spacing on the road stays the same at any
/// speed and none pile up while the taxi is parked at a stop.
/// </summary>
public class PotholeSpawner : MonoBehaviour
{
    [SerializeField] private GameObject potholePrefab;

    [Header("Spacing (world units)")]
    [SerializeField] private float minSpacing = 90f;
    [SerializeField] private float maxSpacing = 210f;
    [Tooltip("Spacing is multiplied by this at full difficulty (more potholes the further you go).")]
    [SerializeField, Range(0.2f, 1f)] private float spacingAtMaxDifficulty = 0.6f;
    [Tooltip("Distance at which difficulty reaches its maximum.")]
    [SerializeField] private float maxDifficultyDistance = 6000f;

    [Header("Lane range (X)")]
    [UnityEngine.Serialization.FormerlySerializedAs("point1")]
    [SerializeField] private float minX = -8f;
    [UnityEngine.Serialization.FormerlySerializedAs("point2")]
    [SerializeField] private float maxX = 8f;

    private float _distanceUntilNext;

    private void Start()
    {
        if (potholePrefab == null)
        {
            enabled = false;
            return;
        }

        PrefabPool.Prewarm(potholePrefab, 4);
        _distanceUntilNext = NextSpacing();
    }

    private void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Driving || WorldScroller.Instance == null) return;

        _distanceUntilNext -= WorldScroller.Instance.LastDelta;
        if (_distanceUntilNext > 0f) return;

        Vector3 position = transform.position;
        position.x = Random.Range(minX, maxX);
        PrefabPool.Spawn(potholePrefab, position, Quaternion.identity);

        _distanceUntilNext += NextSpacing();
    }

    private float NextSpacing()
    {
        float difficulty = WorldScroller.Instance != null
            ? Mathf.Clamp01(WorldScroller.Instance.Distance / maxDifficultyDistance)
            : 0f;
        float scale = Mathf.Lerp(1f, spacingAtMaxDifficulty, difficulty);
        return Random.Range(minSpacing, maxSpacing) * scale;
    }
}
