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
/// </summary>
[RequireComponent(typeof(Graphic))]
public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("Pressing starts when a finger that is already down slides onto the button.")]
    public bool slideToActivate = true;

    [Tooltip("Colour while held. The idle colour is whatever colour the Image has in the editor.")]
    public Color pressedColor = new Color(1f, 0.82f, 0.15f, 0.75f);
    [Range(0.8f, 1f)] public float pressedScale = 0.93f;

    /// <summary>Raised when the button goes from released to held (true) or back (false).</summary>
    public event Action<bool> HeldChanged;
    /// <summary>Raised on each new press.</summary>
    public event Action Pressed;

    private readonly HashSet<int> _pointers = new HashSet<int>();
    private Graphic _graphic;
    private Color _idleColor;
    private Vector3 _restScale = Vector3.one;

    public bool IsHeld => _pointers.Count > 0;

    private void Awake()
    {
        _graphic = GetComponent<Graphic>();
        _idleColor = _graphic != null ? _graphic.color : Color.white;
        _restScale = transform.localScale;
    }

    private void OnDisable()
    {
        ReleaseAll();
    }

    private void LateUpdate()
    {
        // Safety net: if no finger or mouse button is down, nothing can be held
        // (e.g. sliding onto a button with the mouse and releasing never sends it an "up").
        if (IsHeld && !AnyPointerDown()) ReleaseAll();
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
        if (_graphic != null) _graphic.color = held ? pressedColor : _idleColor;
        transform.localScale = _restScale * (held ? pressedScale : 1f);
        HeldChanged?.Invoke(held);
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
