using System.Collections;
using UnityEngine;

public class TaxiStop : MonoBehaviour
{
    [SerializeField] private float deceleration = 10f;
    [SerializeField] private int minPassengers = 5;
    [SerializeField] private int maxPassengers = 10;
    [SerializeField] private float loadingDuration = 5f;
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            AudioManager.Instance.Play("Hey taxi");
            GameManager.Instance.atStop = true;
            StartCoroutine(StopTaxi());
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && GameManager.Instance.gameSpeed > 0)
        {
            StopCoroutine(StopTaxi());
            GameManager.Instance.atStop = false;
        }
    }

    private IEnumerator StopTaxi()
    {
        Timer.Instance.StopTimer();
        
        while (GameManager.Instance.gameSpeed > 0)
        {
            GameManager.Instance.gameSpeed -= deceleration;
            
            if (GameManager.Instance.gameSpeed < 0)
                GameManager.Instance.gameSpeed = 0;
            
            yield return new WaitForSeconds(0.01f);
        }

        StartCoroutine(OffloadPassengers());
    }

    private IEnumerator LoadPassengers()
    {
        int passengers = Random.Range(minPassengers, maxPassengers);
        float passengerDelay = loadingDuration / passengers;
        
        for (int i = 0; i < passengers; i++)
        {
            if (GameManager.Instance.passengers == 10)
            {
                passengerDelay = 0f;
            }
            
            if (GameManager.Instance.passengers < GameManager.Instance.maxPassengers)
            {
                GameManager.Instance.passengers++;
            }
            
            yield return new WaitForSeconds(passengerDelay);
        }
        
        GameManager.Instance.atStop = false;
        Timer.Instance.StartTimer();
        AudioManager.Instance.Play("Sharp");
    }
    
    private IEnumerator OffloadPassengers()
    {
        if (GameManager.Instance.passengers > 0)
        {
            for (int i = 0; i < GameManager.Instance.passengers; i++)
            {
                if (GameManager.Instance.passengers > 0)
                {
                    GameManager.Instance.passengers--;
                    GameManager.Instance.score += 10;
                }
                yield return new WaitForSeconds(loadingDuration/ GameManager.Instance.passengers);
            }
        }
        
        StartCoroutine(LoadPassengers());
    }
}
