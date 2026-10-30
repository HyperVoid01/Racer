using UnityEngine;

/// <summary>Added automatically by <see cref="PrefabPool"/> to remember which prefab an instance came from.</summary>
[DisallowMultipleComponent]
public sealed class PooledInstance : MonoBehaviour
{
    public GameObject Prefab { get; internal set; }
}
