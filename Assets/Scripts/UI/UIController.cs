using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// In-game HUD, pause screen and game over screen.
/// Text only updates when its value changes, and uses TMP's SetText with numeric arguments,
/// so the HUD produces no garbage per frame (important on mobile).
/// </summary>
public class UIController : MonoBehaviour
{
    public static UIController Instance { get; private set; }

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

    [Header("Feedback colours")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color warningColor = new Color(1f, 0.8f, 0.1f);
    [SerializeField] private Color lateColor = new Color(1f, 0.25f, 0.2f);
    [SerializeField] private Color gainColor = new Color(0.35f, 1f, 0.45f);

    [Header("Score popups")]
    [SerializeField] private int popupPoolSize = 6;
    [SerializeField] private float popupRise = 70f;
    [SerializeField] private float popupDuration = 0.8f;

    private int _shownKmh = int.MinValue;
    private int _shownTimerCentis = int.MinValue;
    private int _shownWholeSeconds = int.MinValue;
    private bool? _timerVisible;
    private int _timerColorState = -1;

    private TextMeshProUGUI _summaryText;
    private TextMeshProUGUI[] _popups;
    private int _nextPopup;
    private MobileControls _mobileControls;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        GameManager gm = GameManager.Instance;
        gm.ScoreChanged += OnScoreChanged;
        gm.PassengersChanged += OnPassengersChanged;
        gm.PauseChanged += OnPauseChanged;
        gm.GameOverStarted += OnGameOverStarted;
        gm.GameOverScreenRequested += ShowDeathScreen;

        hud.SetActive(true);
        pauseScreen.SetActive(false);
        deathScreen.SetActive(false);
        if (radio != null) radio.SetActive(UpgradeManager.Instance.RadioPurchased);

        BuildScorePopups();

        if (TaxiInput.UseTouchControls)
        {
            _mobileControls = MobileControls.Create(scoreText != null ? scoreText.font : null);
        }

        scoreText.SetText("SCORE\nR{0}", gm.Score);
        OnPassengersChanged(gm.Passengers);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_mobileControls != null) Destroy(_mobileControls.gameObject);

        GameManager gm = GameManager.Instance;
        if (gm == null) return;
        gm.ScoreChanged -= OnScoreChanged;
        gm.PassengersChanged -= OnPassengersChanged;
        gm.PauseChanged -= OnPauseChanged;
        gm.GameOverStarted -= OnGameOverStarted;
        gm.GameOverScreenRequested -= ShowDeathScreen;
    }

    private void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm.IsGameOver) return;

        int kmh = GameManager.ToKmh(gm.GameSpeed);
        if (kmh != _shownKmh)
        {
            _shownKmh = kmh;
            speedText.SetText("SPEED\n{0} kmh", kmh);
        }

        UpdateTimer(gm);
        UpdateNotice(gm);
    }

    // ---------------------------------------------------------------- HUD

    private void UpdateTimer(GameManager gm)
    {
        Timer t = Timer.Instance;
        bool showTimer = gm.Passengers > 0 && t != null;
        if (_timerVisible != showTimer)
        {
            _timerVisible = showTimer;
            timer.SetActive(showTimer);
            noPassengersNoticeText.gameObject.SetActive(!showTimer);
        }

        if (!showTimer || !t.IsActive) return;

        float time = t.TimeRemaining;
        int centis = Mathf.RoundToInt(time * 100f);
        if (centis != _shownTimerCentis)
        {
            _shownTimerCentis = centis;
            timerText.SetText("{0:2}", time);
        }

        int colorState = t.IsLate ? 2 : t.IsWarning ? 1 : 0;
        if (colorState != _timerColorState)
        {
            _timerColorState = colorState;
            UIPunch.SetRestColor(timerText, colorState == 2 ? lateColor : colorState == 1 ? warningColor : normalColor);
        }

        // Tick on every whole second while the clock is running low.
        int wholeSeconds = Mathf.CeilToInt(time);
        if (wholeSeconds != _shownWholeSeconds)
        {
            _shownWholeSeconds = wholeSeconds;
            if (colorState == 1) UIPunch.Punch(this, timerText.transform, 0.25f);
        }
    }

    private void UpdateNotice(GameManager gm)
    {
        if (!noPassengersNoticeText.gameObject.activeSelf) return;

        // Gentle pulse so new players notice they need to pick people up.
        Color c = noPassengersNoticeText.color;
        c.a = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
        noPassengersNoticeText.color = c;
    }

    private void OnScoreChanged(int score, int delta)
    {
        scoreText.SetText("SCORE\nR{0}", score);
        UIPunch.Punch(this, scoreText.transform, delta > 0 ? 0.2f : 0.1f);
        UIPunch.Flash(this, scoreText, delta > 0 ? gainColor : lateColor);
        ShowPopup(delta);
    }

    private void OnPassengersChanged(int passengers)
    {
        passengersText.SetText("PASSENGERS\n{0}/{1}", passengers, GameManager.Instance.MaxPassengers);
        if (passengers > 0) UIPunch.Punch(this, passengersText.transform, 0.12f);
    }

    // ---------------------------------------------------------------- Score popups

    private void BuildScorePopups()
    {
        RectTransform anchor = scoreText.transform.parent as RectTransform;
        if (anchor == null) return;

        _popups = new TextMeshProUGUI[popupPoolSize];
        for (int i = 0; i < popupPoolSize; i++)
        {
            TextMeshProUGUI popup = RuntimeUI.CreateText("ScorePopup", anchor, string.Empty,
                scoreText.fontSize, scoreText.font, gainColor, TextAlignmentOptions.Left);
            RuntimeUI.Place(popup.rectTransform, new Vector2(1f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(20f, 0f), new Vector2(220f, 60f));
            popup.gameObject.SetActive(false);
            _popups[i] = popup;
        }
    }

    private void ShowPopup(int delta)
    {
        if (_popups == null || _popups.Length == 0 || !isActiveAndEnabled) return;

        TextMeshProUGUI popup = _popups[_nextPopup];
        _nextPopup = (_nextPopup + 1) % _popups.Length;

        popup.SetText(delta > 0 ? "+R{0}" : "-R{0}", Mathf.Abs(delta));
        popup.color = delta > 0 ? gainColor : lateColor;
        popup.gameObject.SetActive(true);
        StartCoroutine(AnimatePopup(popup));
    }

    private IEnumerator AnimatePopup(TextMeshProUGUI popup)
    {
        RectTransform rect = popup.rectTransform;
        Color baseColor = popup.color;
        float t = 0f;
        while (t < popupDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / popupDuration);
            rect.anchoredPosition = new Vector2(20f, popupRise * (1f - (1f - k) * (1f - k)));
            popup.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f - k * k);
            yield return null;
        }
        popup.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- Screens

    private void OnPauseChanged(bool paused)
    {
        pauseScreen.SetActive(paused);
        hud.SetActive(!paused);
    }

    private void OnGameOverStarted(GameOverReason reason)
    {
        pauseScreen.SetActive(false);
    }

    public void ShowDeathScreen()
    {
        hud.SetActive(false);
        pauseScreen.SetActive(false);
        deathScreen.SetActive(true);

        UpdateSummary();
        UIPunch.Punch(this, deathScreen.transform, 0.06f, 0.3f);
    }

    private void UpdateSummary()
    {
        GameManager gm = GameManager.Instance;
        if (_summaryText == null) _summaryText = CreateSummaryText();
        if (_summaryText == null) return;

        string reason = gm.LastGameOverReason == GameOverReason.Crashed ? "You crashed!" : "Your passengers gave up!";
        string best = gm.LastRunWasHighScore
            ? "<color=#FFD21F>NEW BEST!</color>"
            : $"BEST  R{Statistics.Instance.HighScore}";

        _summaryText.text = $"{reason}\nFARE  R{gm.Score}     {best}";
    }

    private TextMeshProUGUI CreateSummaryText()
    {
        // Sits between the "GAME OVER" title and the buttons.
        TextMeshProUGUI title = deathScreen.GetComponentInChildren<TextMeshProUGUI>(true);
        if (title == null) return null;

        TextMeshProUGUI summary = RuntimeUI.CreateText("RunSummary", title.transform.parent, string.Empty, 36f,
            title.font, Color.white);
        RuntimeUI.Place(summary.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 158f), new Vector2(1000f, 100f));
        summary.richText = true;
        return summary;
    }

    // ---------------------------------------------------------------- Buttons (wired in the scene)

    public void OnPauseButtonClicked()
    {
        AudioManager.Play("Button Select");
        GameManager.Instance.SetPaused(true);
    }

    public void OnResumeButtonClicked()
    {
        AudioManager.Play("Button Select");
        GameManager.Instance.SetPaused(false);
    }

    public void OnMainMenuButtonClicked()
    {
        Time.timeScale = 1f;
        AudioManager.StopAll();
        AudioManager.Play("Button Select");
        SceneManager.LoadScene("UpgradeMenu");
        AudioManager.Play("Main Menu Music");
    }

    public void OnTryAgainButtonClicked()
    {
        Time.timeScale = 1f;
        AudioManager.Play("Button Select");
        SceneManager.LoadScene("GameLoop");
    }
}
