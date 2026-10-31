using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// One place to read player intent from, whatever the device:
/// keyboard (WASD / arrows), gamepad (left stick / d-pad, South button = horn, Start = pause)
/// and the on-screen touch controls (<see cref="MobileControls"/>).
/// Gameplay code should only ever read from here, never from the input devices directly.
/// Reads devices through the Input System package; the Android back button arrives as the Escape key.
/// </summary>
public static class TaxiInput
{
    private const float AxisThreshold = 0.5f;
    private const float SteerDeadZone = 0.2f;

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
        KeyHeld(KeyCodeUp) ||
        GamepadVertical() > AxisThreshold;

    public static bool Brake =>
        VirtualBrake ||
        KeyHeld(KeyCodeDown) ||
        GamepadVertical() < -AxisThreshold;

    /// <summary>-1 (left) to 1 (right).</summary>
    public static float Steer
    {
        get
        {
            float steer = 0f;
            if (VirtualLeft || KeyHeld(KeyCodeLeft)) steer -= 1f;
            if (VirtualRight || KeyHeld(KeyCodeRight)) steer += 1f;

            if (Mathf.Approximately(steer, 0f))
            {
                float axis = GamepadHorizontal();
                if (Mathf.Abs(axis) > SteerDeadZone) steer = axis;
            }
            return Mathf.Clamp(steer, -1f, 1f);
        }
    }

    public static bool BrakePressedThisFrame => KeyPressedThisFrame(KeyCodeDown);

    /// <summary>True once per horn press. Call from one place only (the taxi).</summary>
    public static bool ConsumeHornPress()
    {
        bool pressed = _hornQueued || HornPressedThisFrame();
        _hornQueued = false;
        return pressed;
    }

    /// <summary>True once per pause request. Escape is also the Android back button.</summary>
    public static bool ConsumePausePress()
    {
        bool pressed = _pauseQueued || PausePressedThisFrame();
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

    // ------------------------------------------------------------------------------------------------
    // Device reading. Kept in one place so the rest of the class doesn't care which input backend is on.
    // ------------------------------------------------------------------------------------------------

    private const int KeyCodeUp = 0;
    private const int KeyCodeDown = 1;
    private const int KeyCodeLeft = 2;
    private const int KeyCodeRight = 3;

#if ENABLE_INPUT_SYSTEM
    private static bool KeyHeld(int direction)
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return false;
        switch (direction)
        {
            case KeyCodeUp: return kb.wKey.isPressed || kb.upArrowKey.isPressed;
            case KeyCodeDown: return kb.sKey.isPressed || kb.downArrowKey.isPressed;
            case KeyCodeLeft: return kb.aKey.isPressed || kb.leftArrowKey.isPressed;
            case KeyCodeRight: return kb.dKey.isPressed || kb.rightArrowKey.isPressed;
            default: return false;
        }
    }

    private static bool KeyPressedThisFrame(int direction)
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return false;
        switch (direction)
        {
            case KeyCodeUp: return kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame;
            case KeyCodeDown: return kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame;
            case KeyCodeLeft: return kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame;
            case KeyCodeRight: return kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame;
            default: return false;
        }
    }

    private static float GamepadHorizontal()
    {
        Gamepad pad = Gamepad.current;
        if (pad == null) return 0f;
        float stick = pad.leftStick.x.ReadValue();
        float dpad = pad.dpad.x.ReadValue();
        return Mathf.Abs(dpad) > Mathf.Abs(stick) ? dpad : stick;
    }

    private static float GamepadVertical()
    {
        Gamepad pad = Gamepad.current;
        if (pad == null) return 0f;
        float stick = pad.leftStick.y.ReadValue();
        float dpad = pad.dpad.y.ReadValue();
        return Mathf.Abs(dpad) > Mathf.Abs(stick) ? dpad : stick;
    }

    private static bool HornPressedThisFrame()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (kb != null && kb.spaceKey.wasPressedThisFrame) ||
               (pad != null && pad.buttonSouth.wasPressedThisFrame);
    }

    private static bool PausePressedThisFrame()
    {
        // On Android the Input System reports the back button as the Escape key.
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)) ||
               (pad != null && pad.startButton.wasPressedThisFrame);
    }
#else
    // Fallback for projects set to the legacy Input Manager only.
    private static bool KeyHeld(int direction)
    {
        switch (direction)
        {
            case KeyCodeUp: return Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            case KeyCodeDown: return Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            case KeyCodeLeft: return Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
            case KeyCodeRight: return Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
            default: return false;
        }
    }

    private static bool KeyPressedThisFrame(int direction)
    {
        switch (direction)
        {
            case KeyCodeUp: return Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
            case KeyCodeDown: return Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);
            case KeyCodeLeft: return Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
            case KeyCodeRight: return Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);
            default: return false;
        }
    }

    private static float GamepadHorizontal() => Input.GetAxisRaw("Horizontal");
    private static float GamepadVertical() => Input.GetAxisRaw("Vertical");

    private static bool HornPressedThisFrame() =>
        Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0);

    private static bool PausePressedThisFrame() =>
        Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.JoystickButton7);
#endif
}