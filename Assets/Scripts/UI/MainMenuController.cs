using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Main menu: play, settings (volume sliders), how to play and quit.</summary>
public class MainMenuController : MonoBehaviour
{
    private const string MobileControlsText =
        "GAS (right) : Accelerate\n\n" +
        "Arrows (left) : Change lanes\n\n" +
        "BRAKE (right) : Brake\n\n\n" +
        "HOOT : hoot\n\n" +
        "II : pause";

    [SerializeField] private GameObject mainMenu;
    [SerializeField] private GameObject settingsMenu;
    [SerializeField] private GameObject howToPlayMenu;

    [SerializeField] private Slider menuVolumeSlider;
    [SerializeField] private Slider radioVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [SerializeField] private TextMeshProUGUI menuValueText;
    [SerializeField] private TextMeshProUGUI radioValueText;
    [SerializeField] private TextMeshProUGUI sfxValueText;

    [Tooltip("Optional. Hidden on platforms where apps shouldn't quit themselves (iOS, WebGL). Found automatically if empty.")]
    [SerializeField] private GameObject quitButton;

    private void Start()
    {
        BindSlider(menuVolumeSlider, menuValueText, SoundCategory.Music);
        BindSlider(radioVolumeSlider, radioValueText, SoundCategory.Radio);
        BindSlider(sfxVolumeSlider, sfxValueText, SoundCategory.Sfx);

        ConfigureForPlatform();
        ShowOnly(mainMenu);
    }

    private static void BindSlider(Slider slider, TextMeshProUGUI label, SoundCategory category)
    {
        if (slider == null) return;

        float value = AudioManager.GetCategoryVolume(category);
        slider.SetValueWithoutNotify(value);
        SetPercent(label, value);

        // Event driven: the original rewrote every label each frame and every volume each physics step.
        slider.onValueChanged.AddListener(v =>
        {
            AudioManager.SetCategoryVolume(category, v);
            SetPercent(label, v);
        });
    }

    private static void SetPercent(TextMeshProUGUI label, float value)
    {
        if (label != null) label.SetText("{0}%", Mathf.Round(value * 100f));
    }

    private void ConfigureForPlatform()
    {
        if (quitButton == null)
        {
            Transform found = mainMenu != null ? mainMenu.transform.Find("QuitGame") : null;
            if (found != null) quitButton = found.gameObject;
        }

        bool canQuit = Application.platform != RuntimePlatform.IPhonePlayer &&
                       Application.platform != RuntimePlatform.WebGLPlayer;
        if (quitButton != null) quitButton.SetActive(canQuit);

        if (TaxiInput.UseTouchControls) ShowTouchInstructions();
    }

    /// <summary>Swap the keyboard help in How To Play for the touch layout.</summary>
    private void ShowTouchInstructions()
    {
        if (howToPlayMenu == null) return;

        foreach (TextMeshProUGUI text in howToPlayMenu.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            string content = text.text.Trim();
            if (content.StartsWith("W :"))
            {
                text.text = MobileControlsText;
            }
            else if (content.Length == 1 && "WASD".Contains(content))
            {
                // Hide the keyboard key-cap images.
                if (text.transform.parent != null) text.transform.parent.gameObject.SetActive(false);
            }
        }
    }

    private void ShowOnly(GameObject menu)
    {
        if (mainMenu != null) mainMenu.SetActive(menu == mainMenu);
        if (settingsMenu != null) settingsMenu.SetActive(menu == settingsMenu);
        if (howToPlayMenu != null) howToPlayMenu.SetActive(menu == howToPlayMenu);
    }

    // ---------------------------------------------------------------- Buttons (wired in the scene)

    public void OnPlayButtonClicked()
    {
        AudioManager.Play("Button Select");
        SceneManager.LoadScene("UpgradeMenu");
    }

    public void OnSettingsButtonClicked()
    {
        AudioManager.Play("Button Select");
        ShowOnly(settingsMenu);
    }

    public void OnExitButtonClicked()
    {
        AudioManager.Play("Button Select");
        SaveSystem.Flush();
        Application.Quit();
    }

    public void OnBackButtonClicked()
    {
        AudioManager.Play("Button Select");
        SaveSystem.Flush();
        ShowOnly(mainMenu);
    }

    public void OnHowToPlayButtonClicked()
    {
        AudioManager.Play("Button Select");
        ShowOnly(howToPlayMenu);
    }
}
