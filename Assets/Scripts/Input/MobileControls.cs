using TMPro;
using UnityEngine;

/// <summary>
/// On-screen touch controls: steering arrows, gas and brake pedals, a horn and a pause button.
///
/// Recommended: use Tools > Mzansi Rush > Create Editable Game UI once in the GameLoop scene. That puts the controls
/// in your UI canvas as normal objects you can move, resize and restyle (swap the sprites, colours, labels...),
/// and assigns them to the UIController. If none are assigned, UIController builds a default set at runtime.
///
/// They show on phones and tablets, and in the editor's Device Simulator. Tick <see cref="showInEditor"/> to also
/// see them in the normal Game view (they work with the mouse).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class MobileControls : MonoBehaviour
{
    private const float Margin = 50f;
    private const float Gap = 30f;

    public static readonly Color DefaultButtonColor = new Color(0f, 0f, 0f, 0.35f);
    public static readonly Color DefaultLabelColor = new Color(1f, 1f, 1f, 0.9f);

    [Header("Buttons")]
    [SerializeField] private TouchButton leftButton;
    [SerializeField] private TouchButton rightButton;
    [SerializeField] private TouchButton gasButton;
    [SerializeField] private TouchButton brakeButton;
    [SerializeField] private TouchButton hornButton;
    [SerializeField] private TouchButton pauseButton;

    [Header("Visibility")]
    [Tooltip("Also show the controls in the normal Game view (they always show on phones and in the Device Simulator).")]
    [SerializeField] private bool showInEditor;

    private CanvasGroup _group;
    private bool _wired;
    private bool _subscribed;

    public bool ShouldShow => TaxiInput.UseTouchControls || (Application.isEditor && showInEditor);

    private void Awake()
    {
        if (!TryGetComponent(out _group)) _group = gameObject.AddComponent<CanvasGroup>();
    }

    private void Start()
    {
        if (!ShouldShow)
        {
            gameObject.SetActive(false);
            return;
        }

        Wire();

        GameManager gm = GameManager.Instance;
        if (gm != null)
        {
            gm.PauseChanged += OnPauseChanged;
            gm.GameOverStarted += OnGameOver;
            _subscribed = true;
        }

        SetVisible(true);
    }

    private void OnDestroy()
    {
        TaxiInput.ResetVirtual();

        GameManager gm = GameManager.Instance;
        if (!_subscribed || gm == null) return;
        gm.PauseChanged -= OnPauseChanged;
        gm.GameOverStarted -= OnGameOver;
    }

    /// <summary>Used by the builder below and the editor setup tool.</summary>
    public void SetButtons(TouchButton left, TouchButton right, TouchButton gas, TouchButton brake,
        TouchButton horn, TouchButton pause)
    {
        leftButton = left;
        rightButton = right;
        gasButton = gas;
        brakeButton = brake;
        hornButton = horn;
        pauseButton = pause;
    }

    private void Wire()
    {
        if (_wired) return;
        _wired = true;

        if (leftButton != null) leftButton.HeldChanged += held => TaxiInput.VirtualLeft = held;
        if (rightButton != null) rightButton.HeldChanged += held => TaxiInput.VirtualRight = held;
        if (gasButton != null) gasButton.HeldChanged += held => TaxiInput.VirtualThrottle = held;
        if (brakeButton != null) brakeButton.HeldChanged += held => TaxiInput.VirtualBrake = held;
        if (hornButton != null) hornButton.Pressed += TaxiInput.QueueHorn;
        if (pauseButton != null)
        {
            pauseButton.Pressed += () =>
            {
                AudioManager.Play("Button Select");
                if (GameManager.Instance != null) GameManager.Instance.SetPaused(true);
            };
        }
    }

    private void OnPauseChanged(bool paused) => SetVisible(!paused);

    private void OnGameOver(GameOverReason reason) => SetVisible(false);

    public void SetVisible(bool visible)
    {
        _group.alpha = visible ? 1f : 0f;
        _group.interactable = visible;
        _group.blocksRaycasts = visible;

        if (visible) return;
        foreach (TouchButton button in new[] { leftButton, rightButton, gasButton, brakeButton, hornButton, pauseButton })
        {
            if (button != null) button.ReleaseAll();
        }
        TaxiInput.ResetVirtual();
    }

    // ---------------------------------------------------------------- Building the default layout

    /// <summary>Default controls on their own overlay canvas (used when none are placed in the scene).</summary>
    public static MobileControls CreateRuntime(TMP_FontAsset font)
    {
        Canvas canvas = RuntimeUI.CreateOverlayCanvas("MobileControls Canvas", 50);
        return Build(canvas.transform, font, RuntimeUI.RoundedRect, RuntimeUI.Circle, RuntimeUI.Triangle);
    }

    /// <summary>
    /// Builds the default layout under <paramref name="canvasRoot"/> (a Canvas or a full-screen child of one).
    /// Works in edit mode too, which is how the editor setup tool creates scene objects.
    /// </summary>
    public static MobileControls Build(Transform canvasRoot, TMP_FontAsset font, Sprite rounded, Sprite circle, Sprite arrow)
    {
        RectTransform root = RuntimeUI.Stretch(RuntimeUI.CreateRect("MobileControls", canvasRoot));
        root.gameObject.AddComponent<CanvasGroup>();

        RectTransform safe = RuntimeUI.Stretch(RuntimeUI.CreateRect("SafeArea", root));
        safe.gameObject.AddComponent<SafeAreaFitter>();

        // Steering (bottom-left).
        TouchButton left = CreateButton(safe, "Left", rounded, true,
            new Vector2(0f, 0f), new Vector2(Margin, Margin), new Vector2(230f, 230f));
        AddIcon(left.transform, arrow, flip: true);

        TouchButton right = CreateButton(safe, "Right", rounded, true,
            new Vector2(0f, 0f), new Vector2(Margin + 230f + Gap, Margin), new Vector2(230f, 230f));
        AddIcon(right.transform, arrow, flip: false);

        // Pedals (bottom-right): tall gas pedal on the outside, brake next to it, horn above the brake.
        TouchButton gas = CreateButton(safe, "Gas", rounded, true,
            new Vector2(1f, 0f), new Vector2(-Margin, Margin), new Vector2(230f, 320f));
        AddLabel(gas.transform, "GAS", font, 46f);

        TouchButton brake = CreateButton(safe, "Brake", rounded, true,
            new Vector2(1f, 0f), new Vector2(-Margin - 230f - Gap, Margin), new Vector2(200f, 230f));
        AddLabel(brake.transform, "BRAKE", font, 38f);

        TouchButton horn = CreateButton(safe, "Horn", circle, false,
            new Vector2(1f, 0f), new Vector2(-Margin - 230f - Gap - 25f, Margin + 230f + Gap), new Vector2(150f, 150f));
        horn.slideToActivate = false;
        AddLabel(horn.transform, "HOOT", font, 32f);

        // Pause (top-right, below the speedometer).
        TouchButton pause = CreateButton(safe, "Pause", circle, false,
            new Vector2(1f, 1f), new Vector2(-40f, -170f), new Vector2(110f, 110f));
        pause.slideToActivate = false;
        AddLabel(pause.transform, "II", font, 44f);

        var controls = root.gameObject.AddComponent<MobileControls>();
        controls.SetButtons(left, right, gas, brake, horn, pause);
        return controls;
    }

    private static TouchButton CreateButton(Transform parent, string name, Sprite sprite, bool sliced,
        Vector2 corner, Vector2 position, Vector2 size)
    {
        var image = RuntimeUI.CreateImage(name, parent, sprite, DefaultButtonColor, sliced);
        RuntimeUI.Place(image.rectTransform, corner, corner, position, size);
        return image.gameObject.AddComponent<TouchButton>();
    }

    private static void AddIcon(Transform button, Sprite sprite, bool flip)
    {
        var icon = RuntimeUI.CreateImage("Icon", button, sprite, DefaultLabelColor);
        icon.raycastTarget = false;
        RuntimeUI.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));
        if (flip) icon.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
    }

    private static void AddLabel(Transform button, string text, TMP_FontAsset font, float size)
    {
        TextMeshProUGUI label = RuntimeUI.CreateText("Label", button, text, size, font, DefaultLabelColor);
        RuntimeUI.Stretch(label.rectTransform);
    }
}
