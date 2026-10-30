using TMPro;
using UnityEngine;

/// <summary>
/// On-screen touch controls, built entirely from code so no scene setup is needed:
/// steering arrows bottom-left, gas and brake pedals bottom-right, a horn button and a pause button.
/// Created by <see cref="UIController"/> when <see cref="TaxiInput.UseTouchControls"/> is true.
/// </summary>
public class MobileControls : MonoBehaviour
{
    private const float Margin = 50f;
    private const float Gap = 30f;

    private static readonly Color LabelColor = new Color(1f, 1f, 1f, 0.9f);

    private CanvasGroup _group;
    private TouchButton[] _buttons;

    public static MobileControls Create(TMP_FontAsset font)
    {
        Canvas canvas = RuntimeUI.CreateOverlayCanvas("MobileControls", 50);
        var controls = canvas.gameObject.AddComponent<MobileControls>();
        controls.Build(canvas.transform, font);
        return controls;
    }

    private void Build(Transform root, TMP_FontAsset font)
    {
        _group = gameObject.AddComponent<CanvasGroup>();

        RectTransform safe = RuntimeUI.Stretch(RuntimeUI.CreateRect("SafeArea", root));
        safe.gameObject.AddComponent<SafeAreaFitter>();

        // Steering (bottom-left).
        TouchButton left = CreateButton(safe, "Left", RuntimeUI.RoundedRect, true,
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(Margin, Margin), new Vector2(230f, 230f));
        AddIcon(left.transform, RuntimeUI.Triangle, flip: true);

        TouchButton right = CreateButton(safe, "Right", RuntimeUI.RoundedRect, true,
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(Margin + 230f + Gap, Margin), new Vector2(230f, 230f));
        AddIcon(right.transform, RuntimeUI.Triangle, flip: false);

        // Pedals (bottom-right): tall gas pedal on the outside, brake next to it.
        TouchButton gas = CreateButton(safe, "Gas", RuntimeUI.RoundedRect, true,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-Margin, Margin), new Vector2(230f, 320f));
        AddLabel(gas.transform, "GAS", font, 46f);

        TouchButton brake = CreateButton(safe, "Brake", RuntimeUI.RoundedRect, true,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-Margin - 230f - Gap, Margin), new Vector2(200f, 230f));
        AddLabel(brake.transform, "BRAKE", font, 38f);

        TouchButton horn = CreateButton(safe, "Horn", RuntimeUI.Circle, false,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-Margin - 230f - Gap - 25f, Margin + 230f + Gap), new Vector2(150f, 150f));
        horn.slideToActivate = false;
        AddLabel(horn.transform, "HOOT", font, 32f);

        // Pause (top-right, below the speedometer).
        TouchButton pause = CreateButton(safe, "Pause", RuntimeUI.Circle, false,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -170f), new Vector2(110f, 110f));
        pause.slideToActivate = false;
        AddLabel(pause.transform, "II", font, 44f);

        left.HeldChanged += held => TaxiInput.VirtualLeft = held;
        right.HeldChanged += held => TaxiInput.VirtualRight = held;
        gas.HeldChanged += held => TaxiInput.VirtualThrottle = held;
        brake.HeldChanged += held => TaxiInput.VirtualBrake = held;
        horn.Pressed += TaxiInput.QueueHorn;
        pause.Pressed += () =>
        {
            AudioManager.Play("Button Select");
            if (GameManager.Instance != null) GameManager.Instance.SetPaused(true);
        };

        _buttons = new[] { left, right, gas, brake, horn, pause };
    }

    private void Start()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        gm.PauseChanged += OnPauseChanged;
        gm.GameOverStarted += OnGameOver;
    }

    private void OnDestroy()
    {
        TaxiInput.ResetVirtual();

        GameManager gm = GameManager.Instance;
        if (gm == null) return;
        gm.PauseChanged -= OnPauseChanged;
        gm.GameOverStarted -= OnGameOver;
    }

    private void OnPauseChanged(bool paused) => SetVisible(!paused);

    private void OnGameOver(GameOverReason reason) => SetVisible(false);

    public void SetVisible(bool visible)
    {
        _group.alpha = visible ? 1f : 0f;
        _group.interactable = visible;
        _group.blocksRaycasts = visible;

        if (visible) return;
        foreach (TouchButton button in _buttons) button.ReleaseAll();
        TaxiInput.ResetVirtual();
    }

    private static TouchButton CreateButton(Transform parent, string name, Sprite sprite, bool sliced,
        Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var image = RuntimeUI.CreateImage(name, parent, sprite, Color.white, sliced);
        RuntimeUI.Place(image.rectTransform, anchor, pivot, position, size);
        return image.gameObject.AddComponent<TouchButton>();
    }

    private static void AddIcon(Transform button, Sprite sprite, bool flip)
    {
        var icon = RuntimeUI.CreateImage("Icon", button, sprite, LabelColor);
        icon.raycastTarget = false;
        RuntimeUI.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));
        if (flip) icon.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
    }

    private static void AddLabel(Transform button, string text, TMP_FontAsset font, float size)
    {
        TextMeshProUGUI label = RuntimeUI.CreateText("Label", button, text, size, font, LabelColor);
        RuntimeUI.Stretch(label.rectTransform);
    }
}
