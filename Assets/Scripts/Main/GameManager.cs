using UnityEngine;

public class GameManager : MonoBehaviour
{
    public bool gameOver = false;
    [SerializeField] public float gameSpeed = 1f;
    public bool atStop = false;
    public int passengers = 0;
    public int maxPassengers = 10;
    public int score = 0;
    
    public static GameManager Instance;

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
        AudioManager.Instance.StopAll();
    }

    private void Update()
    {
        if (gameOver && score > 0)
        {
            //Time.timeScale = 0f;
            UIController.instance.ShowDeathScreen();
            Statistics.Instance.currency += score;
            score = 0;

            if (score > Statistics.Instance.highScore)
            {
                Statistics.Instance.highScore = score;
            }
        }
    }
}
