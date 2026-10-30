using System.Collections;
using UnityEngine;

/// <summary>
/// Spawns traffic in waves at lane spawn points ahead of the taxi.
/// Checks that a lane is clear before spawning (no stacked cars) and always leaves at least one lane open.
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

    [Header("Speed factor range (see Vehicle)")]
    public float vehicleMinSpeed = 0.3f;
    public float vehicleMaxSpeed = 1f;

    [Header("Difficulty")]
    [Tooltip("Delays are multiplied by this at full difficulty (denser traffic the further you go).")]
    [SerializeField, Range(0.3f, 1f)] private float delayAtMaxDifficulty = 0.65f;
    [Tooltip("Distance at which difficulty reaches its maximum.")]
    [SerializeField] private float maxDifficultyDistance = 6000f;

    [Header("Spacing")]
    [Tooltip("Half size of the box that must be free of cars for a lane to be used.")]
    [SerializeField] private Vector3 clearanceHalfExtents = new Vector3(1.4f, 2f, 7f);
    [Tooltip("Half depth of the area checked when making sure one lane stays open.")]
    [SerializeField] private float openLaneCheckDepth = 30f;

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

    private float DifficultyScale()
    {
        float distance = WorldScroller.Instance != null ? WorldScroller.Instance.Distance : 0f;
        return Mathf.Lerp(1f, delayAtMaxDifficulty, Mathf.Clamp01(distance / maxDifficultyDistance));
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

    private bool IsAreaClear(Vector3 center, Vector3 halfExtents)
    {
        int hits = Physics.OverlapBoxNonAlloc(center, halfExtents, _overlapBuffer, Quaternion.identity,
            Physics.AllLayers, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits; i++)
        {
            if (_overlapBuffer[i].CompareTag("Vehicle")) return false;
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
