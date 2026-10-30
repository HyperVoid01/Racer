using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Helpers for building UI from code (used by the touch controls and HUD extras), including procedurally
/// generated sprites so no extra art assets are needed.
/// </summary>
public static class RuntimeUI
{
    /// <summary>9-slice border (pixels) of the rounded rectangle sprite.</summary>
    public const int RoundedRectBorder = 34;

    private static Sprite _circle;
    private static Sprite _roundedRect;
    private static Sprite _triangle;

    public static Sprite Circle
    {
        get
        {
            if (_circle == null) _circle = ToSprite(CreateCircleTexture(false), 0);
            return _circle;
        }
    }

    /// <summary>9-sliced rounded rectangle.</summary>
    public static Sprite RoundedRect
    {
        get
        {
            if (_roundedRect == null) _roundedRect = ToSprite(CreateRoundedRectTexture(false), RoundedRectBorder);
            return _roundedRect;
        }
    }

    /// <summary>Triangle pointing right.</summary>
    public static Sprite Triangle
    {
        get
        {
            if (_triangle == null) _triangle = ToSprite(CreateArrowTexture(false), 0);
            return _triangle;
        }
    }

    public static Canvas CreateOverlayCanvas(string name, int sortingOrder)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        return canvas;
    }

    public static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    public static RectTransform Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    public static Image CreateImage(string name, Transform parent, Sprite sprite, Color color, bool sliced = false)
    {
        RectTransform rect = CreateRect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        return image;
    }

    public static TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize,
        TMP_FontAsset font, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        RectTransform rect = CreateRect(name, parent);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    // ---------------------------------------------------------------- Procedural textures
    // Public so the editor setup tool can save them as PNG assets you can restyle or replace.

    public static Texture2D CreateRoundedRectTexture(bool keepReadable) => RoundedRectTexture(96, 32, keepReadable);
    public static Texture2D CreateCircleTexture(bool keepReadable) => RoundedRectTexture(128, 64, keepReadable);

    /// <summary>Triangle pointing right.</summary>
    public static Texture2D CreateArrowTexture(bool keepReadable)
    {
        const int size = 96;
        Texture2D texture = NewTexture(size, "RuntimeUI_Arrow");

        var pixels = new Color32[size * size];
        var a = new Vector2(size * 0.2f, size * 0.12f);
        var b = new Vector2(size * 0.2f, size * 0.88f);
        var c = new Vector2(size * 0.88f, size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Mathf.Max(EdgeDistance(p, a, b), Mathf.Max(EdgeDistance(p, b, c), EdgeDistance(p, c, a)));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, !keepReadable);
        return texture;
    }

    private static Texture2D RoundedRectTexture(int size, int radius, bool keepReadable)
    {
        Texture2D texture = NewTexture(size, "RuntimeUI_Rounded");

        var pixels = new Color32[size * size];
        float half = size * 0.5f;
        float inner = half - radius;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Signed distance to a rounded box.
                float px = Mathf.Abs(x + 0.5f - half) - inner;
                float py = Mathf.Abs(y + 0.5f - half) - inner;
                float outside = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude;
                float distance = outside + Mathf.Min(Mathf.Max(px, py), 0f) - radius;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - distance) * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, !keepReadable);
        return texture;
    }

    private static Texture2D NewTexture(int size, string name)
    {
        return new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = name
        };
    }

    private static Sprite ToSprite(Texture2D texture, int border)
    {
        var borders = new Vector4(border, border, border, border);
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, borders);
    }

    // Signed distance to the line through a->b; positive = outside for the a,b,c order used above.
    private static float EdgeDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 edge = b - a;
        Vector2 normal = new Vector2(-edge.y, edge.x).normalized;
        return Vector2.Dot(p - a, normal);
    }
}
