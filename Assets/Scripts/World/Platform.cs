using UnityEngine;

/// <summary>
/// A road piece. Its "ConnectionPoint" child marks where the next piece attaches,
/// so it also tells us where the piece ends.
/// </summary>
public class Platform : ScrollingObject
{
    private Transform _connectionPoint;

    public Transform ConnectionPoint
    {
        get
        {
            if (_connectionPoint == null) _connectionPoint = FindConnectionPoint();
            return _connectionPoint;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        _connectionPoint = FindConnectionPoint();
    }

    private Transform FindConnectionPoint()
    {
        Transform point = transform.Find("ConnectionPoint");
        if (point != null) return point;
        return transform.childCount > 0 ? transform.GetChild(0) : transform;
    }

    // Wait until the whole piece is behind the camera, not just its start.
    public override bool ShouldDespawn() => ConnectionPoint.position.z < despawnBehindZ;
}
