using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class Timer : MonoBehaviour
{
    [SerializeField] private float timeLimit;
    public float timer;
    public bool timerActive = false;
    private bool _isLate = false;
    [SerializeField] private int latePenaltyFee = 2;
    [SerializeField] private float latePenaltyInterval = 1f;
    [SerializeField] private float maxPenaltyTime = 20f;
    
    public static Timer Instance;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        if (timerActive)
        {
            timer -= Time.deltaTime;
        }

        if (timerActive && timer < 0 && !_isLate)
        {
            _isLate = true;
            StartCoroutine(DecreaseScore());
        }

        if (timer <= -maxPenaltyTime)
        {
            GameManager.Instance.gameOver = true;
        }

        if (GameManager.Instance.gameOver)
        {
            StopTimer();
        }
    }

    public void StartTimer()
    {
        timerActive = true;
        _isLate = false;
        timer = timeLimit;
    }

    public void StopTimer()
    {
        StopAllCoroutines();
        timerActive = false;
    }

    private IEnumerator DecreaseScore()
    {
        while (true)
        {
            if (GameManager.Instance.score > 0)
            {
                GameManager.Instance.score -= latePenaltyFee;
            }

            if (GameManager.Instance.score < 0)
                GameManager.Instance.score = 0;
            
            yield return new WaitForSeconds(latePenaltyInterval);
        }
    }
}
