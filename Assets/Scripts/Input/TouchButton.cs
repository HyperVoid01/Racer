using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// A hold-able on-screen button that supports multi-touch (steer and accelerate at the same time) and,
/// optionally, sliding a thumb from one button onto another (e.g. left to right steering) without lifting it.
/// Style it like any other UI: the Image colour you set in the editor is the idle colour.
/// Buttons with a cooldown (e.g. HOOT) can show it with <see cref="SetReadiness"/>.
/// </summary>
[RequireComponent(typeof(Graphic))]
public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("Pressing starts when a finger that is already down slides onto the button.")]
    public bool slideToActivate = true;

    [Tooltip("Colour while held. The idle colour is whatever colour the Image has in the editor.")]
    public Color pressedColor = new Color(1f, 0.82f, 0.15f, 0.75f);
    [Range(0.8f, 1f)] public float pressedScale = 0.93f;

    [Tooltip("Colour right after use while on cooldown. It fades back to the idle colour as the cooldown runs out.")]
    public Color cooldownColor = new Color(0.35f, 0.35f, 0.35f, 0.45f);
    [Tooltip("Scale pop when the cooldown finishes, so the player notices the button is ready.")]
    [Range(1f, 1.3f)] public float readyPopScale = 1.12f;

    /// <summary>Raised when the button goes from released to held (true) or back (false).</summary>
    public event Action<bool> HeldChanged;
    /// <summary>Raised on each new press.</summary>
    public event Action Pressed;

    private const float ReadyPopDuration = 0.18f;

    private readonly HashSet<int> _pointers = new HashSet<int>();
    private Graphic _graphic;
    private Color _idleColor;
    private Vector3 _restScale = Vector3.one;
    private bool _initialised;
    private float _readiness = 1f;
    private float _readyPopTimer;

    public bool IsHeld => _pointers.Count > 0;

    /// <summary>1 = ready, below 1 = on cooldown.</summary>
    public float Readiness => _readiness;

    private void Awake()
    {
        Initialise();
    }

    private void Initialise()
    {
        if (_initialised) return;
        _initialised = true;

        _graphic = GetComponent<Graphic>();
        _idleColor = _graphic != null ? _graphic.color : Color.white;
        _restScale = transform.localScale;
    }

    private void OnDisable()
    {
        ReleaseAll();
        _readyPopTimer = 0f;
        transform.localScale = _restScale;
    }

    private void LateUpdate()
    {
        // Safety net: if no finger or mouse button is down, nothing can be held
        // (e.g. sliding onto a button with the mouse and releasing never sends it an "up").
        if (IsHeld && !AnyPointerDown()) ReleaseAll();

        UpdateReadyPop();
    }

    public void OnPointerDown(PointerEventData eventData) => AddPointer(eventData.pointerId);

    public void OnPointerUp(PointerEventData eventData) => RemovePointer(eventData.pointerId);

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Only a finger that is already pressing something counts; plain mouse hover doesn't.
        if (slideToActivate && (eventData.pointerPress != null || eventData.dragging))
        {
            AddPointer(eventData.pointerId);
        }
    }

    public void OnPointerExit(PointerEventData eventData) => RemovePointer(eventData.pointerId);

    public void ReleaseAll()
    {
        if (_pointers.Count == 0) return;
        _pointers.Clear();
        OnHeldChanged(false);
    }

    /// <summary>
    /// Shows a cooldown: 0 = just used (greyed out), 1 = ready (idle colour). The button still works while
    /// greyed out; this is only visual. Reaching 1 plays a small pop.
    /// </summary>
    public void SetReadiness(float readiness01)
    {
        Initialise();

        float previous = _readiness;
        _readiness = Mathf.Clamp01(readiness01);

        if (previous < 1f && _readiness >= 1f && isActiveAndEnabled) _readyPopTimer = ReadyPopDuration;
        if (!IsHeld) ApplyIdleColor();
    }

    private void AddPointer(int pointerId)
    {
        bool wasHeld = IsHeld;
        if (!_pointers.Add(pointerId) || wasHeld) return;
        OnHeldChanged(true);
        Pressed?.Invoke();
    }

    private void RemovePointer(int pointerId)
    {
        if (!_pointers.Remove(pointerId) || IsHeld) return;
        OnHeldChanged(false);
    }

    private void OnHeldChanged(bool held)
    {
        if (held)
        {
            if (_graphic != null) _graphic.color = pressedColor;
            _readyPopTimer = 0f;
        }
        else
        {
            ApplyIdleColor();
        }

        transform.localScale = _restScale * (held ? pressedScale : 1f);
        HeldChanged?.Invoke(held);
    }

    private void ApplyIdleColor()
    {
        if (_graphic == null) return;

        // Fade from the cooldown colour most of the way back, then snap to full on ready so it's noticeable.
        _graphic.color = _readiness >= 1f
            ? _idleColor
            : Color.Lerp(cooldownColor, Color.Lerp(cooldownColor, _idleColor, 0.7f), _readiness);
    }

    private void UpdateReadyPop()
    {
        if (_readyPopTimer <= 0f || IsHeld) return;

        _readyPopTimer = Mathf.Max(0f, _readyPopTimer - Time.unscaledDeltaTime);
        float t = 1f - _readyPopTimer / ReadyPopDuration;
        float pop = Mathf.Sin(t * Mathf.PI) * (readyPopScale - 1f);
        transform.localScale = _restScale * (1f + pop);
    }

    private static bool AnyPointerDown()
    {
#if ENABLE_INPUT_SYSTEM
        // The Input System sees the Device Simulator's touches; the legacy Input class may not.
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            foreach (var touch in touchscreen.touches)
            {
                if (touch.press.isPressed) return true;
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.touchCount > 0 || Input.GetMouseButton(0)) return true;
#endif
        return false;
    }
}