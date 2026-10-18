using UnityEngine;
using System.Collections;

public class PotholeSpawner : MonoBehaviour
{
    [SerializeField] private GameObject potholePrefab;
    [SerializeField] private float potholeMinSpawnRate;
    [SerializeField] private float potholeMaxSpawnRate;
    [SerializeField] private float point1;
    [SerializeField] private float point2;

    private void Start()
    {
        StartCoroutine(SpawnPothole());
    }

    private IEnumerator SpawnPothole()
    {
        while (GameManager.Instance.gameOver == false)
        {
            if (!UIController.instance._isPaused)
            {
                float randomX = Random.Range(point1, point2);
                Vector3 randomPos = transform.position;
                randomPos.x = randomX;
                Instantiate(potholePrefab, randomPos, Quaternion.identity);
            }
            
            yield return new WaitForSeconds(Random.Range(potholeMinSpawnRate, potholeMaxSpawnRate));
        }
    }
}
