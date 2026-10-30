using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Spins the garage/showroom taxi by dragging or swiping left/right anywhere on the screen,
/// or with A/D (and the left/right arrow keys). Releasing a drag flings the taxi with momentum.
/// Put it on the taxi model (or assign <see cref="target"/>). Uses unscaled time, so it works
/// even if Time.timeScale is 0.
/// </summary>
[DisallowMultipleComponent]
public class ShowroomSpin : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The object to spin. Defaults to this object.")]
    [SerializeField] private Transform target;

    [Tooltip("Flip this if dragging right spins the taxi the 'wrong' way for your camera.")]
    [SerializeField] private bool invertDirection = false;

    [Header("Drag / Swipe")]
    [Tooltip("Degrees of rotation for a drag across the full width of the screen.")]
    [SerializeField] private float degreesPerScreenWidth = 360f;

    [Tooltip("Pixels a press must move before it counts as a drag (stops taps from nudging the taxi).")]
    [SerializeField] private float dragThresholdPixels = 8f;

    [Tooltip("Ignore presses that start on a button, slider, toggle or scroll view.")]
    [SerializeField] private bool ignorePressesOnUIControls = true;

    [Header("Keyboard (A / D, arrow keys)")]
    [Tooltip("Spin speed in degrees per second while a key is held.")]
    [SerializeField] private float keySpinSpeed = 140f;

    [Tooltip("How quickly key spinning ramps up to full speed.")]
    [SerializeField] private float keyResponsiveness = 8f;

    [Header("Momentum")]
    [Tooltip("How quickly the spin slows down after you let go. Higher = stops sooner.")]
    [SerializeField] private float momentumDamping = 3.5f;

    [Tooltip("Maximum fling speed in degrees per second.")]
    [SerializeField] private float maxSpinSpeed = 900f;

    [Header("Idle Spin (optional)")]
    [Tooltip("Slow automatic spin when nobody is touching it. 0 = off.")]
    [SerializeField] private float idleSpinSpeed = 0f;

    [Tooltip("Seconds without input before the idle spin starts.")]
    [SerializeField] private float idleDelay = 3f;

    // Signed spin speed in "rightward" degrees per second (drag right / D = positive).
    private float velocity;
    private float timeSinceInput;

    private bool pointerWasDown;
    private bool pressBlocked;
    private bool dragging;
    private Vector2 pressStartPosition;
    private Vector2 lastPointerPosition;

    private static readonly List<RaycastResult> raycastHits = new List<RaycastResult>();

    public Transform Target => target;

    private void Awake()
    {
        if (target == null) target = transform;
    }

    private void OnDisable()
    {
        velocity = 0f;
        pointerWasDown = false;
        dragging = false;
        pressBlocked = false;
    }

    /// <summary>Stops any momentum (e.g. call it when the garage opens).</summary>
    public void StopSpin()
    {
        velocity = 0f;
        timeSinceInput = 0f;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f || target == null) return;

        bool hadInput = HandlePointer(dt);
        if (!dragging) hadInput |= HandleKeyboard(dt);

        if (hadInput)
        {
            timeSinceInput = 0f;
        }
        else
        {
            timeSinceInput += dt;

            if (idleSpinSpeed != 0f && timeSinceInput >= idleDelay)
                velocity = Damp(velocity, idleSpinSpeed, 1.5f, dt);
            else
                velocity = Damp(velocity, 0f, momentumDamping, dt);
        }

        // While dragging, rotation follows the finger directly (applied in HandlePointer).
        if (!dragging && Mathf.Abs(velocity) > 0.01f)
            ApplySpin(velocity * dt);
    }

    // ---------------------------------------------------------------- Pointer (mouse + touch)

    private bool HandlePointer(float dt)
    {
        bool held = ReadPointer(out Vector2 position);

        if (!held)
        {
            if (pointerWasDown && dragging)
                velocity = Mathf.Clamp(velocity, -maxSpinSpeed, maxSpinSpeed); // fling

            pointerWasDown = false;
            dragging = false;
            pressBlocked = false;
            return false;
        }

        if (!pointerWasDown)
        {
            // Press just started.
            pointerWasDown = true;
            dragging = false;
            pressStartPosition = position;
            lastPointerPosition = position;
            pressBlocked = ignorePressesOnUIControls && IsOverUIControl(position);

            if (!pressBlocked) velocity = 0f; // grabbing the taxi stops it
            return !pressBlocked;
        }

        if (pressBlocked) return false;

        if (!dragging)
        {
            float threshold = dragThresholdPixels * dragThresholdPixels;
            if ((position - pressStartPosition).sqrMagnitude < threshold) return true;
            dragging = true; // lastPointerPosition is still the press start, so no movement is lost
        }

        float deltaX = position.x - lastPointerPosition.x;
        lastPointerPosition = position;

        float degrees = deltaX / Mathf.Max(1f, Screen.width) * degreesPerScreenWidth;
        ApplySpin(degrees);

        // Track drag speed so letting go flings the taxi. Holding still bleeds it to zero.
        velocity = Damp(velocity, degrees / dt, 20f, dt);
        return true;
    }

    private static bool ReadPointer(out Vector2 position)
    {
#if ENABLE_INPUT_SYSTEM
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
        {
            position = touchscreen.primaryTouch.position.ReadValue();
            return true;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed)
        {
            position = mouse.position.ReadValue();
            return true;
        }
#else
        if (Input.touchCount > 0)
        {
            position = Input.GetTouch(0).position;
            return true;
        }

        if (Input.GetMouseButton(0))
        {
            position = Input.mousePosition;
            return true;
        }
#endif
        position = default;
        return false;
    }

    /// <summary>True if the screen position is over an interactive UI element (not just a background image).</summary>
    private static bool IsOverUIControl(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return false;

        var data = new PointerEventData(eventSystem) { position = screenPosition };
        raycastHits.Clear();
        eventSystem.RaycastAll(data, raycastHits);

        for (int i = 0; i < raycastHits.Count; i++)
        {
            GameObject hit = raycastHits[i].gameObject;
            if (hit.GetComponentInParent<Selectable>() != null) return true;
            if (hit.GetComponentInParent<ScrollRect>() != null) return true;
        }

        return false;
    }

    // ---------------------------------------------------------------- Keyboard

    private bool HandleKeyboard(float dt)
    {
        float axis = ReadKeyAxis();
        if (axis == 0f) return false;

        velocity = Damp(velocity, axis * keySpinSpeed, keyResponsiveness, dt);
        return true;
    }

    private static float ReadKeyAxis()
    {
        float axis = 0f;
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) axis -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) axis += 1f;
        }
#else
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) axis -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) axis += 1f;
#endif
        return axis;
    }

    // ---------------------------------------------------------------- Helpers

    /// <param name="rightwardDegrees">Positive = the way a rightward drag / D key spins.</param>
    private void ApplySpin(float rightwardDegrees)
    {
        float sign = invertDirection ? 1f : -1f;
        target.Rotate(Vector3.up, rightwardDegrees * sign, Space.World);
    }

    /// <summary>Frame-rate independent exponential approach.</summary>
    private static float Damp(float current, float targetValue, float rate, float dt)
    {
        return Mathf.Lerp(current, targetValue, 1f - Mathf.Exp(-rate * dt));
    }
}