using UnityEngine;

/// <summary>
/// Anything that moves towards the camera as the taxi drives: road pieces, potholes, traffic.
/// Movement is driven centrally by <see cref="WorldScroller"/> (one Update for the whole world instead of
/// one coroutine per object), and objects return to the <see cref="PrefabPool"/> once they leave the play area.
/// </summary>
public class ScrollingObject : MonoBehaviour
{
    [Header("Despawn")]
    [Tooltip("Removed once its position passes this Z behind the camera.")]
    [SerializeField] protected float despawnBehindZ = -60f;
    [Tooltip("Removed once its position passes this Z ahead of the spawners.")]
    [SerializeField] protected float despawnAheadZ = 400f;

    // Index in WorldScroller's list, for O(1) removal.
    internal int ScrollerIndex = -1;

    protected Transform CachedTransform { get; private set; }

    protected virtual void Awake()
    {
        CachedTransform = transform;

        // Colliders that move every frame should belong to a kinematic body; moving "static" colliders
        // (no Rigidbody) forces the physics engine to rebuild them each time. Triggers still fire.
        if (!TryGetComponent(out Rigidbody body)) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.None;
    }

    protected virtual void OnEnable()
    {
        WorldScroller.Register(this);
    }

    protected virtual void OnDisable()
    {
        WorldScroller.Unregister(this);
    }

    /// <summary>Move with the road. <paramref name="worldDelta"/> is how far the road moved this frame.</summary>
    public virtual void Scroll(float worldDelta, float deltaTime)
    {
        CachedTransform.position += new Vector3(0f, 0f, -worldDelta);
    }

    public virtual bool ShouldDespawn()
    {
        float z = CachedTransform.position.z;
        return z < despawnBehindZ || z > despawnAheadZ;
    }

    public void Despawn()
    {
        PrefabPool.Release(gameObject);
    }
}
