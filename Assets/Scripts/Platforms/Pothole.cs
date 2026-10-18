using System;
using System.Collections;
using UnityEngine;

public class Pothole : MonoBehaviour
{
    [SerializeField] private int speedSet = 15;

    private void Start()
    {
        StartCoroutine(MovePlatform());
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && GameManager.Instance.gameSpeed > speedSet)
        {
            GameManager.Instance.gameSpeed = speedSet;
            AudioManager.Instance.Play("Pothole");
        }
        
        if (other.CompareTag("Destroyer"))
        {
            Destroy(gameObject);
        }
    }
    
    private IEnumerator MovePlatform()
    {
        while (true)
        {
            Vector3 targetPosition = new Vector3(transform.position.x, transform.position.y, transform.position.z - (PlatformSpawner.Instance.platformSpeed * GameManager.Instance.gameSpeed));
            Vector3 newLocation = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime);
            transform.position = newLocation;
            yield return null;
        }
    }
}
