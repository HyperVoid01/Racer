using UnityEngine;

/// <summary>
/// One place to read player intent from, whatever the device:
/// keyboard (WASD / arrows), gamepad (left stick / d-pad through the legacy "Horizontal"/"Vertical" axes)
/// and the on-screen touch controls (<see cref="MobileControls"/>).
/// Gameplay code should only ever read from here, never from Input directly.
/// </summary>
public static class TaxiInput
{
    private const float AxisThreshold = 0.5f;

    // Written by on-screen controls.
    public static bool VirtualThrottle;
    public static bool VirtualBrake;
    public static bool VirtualLeft;
    public static bool VirtualRight;

    // Taps from UI buttons arrive during the EventSystem's update, which can run before or after gameplay
    // scripts in a frame, so they are stored until read rather than matched to a frame number.
    private static bool _hornQueued;
    private static bool _pauseQueued;

    /// <summary>
    /// Force touch controls on or off. By default they show on phones/tablets, and in the editor's
    /// Device Simulator (which reports a mobile platform), so they can be tested with the mouse.
    /// </summary>
    public static bool? TouchControlsOverride;

    public static bool UseTouchControls => TouchControlsOverride ?? Bootstrap.IsMobile;

    public static bool Throttle =>
        VirtualThrottle ||
        Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ||
        Input.GetAxisRaw("Vertical") > AxisThreshold;

    public static bool Brake =>
        VirtualBrake ||
        Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ||
        Input.GetAxisRaw("Vertical") < -AxisThreshold;

    /// <summary>-1 (left) to 1 (right).</summary>
    public static float Steer
    {
        get
        {
            float steer = 0f;
            if (VirtualLeft || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) steer -= 1f;
            if (VirtualRight || Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) steer += 1f;

            if (Mathf.Approximately(steer, 0f))
            {
                float axis = Input.GetAxisRaw("Horizontal");
                if (Mathf.Abs(axis) > 0.2f) steer = axis;
            }
            return Mathf.Clamp(steer, -1f, 1f);
        }
    }

    public static bool BrakePressedThisFrame =>
        Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);

    /// <summary>True once per horn press. Call from one place only (the taxi).</summary>
    public static bool ConsumeHornPress()
    {
        bool pressed = _hornQueued ||
                       Input.GetKeyDown(KeyCode.Space) ||
                       Input.GetKeyDown(KeyCode.JoystickButton0);
        _hornQueued = false;
        return pressed;
    }

    /// <summary>True once per pause request. Escape is also the Android back button.</summary>
    public static bool ConsumePausePress()
    {
        bool pressed = _pauseQueued ||
                       Input.GetKeyDown(KeyCode.Escape) ||
                       Input.GetKeyDown(KeyCode.P) ||
                       Input.GetKeyDown(KeyCode.JoystickButton7);
        _pauseQueued = false;
        return pressed;
    }

    public static void QueueHorn() => _hornQueued = true;
    public static void QueuePause() => _pauseQueued = true;

    public static void ResetVirtual()
    {
        VirtualThrottle = VirtualBrake = VirtualLeft = VirtualRight = false;
        _hornQueued = _pauseQueued = false;
    }
}
