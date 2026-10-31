using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Main menu: play, settings (volume sliders, reset progress), how to play and quit.</summary>
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

    [Header("Reset Progress")]
    [Tooltip("Optional. Leave empty to create one automatically from a copy of the Settings BACK button. " +
             "If you assign your own button, you don't need to add an OnClick for it.")]
    [SerializeField] private Button resetProgressButton;
    [Tooltip("Where the automatic button goes, relative to the BACK button.")]
    [SerializeField] private Vector2 autoResetButtonOffset = new Vector2(400f, 0f);
    [Tooltip("Tint of the automatic button, so it doesn't look like a normal menu button.")]
    [SerializeField] private Color autoResetButtonTint = new Color(1f, 0.55f, 0.55f, 1f);
    [SerializeField] private string resetLabel = "RESET PROGRESS";
    [SerializeField] private string confirmLabel = "TAP AGAIN TO CONFIRM";
    [SerializeField] private string doneLabel = "PROGRESS RESET";
    [Tooltip("Seconds the player has to tap a second time.")]
    [SerializeField] private float confirmWindow = 3f;
    [SerializeField] private float doneMessageTime = 1.5f;

    private TMP_Text _resetText;
    private float _confirmUntil = -1f;
    private float _doneUntil = -1f;

    private void Start()
    {
        BindSlider(menuVolumeSlider, menuValueText, SoundCategory.Music);
        BindSlider(radioVolumeSlider, radioValueText, SoundCategory.Radio);
        BindSlider(sfxVolumeSlider, sfxValueText, SoundCategory.Sfx);

        SetUpResetButton();
        ConfigureForPlatform();
        ShowOnly(mainMenu);
    }

    private void Update()
    {
        // Revert the reset button's label once its message has timed out.
        if (_confirmUntil > 0f && Time.unscaledTime > _confirmUntil)
        {
            _confirmUntil = -1f;
            SetResetLabel(resetLabel);
        }

        if (_doneUntil > 0f && Time.unscaledTime > _doneUntil)
        {
            _doneUntil = -1f;
            SetResetLabel(resetLabel);
        }
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

        CancelResetConfirmation();
    }

    // ---------------------------------------------------------------- Reset progress

    private void SetUpResetButton()
    {
        bool created = false;
        if (resetProgressButton == null)
        {
            resetProgressButton = CreateResetButton();
            created = resetProgressButton != null;
        }
        if (resetProgressButton == null) return;

        _resetText = resetProgressButton.GetComponentInChildren<TMP_Text>(true);
        if (_resetText != null)
        {
            // "TAP AGAIN TO CONFIRM" is longer than "BACK"; let it shrink to fit the same button.
            _resetText.enableAutoSizing = true;
            _resetText.fontSizeMax = _resetText.fontSize;
            _resetText.fontSizeMin = Mathf.Min(18f, _resetText.fontSize);
        }
        SetResetLabel(resetLabel);

        // A button assigned by hand may already call OnResetProgressButtonClicked from its OnClick list.
        // Only add the listener when there isn't one, or a single tap would count as two.
        if (created || resetProgressButton.onClick.GetPersistentEventCount() == 0)
        {
            resetProgressButton.onClick.AddListener(OnResetProgressButtonClicked);
        }
    }

    /// <summary>Copies the Settings BACK button so the new button matches the menu's style.</summary>
    private Button CreateResetButton()
    {
        if (settingsMenu == null) return null;

        Button template = null;
        foreach (Button button in settingsMenu.GetComponentsInChildren<Button>(true))
        {
            if (button.name.IndexOf("Back", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                template = button;
                break;
            }
        }
        if (template == null)
        {
            Debug.LogWarning("[MainMenu] No BACK button found in Settings to copy for Reset Progress. " +
                             "Assign a button to 'Reset Progress Button' on the MainMenuController.", this);
            return null;
        }

        Button copy = Instantiate(template, template.transform.parent);
        copy.name = "ResetProgressButton";
        // A fresh event drops the copied OnClick entry (which would have called OnBackButtonClicked).
        copy.onClick = new Button.ButtonClickedEvent();

        var rect = (RectTransform)copy.transform;
        rect.anchoredPosition = ((RectTransform)template.transform).anchoredPosition + autoResetButtonOffset;

        if (copy.targetGraphic != null) copy.targetGraphic.color *= autoResetButtonTint;
        return copy;
    }

    /// <summary>First tap asks for confirmation, a second tap within the time window resets.</summary>
    public void OnResetProgressButtonClicked()
    {
        AudioManager.Play("Button Select");

        bool confirming = _confirmUntil > 0f && Time.unscaledTime <= _confirmUntil;
        if (!confirming)
        {
            _doneUntil = -1f;
            _confirmUntil = Time.unscaledTime + confirmWindow;
            SetResetLabel(confirmLabel);
            return;
        }

        _confirmUntil = -1f;
        SaveSystem.ResetProgress();
        _doneUntil = Time.unscaledTime + doneMessageTime;
        SetResetLabel(doneLabel);
    }

    private void CancelResetConfirmation()
    {
        if (_confirmUntil < 0f && _doneUntil < 0f) return;
        _confirmUntil = -1f;
        _doneUntil = -1f;
        SetResetLabel(resetLabel);
    }

    private void SetResetLabel(string label)
    {
        if (_resetText != null) _resetText.text = label;
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