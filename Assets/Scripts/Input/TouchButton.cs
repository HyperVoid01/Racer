using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A hold-able on-screen button that supports multi-touch (steer and accelerate at the same time) and,
/// optionally, sliding a thumb from one button onto another (e.g. left to right steering) without lifting it.
/// </summary>
public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("Pressing starts when a finger that is already down slides onto the button.")]
    public bool slideToActivate = true;

    public Color idleColor = new Color(0f, 0f, 0f, 0.35f);
    public Color pressedColor = new Color(1f, 0.82f, 0.15f, 0.75f);
    [Range(0.8f, 1f)] public float pressedScale = 0.93f;

    /// <summary>Raised when the button goes from released to held (true) or back (false).</summary>
    public event Action<bool> HeldChanged;
    /// <summary>Raised on each new press.</summary>
    public event Action Pressed;

    private readonly HashSet<int> _pointers = new HashSet<int>();
    private Graphic _graphic;
    private Vector3 _restScale = Vector3.one;

    public bool IsHeld => _pointers.Count > 0;

    private void Awake()
    {
        _graphic = GetComponent<Graphic>();
        _restScale = transform.localScale;
        ApplyVisual();
    }

    private void OnDisable()
    {
        ReleaseAll();
    }

    private void LateUpdate()
    {
        // Safety net: if every finger is up, nothing can be held (some platforms skip the final exit/up event).
        if (IsHeld && Input.touchCount == 0 && !Input.GetMouseButton(0)) ReleaseAll();
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
        ApplyVisual();
        HeldChanged?.Invoke(held);
    }

    private void ApplyVisual()
    {
        if (_graphic != null) _graphic.color = IsHeld ? pressedColor : idleColor;
        transform.localScale = _restScale * (IsHeld ? pressedScale : 1f);
    }
}
