using UnityEngine;

public class Statistics : MonoBehaviour
{
    // SUBSYSTEM
    
    public int currency = 0;
    public int highScore = 0;
    
    public static Statistics Instance;

    public void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        
        DontDestroyOnLoad(gameObject);
        Instance = this;
    }
}
