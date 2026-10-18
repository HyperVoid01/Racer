using System.Collections;
using UnityEngine;

public class VehicleSpawner : MonoBehaviour
{
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private GameObject[] vehiclePrefab;
    [SerializeField] private float spawnInterval = 7f;
    [SerializeField] private float intervalMinDelay = 2f;
    [SerializeField] private float intervalMaxDelay = 5f;
    public float vehicleMaxSpeed = 1;
    public float vehicleMinSpeed = 5;

    public static VehicleSpawner Instance;
    
    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        StartCoroutine(SpawnVehicles());
    }

    private void Update()
    {
        if (GameManager.Instance.gameOver)
        {
            StopAllCoroutines();
        }
    }

    private IEnumerator SpawnVehicles()
    {
        while (true)
        {
            int spawnPointCount = Random.Range(1, spawnPoints.Length - 1);

            for (int i = 0; i < spawnPointCount; i++)
            {
                Instantiate(vehiclePrefab[Random.Range(0, vehiclePrefab.Length)], spawnPoints[Random.Range(0, spawnPoints.Length)].position, Quaternion.identity);
                yield return new WaitForSeconds(Random.Range(intervalMinDelay, intervalMaxDelay));
            }

            yield return new WaitForSeconds(spawnInterval);
        }
    }
}
