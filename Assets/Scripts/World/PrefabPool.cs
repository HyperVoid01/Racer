using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple prefab pool. Endless runners spawn and remove the same few prefabs constantly; reusing them
/// avoids Instantiate/Destroy spikes and garbage collection hitches, which are very noticeable on mobile.
/// Pooled objects should reset their state in OnEnable.
/// </summary>
public static class PrefabPool
{
    private static readonly Dictionary<GameObject, Stack<GameObject>> Pools = new Dictionary<GameObject, Stack<GameObject>>();

    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        GameObject instance = null;
        if (Pools.TryGetValue(prefab, out Stack<GameObject> stack))
        {
            // Skip anything destroyed behind our back.
            while (instance == null && stack.Count > 0) instance = stack.Pop();
        }

        if (instance == null)
        {
            instance = Object.Instantiate(prefab, position, rotation);
            instance.AddComponent<PooledInstance>().Prefab = prefab;
            return instance;
        }

        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);
        return instance;
    }

    public static void Release(GameObject instance)
    {
        if (instance == null || !instance.activeSelf) return;

        if (!instance.TryGetComponent(out PooledInstance pooled) || pooled.Prefab == null)
        {
            Object.Destroy(instance);
            return;
        }

        instance.SetActive(false);

        if (!Pools.TryGetValue(pooled.Prefab, out Stack<GameObject> stack))
        {
            stack = new Stack<GameObject>();
            Pools.Add(pooled.Prefab, stack);
        }
        stack.Push(instance);
    }

    /// <summary>Creates inactive instances up front so the first spawns during play don't hitch.</summary>
    public static void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null) return;

        var temp = new List<GameObject>(count);
        for (int i = 0; i < count; i++)
        {
            temp.Add(Spawn(prefab, new Vector3(0f, -1000f, -1000f), Quaternion.identity));
        }
        foreach (GameObject go in temp) Release(go);
    }

    /// <summary>Forget every pooled instance. Called when a scene unloads (its objects are destroyed with it).</summary>
    public static void Clear()
    {
        Pools.Clear();
    }
}
