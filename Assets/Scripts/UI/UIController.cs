using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// In-game HUD, pause screen and game over screen.
/// Text only updates when its value changes, and uses TMP's SetText with numeric arguments,
/// so the HUD produces no garbage per frame (important on mobile).
///
/// The run summary, score popups and touch controls are normal scene objects you can lay out yourself:
/// run Tools > Mzansi Rush > Create Editable Game UI once in this scene to create and assign them.
/// Anything left unassigned falls back to a default built at runtime.
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

    [Header("Run summary (game over screen) - optional")]
    [SerializeField] private TextMeshProUGUI gameOverReasonText;
    [SerializeField] private TextMeshProUGUI fareText;
    [SerializeField] private TextMeshProUGUI bestText;
    [Tooltip("Optional object shown only on a new high score (e.g. a badge or banner).")]
    [SerializeField] private GameObject newBestBadge;
    [SerializeField] private string crashedMessage = "You crashed!";
    [SerializeField] private string gaveUpMessage = "Your passengers gave up!";
    [Tooltip("{0} is replaced with the fare.")]
    [SerializeField] private string fareFormat = "FARE  R{0}";
    [Tooltip("{0} is replaced with the best score.")]
    [SerializeField] private string bestFormat = "BEST  R{0}";
    [SerializeField] private string newBestMessage = "NEW BEST!";

    [Header("Score popups - optional")]
    [Tooltip("A disabled text placed where popups should start. Copies of it float up when the score changes.")]
    [SerializeField] private TextMeshProUGUI scorePopupTemplate;
    [SerializeField] private int popupPoolSize = 6;
    [SerializeField] private float popupRise = 70f;
    [SerializeField] private float popupDuration = 0.8f;
    [Tooltip("Where message popups (e.g. the horn's \"Move!\") start, relative to the score popups.")]
    [SerializeField] private Vector2 messagePopupOffset = new Vector2(0f, -55f);

    [Header("Touch controls - optional")]
    [SerializeField] private MobileControls mobileControls;

    [Header("Feedback colours")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color warningColor = new Color(1f, 0.8f, 0.1f);
    [SerializeField] private Color lateColor = new Color(1f, 0.25f, 0.2f);
    [SerializeField] private Color gainColor = new Color(0.35f, 1f, 0.45f);

    private int _shownKmh = int.MinValue;
    private int _shownTimerCentis = int.MinValue;
    private int _shownWholeSeconds = int.MinValue;
    private bool? _timerVisible;
    private int _timerColorState = -1;

    private TextMeshProUGUI[] _popups;
    private Vector2 _popupStart;
    private int _nextPopup;
    private MobileControls _runtimeControls;
    private TextMeshProUGUI _fallbackSummary;

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
        if (newBestBadge != null) newBestBadge.SetActive(false);

        BuildScorePopups();

        // Scene controls manage their own visibility; only build defaults when none were placed.
        if (mobileControls == null && TaxiInput.UseTouchControls)
        {
            _runtimeControls = MobileControls.CreateRuntime(scoreText != null ? scoreText.font : null);
        }

        scoreText.SetText("SCORE\nR{0}", gm.Score);
        OnPassengersChanged(gm.Passengers);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_runtimeControls != null) Destroy(_runtimeControls.transform.root.gameObject);

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
        UpdateNotice();
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

    private void UpdateNotice()
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
        TextMeshProUGUI template = scorePopupTemplate;

        if (template == null)
        {
            // Default: just to the right of the score.
            RectTransform anchor = scoreText.transform.parent as RectTransform;
            if (anchor == null) return;

            template = RuntimeUI.CreateText("ScorePopup", anchor, string.Empty,
                scoreText.fontSize, scoreText.font, gainColor, TextAlignmentOptions.Left);
            RuntimeUI.Place(template.rectTransform, new Vector2(1f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(20f, 0f), new Vector2(220f, 60f));
        }

        template.gameObject.SetActive(false);
        _popupStart = template.rectTransform.anchoredPosition;

        _popups = new TextMeshProUGUI[Mathf.Max(1, popupPoolSize)];
        _popups[0] = template;
        for (int i = 1; i < _popups.Length; i++)
        {
            _popups[i] = Instantiate(template, template.transform.parent, false);
            _popups[i].gameObject.SetActive(false);
        }
    }

    private void ShowPopup(int delta)
    {
        TextMeshProUGUI popup = NextPopup();
        if (popup == null) return;

        popup.SetText(delta > 0 ? "+R{0}" : "-R{0}", Mathf.Abs(delta));
        popup.color = delta > 0 ? gainColor : lateColor;
        StartPopup(popup, _popupStart);
    }

    /// <summary>Floats a short message (e.g. "Move!") up just below where score popups appear.</summary>
    public void ShowMessage(string message) => ShowMessage(message, warningColor);

    public void ShowMessage(string message, Color color)
    {
        if (string.IsNullOrEmpty(message)) return;

        TextMeshProUGUI popup = NextPopup();
        if (popup == null) return;

        popup.text = message;
        popup.color = color;
        StartPopup(popup, _popupStart + messagePopupOffset);
    }

    private TextMeshProUGUI NextPopup()
    {
        if (_popups == null || !isActiveAndEnabled) return null;

        TextMeshProUGUI popup = _popups[_nextPopup];
        _nextPopup = (_nextPopup + 1) % _popups.Length;
        return popup;
    }

    private void StartPopup(TextMeshProUGUI popup, Vector2 start)
    {
        popup.rectTransform.anchoredPosition = start;
        popup.gameObject.SetActive(true);
        StartCoroutine(AnimatePopup(popup, start));
    }

    private IEnumerator AnimatePopup(TextMeshProUGUI popup, Vector2 start)
    {
        RectTransform rect = popup.rectTransform;
        Color baseColor = popup.color;
        float t = 0f;
        while (t < popupDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / popupDuration);
            rect.anchoredPosition = start + new Vector2(0f, popupRise * (1f - (1f - k) * (1f - k)));
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
        bool isNewBest = gm.LastRunWasHighScore;
        string reason = gm.LastGameOverReason == GameOverReason.Crashed ? crashedMessage : gaveUpMessage;
        string fare = string.Format(fareFormat, gm.Score);
        string best = isNewBest ? newBestMessage : string.Format(bestFormat, Statistics.Instance.HighScore);

        if (newBestBadge != null) newBestBadge.SetActive(isNewBest);

        bool hasSceneSummary = gameOverReasonText != null || fareText != null || bestText != null;
        if (hasSceneSummary)
        {
            if (gameOverReasonText != null) gameOverReasonText.text = reason;
            if (fareText != null) fareText.text = fare;
            if (bestText != null)
            {
                bestText.text = best;
                if (isNewBest) UIPunch.Punch(this, bestText.transform, 0.3f, 0.4f);
            }
            return;
        }

        // Default when nothing is placed in the scene: one line between the title and the buttons.
        if (_fallbackSummary == null) _fallbackSummary = CreateFallbackSummary();
        if (_fallbackSummary != null) _fallbackSummary.text = $"{reason}\n{fare}     {best}";
    }

    private TextMeshProUGUI CreateFallbackSummary()
    {
        TextMeshProUGUI title = deathScreen.GetComponentInChildren<TextMeshProUGUI>(true);
        if (title == null) return null;

        TextMeshProUGUI summary = RuntimeUI.CreateText("RunSummary", title.transform.parent, string.Empty, 36f,
            title.font, Color.white);
        RuntimeUI.Place(summary.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 158f), new Vector2(1000f, 100f));
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