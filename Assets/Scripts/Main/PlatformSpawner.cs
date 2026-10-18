using System;
using System.Collections;
using UnityEngine;

public class PlatformSpawner : MonoBehaviour
{
    [SerializeField] private GameObject[] platformPrefabs;
    [SerializeField] private GameObject taxiStopPrefab;
    [SerializeField] public float platformSpeed = 1f;
    [SerializeField] private int spawnerDistance = 50;
    private Transform _connectionPoint;
    public int _platformIndex = 0;
    [SerializeField] private int taxiStopInterval;

    public static PlatformSpawner Instance;
    
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
        GameObject FirstPlatform = Instantiate(SelectRandomPlatform(), transform);
        _connectionPoint = FirstPlatform.transform.parent.GetChild(0);
        FirstPlatform.transform.parent = null;
        
        StartCoroutine(SpawnPlatform());
        StartCoroutine(InitializeStartPlatforms());
    }

    private void Update()
    {
        if (GameManager.Instance.gameOver)
        {
            StopAllCoroutines();
        }
    }

    private GameObject SelectRandomPlatform()
    {
        int i = UnityEngine.Random.Range(0, platformPrefabs.Length);
        return platformPrefabs[i];
    }
    
    private IEnumerator SpawnPlatform()
    {
        while (true)
        {
            if (_connectionPoint.position.z < this.gameObject.transform.position.z)
            {
                if (_platformIndex < taxiStopInterval)
                {
                    GameObject lastPlatform = Instantiate(SelectRandomPlatform(), _connectionPoint.position, _connectionPoint.rotation);
                    _connectionPoint = lastPlatform.transform.GetChild(0);
                    _platformIndex++;
                }
                else
                {
                    GameObject taxiStop = Instantiate(taxiStopPrefab, _connectionPoint.position, _connectionPoint.rotation);
                    _connectionPoint = taxiStop.transform.GetChild(0);
                    _platformIndex = 0;
                }
            }

            yield return null;
        }
    }


    private IEnumerator InitializeStartPlatforms()
    {
        while (transform.position.z < spawnerDistance)
        {
            Vector3 spawnerLocation = new Vector3(0f, 0f, spawnerDistance);
            transform.position = Vector3.Lerp(transform.position, spawnerLocation, 50 * Time.deltaTime);

            yield return new WaitForSeconds(0.01f);
        }
    }
}
