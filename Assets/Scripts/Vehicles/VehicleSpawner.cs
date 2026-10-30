using System.Collections;
using UnityEngine;

/// <summary>
/// Spawns traffic in waves at lane spawn points ahead of the taxi.
/// Checks that a lane is clear before spawning (no stacked cars) and always leaves at least one lane open.
/// Also answers "is there a free lane next to this car?" for cars reacting to the horn.
/// </summary>
public class VehicleSpawner : MonoBehaviour
{
    public static VehicleSpawner Instance { get; private set; }

    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private GameObject[] vehiclePrefab;

    [Header("Timing (seconds)")]
    [Tooltip("Pause between waves.")]
    [SerializeField] private float spawnInterval = 0.5f;
    [Tooltip("Random delay between cars within a wave.")]
    [SerializeField] private float intervalMinDelay = 0f;
    [SerializeField] private float intervalMaxDelay = 1.5f;

    [Header("Traffic speed (km/h, as shown on the HUD)")]
    [Tooltip("Slowest a car can cruise. The taxi's minimum is 30 km/h on the HUD.")]
    [SerializeField] private float trafficMinKmh = 25f;
    [Tooltip("Fastest a car can cruise.")]
    [SerializeField] private float trafficMaxKmh = 55f;
    [Tooltip("While driving, new cars are always at least this much (km/h) slower than the taxi, so they come " +
             "into view instead of spawning and immediately pulling away. Cars already on the road keep their speed.")]
    [SerializeField] private float minClosingKmh = 4f;

    [Header("Difficulty")]
    [Tooltip("Delays are multiplied by this at full difficulty (denser traffic the further you go).")]
    [SerializeField, Range(0.3f, 1f)] private float delayAtMaxDifficulty = 0.65f;
    [Tooltip("Added to traffic speeds at full difficulty (km/h).")]
    [SerializeField] private float extraKmhAtMaxDifficulty = 10f;
    [Tooltip("Distance at which difficulty reaches its maximum.")]
    [SerializeField] private float maxDifficultyDistance = 6000f;

    [Header("Spacing")]
    [Tooltip("Half size of the box that must be free of cars for a lane to be used.")]
    [SerializeField] private Vector3 clearanceHalfExtents = new Vector3(1.4f, 2f, 7f);
    [Tooltip("Half depth of the area checked when making sure one lane stays open.")]
    [SerializeField] private float openLaneCheckDepth = 30f;
    [Tooltip("Extra length (multiplier on the clearance depth) a lane must be free for a car to swerve into it.")]
    [SerializeField] private float laneChangeClearanceScale = 1.6f;

    private readonly Collider[] _overlapBuffer = new Collider[32];
    private int _lastLane = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (spawnPoints == null || spawnPoints.Length == 0 || vehiclePrefab == null || vehiclePrefab.Length == 0)
        {
            Debug.LogError("[VehicleSpawner] Missing spawn points or vehicle prefabs.", this);
            enabled = false;
            return;
        }

        foreach (GameObject prefab in vehiclePrefab) PrefabPool.Prewarm(prefab, 3);
        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            // No new traffic while parked at a stop or after the run ends.
            while (!CanSpawn()) yield return null;

            int carsInWave = Random.Range(1, Mathf.Max(2, spawnPoints.Length - 1));
            for (int i = 0; i < carsInWave; i++)
            {
                if (CanSpawn()) TrySpawnCar();
                yield return new WaitForSeconds(Random.Range(intervalMinDelay, intervalMaxDelay) * DifficultyScale());
            }

            yield return new WaitForSeconds(spawnInterval * DifficultyScale());
        }
    }

    private static bool CanSpawn()
    {
        GameManager gm = GameManager.Instance;
        return gm != null && gm.State == GameState.Driving;
    }

    private float Difficulty01()
    {
        float distance = WorldScroller.Instance != null ? WorldScroller.Instance.Distance : 0f;
        return maxDifficultyDistance > 0f ? Mathf.Clamp01(distance / maxDifficultyDistance) : 1f;
    }

    private float DifficultyScale() => Mathf.Lerp(1f, delayAtMaxDifficulty, Difficulty01());

    /// <summary>HUD km/h to world units per second.</summary>
    public static float KmhToSpeed(float kmh) => Mathf.Max(0f, kmh - GameManager.SpeedDisplayOffset);

    /// <summary>Cruise speed (world units/s) for a newly spawned car.</summary>
    public float PickCruiseSpeed()
    {
        float bonus = extraKmhAtMaxDifficulty * Difficulty01();
        float min = KmhToSpeed(Mathf.Min(trafficMinKmh, trafficMaxKmh) + bonus);
        float max = KmhToSpeed(Mathf.Max(trafficMinKmh, trafficMaxKmh) + bonus);

        // Keep new cars slower than the taxi so they are actually met. If the taxi is slower than all
        // traffic (just leaving a stop), spawn the slowest cars; they'll pull away and despawn ahead.
        GameManager gm = GameManager.Instance;
        if (gm != null && gm.State == GameState.Driving)
        {
            float cap = gm.GameSpeed - minClosingKmh;
            max = cap > min ? Mathf.Min(max, cap) : min;
        }

        return Random.Range(min, max);
    }

    private void TrySpawnCar()
    {
        // Kinematic cars are moved through their transforms; make sure queries see where they are now.
        Physics.SyncTransforms();

        int laneCount = spawnPoints.Length;
        int lane = Random.Range(0, laneCount);
        if (lane == _lastLane && laneCount > 1) lane = (lane + Random.Range(1, laneCount)) % laneCount;

        if (!IsAreaClear(spawnPoints[lane].position, clearanceHalfExtents)) return;
        if (!LeavesALaneOpen(lane)) return;

        GameObject prefab = vehiclePrefab[Random.Range(0, vehiclePrefab.Length)];
        PrefabPool.Spawn(prefab, spawnPoints[lane].position, Quaternion.identity);
        _lastLane = lane;
    }

    private bool LeavesALaneOpen(int laneToFill)
    {
        var rowExtents = new Vector3(clearanceHalfExtents.x, clearanceHalfExtents.y, openLaneCheckDepth);
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (i == laneToFill) continue;
            if (IsAreaClear(spawnPoints[i].position, rowExtents)) return true;
        }
        return false;
    }

    /// <summary>
    /// Finds a free lane directly left or right of <paramref name="vehicle"/> (random side first).
    /// Lanes are the spawn points' X positions, so their order in the array doesn't matter.
    /// </summary>
    public bool TryGetFreeLaneBeside(Vehicle vehicle, out float laneX)
    {
        laneX = 0f;
        if (vehicle == null || spawnPoints == null || spawnPoints.Length < 2) return false;

        Physics.SyncTransforms();

        Vector3 position = vehicle.transform.position;
        bool hasLeft = TryGetNeighbourLaneX(position.x, -1f, out float leftX);
        bool hasRight = TryGetNeighbourLaneX(position.x, 1f, out float rightX);

        var halfExtents = new Vector3(clearanceHalfExtents.x, clearanceHalfExtents.y,
            clearanceHalfExtents.z * laneChangeClearanceScale);

        bool tryLeftFirst = Random.value < 0.5f;
        for (int attempt = 0; attempt < 2; attempt++)
        {
            bool left = attempt == 0 ? tryLeftFirst : !tryLeftFirst;
            if (left ? !hasLeft : !hasRight) continue;

            float x = left ? leftX : rightX;
            if (IsAreaClear(new Vector3(x, position.y, position.z), halfExtents, vehicle.transform))
            {
                laneX = x;
                return true;
            }
        }
        return false;
    }

    /// <summary>Nearest lane X on one side (<paramref name="side"/> -1 = left, 1 = right) of <paramref name="x"/>.</summary>
    private bool TryGetNeighbourLaneX(float x, float side, out float laneX)
    {
        const float sameLaneTolerance = 0.75f;
        laneX = 0f;
        float bestDistance = float.MaxValue;

        foreach (Transform point in spawnPoints)
        {
            if (point == null) continue;

            float offset = (point.position.x - x) * side;
            if (offset <= sameLaneTolerance || offset >= bestDistance) continue;

            bestDistance = offset;
            laneX = point.position.x;
        }
        return bestDistance < float.MaxValue;
    }

    private bool IsAreaClear(Vector3 center, Vector3 halfExtents, Transform ignore = null)
    {
        int hits = Physics.OverlapBoxNonAlloc(center, halfExtents, _overlapBuffer, Quaternion.identity,
            Physics.AllLayers, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits; i++)
        {
            Collider hit = _overlapBuffer[i];
            if (ignore != null && hit.transform.IsChildOf(ignore)) continue;
            if (hit.CompareTag("Vehicle")) return false;
        }
        return true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (spawnPoints == null) return;
        Gizmos.color = Color.yellow;
        foreach (Transform point in spawnPoints)
        {
            if (point != null) Gizmos.DrawWireCube(point.position, clearanceHalfExtents * 2f);
        }
    }
#endif
}