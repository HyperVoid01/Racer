using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moves every <see cref="ScrollingObject"/> by the current game speed and despawns what leaves the play area.
/// Created automatically by <see cref="GameManager"/>.
/// </summary>
[DefaultExecutionOrder(-50)]
public class WorldScroller : MonoBehaviour
{
    private static readonly List<ScrollingObject> Objects = new List<ScrollingObject>(128);

    public static WorldScroller Instance { get; private set; }

    /// <summary>Distance driven this run, in world units.</summary>
    public float Distance { get; private set; }

    /// <summary>How far the road moved during the last frame.</summary>
    public float LastDelta { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    internal static void Register(ScrollingObject obj)
    {
        if (obj.ScrollerIndex >= 0) return;
        obj.ScrollerIndex = Objects.Count;
        Objects.Add(obj);
    }

    internal static void Unregister(ScrollingObject obj)
    {
        int index = obj.ScrollerIndex;
        if (index < 0 || index >= Objects.Count || Objects[index] != obj) return;

        // Swap-remove keeps this O(1).
        int last = Objects.Count - 1;
        ScrollingObject moved = Objects[last];
        Objects[index] = moved;
        moved.ScrollerIndex = index;
        Objects.RemoveAt(last);
        obj.ScrollerIndex = -1;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || GameManager.Instance == null) return;

        float delta = GameManager.Instance.GameSpeed * dt;
        LastDelta = delta;
        Distance += delta;

        // Iterate backwards so an object despawning (and swap-removing itself) can't be skipped or visited twice.
        for (int i = Objects.Count - 1; i >= 0; i--)
        {
            if (i >= Objects.Count) continue;

            ScrollingObject obj = Objects[i];
            obj.Scroll(delta, dt);
            if (obj.ShouldDespawn()) obj.Despawn();
        }
    }
}
