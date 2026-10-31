using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Keeps the road filled up to <see cref="spawnAheadZ"/>. Every <see cref="taxiStopInterval"/> pieces,
/// a taxi stop is placed instead of a regular piece. Also makes sure the main camera has a
/// <see cref="WorldFog"/>, which hides pieces as they appear.
/// </summary>
public class PlatformSpawner : MonoBehaviour
{
    public static PlatformSpawner Instance { get; private set; }

    [SerializeField] private GameObject[] platformPrefabs;
    [SerializeField] private GameObject taxiStopPrefab;
    [Tooltip("Road is generated until it reaches this Z. The fog is fitted to this distance by WorldFog.")]
    [FormerlySerializedAs("spawnerDistance")]
    [SerializeField] private float spawnAheadZ = 300f;
    [Tooltip("Regular road pieces between taxi stops.")]
    [SerializeField] private int taxiStopInterval = 8;
    [Tooltip("Never place the same road piece twice in a row (when there is more than one).")]
    [SerializeField] private bool avoidRepeats = true;

    private Transform _connectionPoint;
    private int _piecesSinceStop;
    private int _lastPrefabIndex = -1;

    /// <summary>The Z up to which road is generated. New pieces appear just short of this line.</summary>
    public float SpawnAheadZ => spawnAheadZ;

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
        EnsureWorldFog();

        if (platformPrefabs == null || platformPrefabs.Length == 0)
        {
            Debug.LogError("[PlatformSpawner] No platform prefabs assigned.", this);
            enabled = false;
            return;
        }

        // First piece starts at the spawner. (The original took the spawner's own child as the first connection
        // point, which placed the second piece on top of the first one.)
        Platform first = SpawnPiece(PickPlatformPrefab(), transform.position, transform.rotation);
        _connectionPoint = first.ConnectionPoint;

        // Build the whole visible road immediately instead of over the first few frames.
        FillRoad();
    }

    private void Update()
    {
        if (_connectionPoint == null) return;
        FillRoad();
    }

    private static void EnsureWorldFog()
    {
        Camera cam = Camera.main;
        if (cam != null && !cam.TryGetComponent(out WorldFog _))
        {
            cam.gameObject.AddComponent<WorldFog>();
        }
    }

    private void FillRoad()
    {
        // A loop (not one per frame) so very high speeds can never outrun the spawner.
        int safety = 32;
        while (_connectionPoint.position.z < spawnAheadZ && safety-- > 0)
        {
            bool placeStop = taxiStopPrefab != null && _piecesSinceStop >= taxiStopInterval;
            GameObject prefab = placeStop ? taxiStopPrefab : PickPlatformPrefab();

            Platform piece = SpawnPiece(prefab, _connectionPoint.position, _connectionPoint.rotation);
            _connectionPoint = piece.ConnectionPoint;
            _piecesSinceStop = placeStop ? 0 : _piecesSinceStop + 1;
        }
    }

    private Platform SpawnPiece(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        GameObject go = PrefabPool.Spawn(prefab, position, rotation);
        if (!go.TryGetComponent(out Platform platform))
        {
            platform = go.AddComponent<Platform>();
        }
        return platform;
    }

    private GameObject PickPlatformPrefab()
    {
        int count = platformPrefabs.Length;
        int index = Random.Range(0, count);
        if (avoidRepeats && count > 1 && index == _lastPrefabIndex)
        {
            index = (index + Random.Range(1, count)) % count;
        }
        _lastPrefabIndex = index;
        return platformPrefabs[index];
    }
}