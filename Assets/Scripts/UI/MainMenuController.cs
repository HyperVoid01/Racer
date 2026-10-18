using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private GameObject mainMenu;
    [SerializeField] private GameObject settingsMenu;
    [SerializeField] private GameObject howToPlayMenu;
    
    [SerializeField] private Slider menuVolumeSlider;
    [SerializeField] private Slider radioVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [SerializeField] private TextMeshProUGUI menuValueText;
    [SerializeField] private TextMeshProUGUI radioValueText;
    [SerializeField] private TextMeshProUGUI sfxValueText;

    public void Start()
    {
        menuVolumeSlider.value = AudioManager.Instance.menuVolume;
        radioVolumeSlider.value = AudioManager.Instance.radioVolume;
        sfxVolumeSlider.value = AudioManager.Instance.sfxVolume;
    }
    
    public void Update()
    {
        UpdateVolumeSlider();
    }

    public void FixedUpdate()
    {
        UpdateVolume();
    }

    public void OnPlayButtonClicked()
    {
        AudioManager.Instance.Play("Button Select");
        SceneManager.LoadScene("UpgradeMenu");
    }

    public void OnSettingsButtonClicked()
    {
        AudioManager.Instance.Play("Button Select");
        mainMenu.SetActive(false);
        settingsMenu.SetActive(true);
    }
    
    public void OnExitButtonClicked()
    {
        AudioManager.Instance.Play("Button Select");
        Application.Quit();
    }

    public void OnBackButtonClicked()
    {
        AudioManager.Instance.Play("Button Select");
        mainMenu.SetActive(true);
        settingsMenu.SetActive(false);
        howToPlayMenu.SetActive(false);
    }

    public void OnHowToPlayButtonClicked()
    {
        AudioManager.Instance.Play("Button Select");
        mainMenu.SetActive(false);
        settingsMenu.SetActive(false);
        howToPlayMenu.SetActive(true);
    }

    public void UpdateVolumeSlider()
    {
        menuValueText.text = (menuVolumeSlider.value * 100).ToString("0") + "%";
        radioValueText.text = (radioVolumeSlider.value * 100).ToString("0") + "%";
        sfxValueText.text = (sfxVolumeSlider.value * 100).ToString("0") + "%";
        
        AudioManager.Instance.menuVolume = menuVolumeSlider.value;
        AudioManager.Instance.radioVolume = radioVolumeSlider.value;
        AudioManager.Instance.sfxVolume = sfxVolumeSlider.value;
    }

    public void UpdateVolume()
    {
        AudioManager.Instance.ChangeVolume("Main Menu Music", AudioManager.Instance.menuVolume);
        
        AudioManager.Instance.ChangeVolume("Track1", AudioManager.Instance.radioVolume);
        AudioManager.Instance.ChangeVolume("Track2", AudioManager.Instance.radioVolume);
        AudioManager.Instance.ChangeVolume("Track3", AudioManager.Instance.radioVolume);
        AudioManager.Instance.ChangeVolume("Track4", AudioManager.Instance.radioVolume);
        AudioManager.Instance.ChangeVolume("Track5", AudioManager.Instance.radioVolume);
        
        AudioManager.Instance.ChangeVolume("Button Select", AudioManager.Instance.sfxVolume);
        AudioManager.Instance.ChangeVolume("Taxi Accelerating", AudioManager.Instance.sfxVolume);
        AudioManager.Instance.ChangeVolume("Taxi braking", AudioManager.Instance.sfxVolume);
        AudioManager.Instance.ChangeVolume("Taxi Hooting", AudioManager.Instance.sfxVolume);
        AudioManager.Instance.ChangeVolume("Taxi Idle", AudioManager.Instance.sfxVolume);
        AudioManager.Instance.ChangeVolume("Taxi crash", AudioManager.Instance.sfxVolume);
        AudioManager.Instance.ChangeVolume("Hey taxi", AudioManager.Instance.sfxVolume);
        AudioManager.Instance.ChangeVolume("Whistle", AudioManager.Instance.sfxVolume);
        AudioManager.Instance.ChangeVolume("Mama Crash", AudioManager.Instance.sfxVolume);
        AudioManager.Instance.ChangeVolume("Pothole", AudioManager.Instance.sfxVolume);
        AudioManager.Instance.ChangeVolume("Stop", AudioManager.Instance.sfxVolume);
        AudioManager.Instance.ChangeVolume("Sharp", AudioManager.Instance.sfxVolume);
    }
}
