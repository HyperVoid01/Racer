using UnityEngine;
// UnityEngine.Device.Screen matches UnityEngine.Screen in builds, and reports the simulated phone
// (notch, cut-outs) in the editor's Device Simulator.
using DeviceScreen = UnityEngine.Device.Screen;

/// <summary>
/// Shrinks a full-screen RectTransform to the device safe area so UI never sits under a notch,
/// camera cut-out or rounded corner. Put your touch controls (or any edge-hugging UI) inside it.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    private RectTransform _rect;
    private Rect _lastSafeArea;
    private Vector2Int _lastScreenSize;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        Apply();
    }

    private void Update()
    {
        if (_lastSafeArea != DeviceScreen.safeArea ||
            _lastScreenSize.x != DeviceScreen.width ||
            _lastScreenSize.y != DeviceScreen.height)
        {
            Apply();
        }
    }

    private void Apply()
    {
        Rect safe = DeviceScreen.safeArea;
        int width = DeviceScreen.width;
        int height = DeviceScreen.height;

        _lastSafeArea = safe;
        _lastScreenSize = new Vector2Int(width, height);

        if (width <= 0 || height <= 0) return;

        Vector2 min = safe.position;
        Vector2 max = safe.position + safe.size;
        min.x /= width;
        min.y /= height;
        max.x /= width;
        max.y /= height;

        _rect.anchorMin = min;
        _rect.anchorMax = max;
        _rect.offsetMin = Vector2.zero;
        _rect.offsetMax = Vector2.zero;
    }
}
