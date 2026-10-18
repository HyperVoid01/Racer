using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UIController : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI speedText;
    [SerializeField] TextMeshProUGUI scoreText;
    [SerializeField] TextMeshProUGUI passengersText;
    [SerializeField] GameObject timer;
    [SerializeField] TextMeshProUGUI timerText;
    [SerializeField] TextMeshProUGUI noPassengersNoticeText;
    [SerializeField] GameObject deathScreen;
    [SerializeField] GameObject pauseScreen;
    [SerializeField] GameObject hud;
    [SerializeField] GameObject radio;
    public bool _isPaused = false;
    
    
    public static UIController instance;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void Start()
    {
        if (UpgradeManager.Instance.radioPurchased == true)
        {
            radio.SetActive(true);
        }
    }

    private void Update()
    {
        if (GameManager.Instance.gameOver)
        {
            ShowDeathScreen();
        }
        
        if (Input.GetKeyDown(KeyCode.Escape) && !GameManager.Instance.gameOver)
        {
            AudioManager.Instance.Play("Button Select");
            _isPaused = !_isPaused;
            
            if (_isPaused)
            {
                AudioManager.Instance.PauseAll();
                
                pauseScreen.SetActive(true);
                hud.SetActive(false);
                Time.timeScale = 0f;
            }
            else if (!_isPaused)
            {
                OnResumeButtonClicked();
            }
        }
        
        speedText.text = $"SPEED\n{Convert.ToInt32(GameManager.Instance.gameSpeed + 20)} kmh";
        scoreText.text = $"SCORE\nR{GameManager.Instance.score}";

        if (GameManager.Instance.passengers == 0)
        {
            noPassengersNoticeText.gameObject.SetActive(true);
            timer.gameObject.SetActive(false);
        }
        else
        {
            noPassengersNoticeText.gameObject.SetActive(false);
            timer.gameObject.SetActive(true);
        }
        passengersText.text = $"PASSENGERS\n{GameManager.Instance.passengers}";
        
        UpdateTimer();
    }

    public void ShowDeathScreen()
    {
        hud.SetActive(false);
        pauseScreen.SetActive(false);
        deathScreen.SetActive(true);
    }

    public void OnResumeButtonClicked()
    {
        AudioManager.Instance.UnPauseAll();
        _isPaused = false;
        pauseScreen.SetActive(false);
        hud.SetActive(true);
        Time.timeScale = 1f;
    }

    public void OnMainMenuButtonClicked()
    {
        Time.timeScale = 1f;
        AudioManager.Instance.Play("Button Select");
        AudioManager.Instance.StopAll();
        //GameManager.Instance.gameOver = true;
        SceneManager.LoadScene("UpgradeMenu");
        AudioManager.Instance.Play("Main Menu Music");
    }

    public void OnTryAgainButtonClicked()
    {
        AudioManager.Instance.Play("Button Select");
        SceneManager.LoadScene("GameLoop");
    }

    private void UpdateTimer()
    {
        if (!Timer.Instance.timerActive) return;
        
        timerText.text = Timer.Instance.timer.ToString("0.00");

        if (Timer.Instance.timer > 0)
        {
            timerText.color = Color.white;
        }
        else if (Timer.Instance.timer < 0)
        {
            timerText.color = Color.red;
        }
    }
}
