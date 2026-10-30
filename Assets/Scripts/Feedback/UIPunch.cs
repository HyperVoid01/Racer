using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small, dependency-free UI tweens ("juice"): scale punches, colour flashes and shakes.
/// Restarting a tween on the same target never drifts, because the resting value is remembered
/// from the first time the target is animated. Uses unscaled time so it works on pause screens too.
/// </summary>
public static class UIPunch
{
    private static readonly Dictionary<Transform, Vector3> RestScales = new Dictionary<Transform, Vector3>();
    private static readonly Dictionary<Graphic, Color> RestColors = new Dictionary<Graphic, Color>();
    private static readonly Dictionary<RectTransform, Vector2> RestPositions = new Dictionary<RectTransform, Vector2>();
    private static readonly Dictionary<Object, Coroutine> Running = new Dictionary<Object, Coroutine>();

    public static void Punch(MonoBehaviour host, Transform target, float strength = 0.18f, float duration = 0.22f)
    {
        if (!CanRun(host, target)) return;
        if (!RestScales.ContainsKey(target)) RestScales[target] = target.localScale;
        Restart(host, target, PunchRoutine(target, RestScales[target], strength, duration));
    }

    /// <summary>Set the colour a graphic returns to after flashes (e.g. when the HUD changes it on purpose).</summary>
    public static void SetRestColor(Graphic graphic, Color color)
    {
        if (graphic == null) return;
        RestColors[graphic] = color;
        if (!Running.ContainsKey(graphic)) graphic.color = color;
    }

    public static void Flash(MonoBehaviour host, Graphic graphic, Color color, float duration = 0.35f)
    {
        if (!CanRun(host, graphic)) return;
        if (!RestColors.ContainsKey(graphic)) RestColors[graphic] = graphic.color;
        Restart(host, graphic, FlashRoutine(graphic, color, duration));
    }

    public static void Shake(MonoBehaviour host, RectTransform target, float distance = 12f, float duration = 0.3f)
    {
        if (!CanRun(host, target)) return;
        if (!RestPositions.ContainsKey(target)) RestPositions[target] = target.anchoredPosition;
        Restart(host, target, ShakeRoutine(target, RestPositions[target], distance, duration));
    }

    /// <summary>Forget cached targets (call when a UI scene is torn down).</summary>
    public static void Clear()
    {
        RestScales.Clear();
        RestColors.Clear();
        RestPositions.Clear();
        Running.Clear();
    }

    private static bool CanRun(MonoBehaviour host, Object target) =>
        host != null && target != null && host.isActiveAndEnabled;

    private static void Restart(MonoBehaviour host, Object key, IEnumerator routine)
    {
        if (Running.TryGetValue(key, out Coroutine running) && running != null) host.StopCoroutine(running);
        Running[key] = host.StartCoroutine(Wrap(key, routine));
    }

    private static IEnumerator Wrap(Object key, IEnumerator routine)
    {
        yield return routine;
        Running.Remove(key);
    }

    private static IEnumerator PunchRoutine(Transform target, Vector3 restScale, float strength, float duration)
    {
        float t = 0f;
        while (t < duration && target != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            target.localScale = restScale * (1f + Mathf.Sin(k * Mathf.PI) * strength);
            yield return null;
        }
        if (target != null) target.localScale = restScale;
    }

    private static IEnumerator FlashRoutine(Graphic graphic, Color color, float duration)
    {
        float t = 0f;
        while (t < duration && graphic != null)
        {
            t += Time.unscaledDeltaTime;
            graphic.color = Color.Lerp(color, RestColors[graphic], Mathf.Clamp01(t / duration));
            yield return null;
        }
        if (graphic != null) graphic.color = RestColors[graphic];
    }

    private static IEnumerator ShakeRoutine(RectTransform target, Vector2 restPosition, float distance, float duration)
    {
        float t = 0f;
        while (t < duration && target != null)
        {
            t += Time.unscaledDeltaTime;
            float falloff = 1f - Mathf.Clamp01(t / duration);
            target.anchoredPosition = restPosition + new Vector2(Mathf.Sin(t * 70f) * distance * falloff, 0f);
            yield return null;
        }
        if (target != null) target.anchoredPosition = restPosition;
    }
}
