using UnityEngine;

/// <summary>Front sensor on a car: slows the car to the speed of whatever car is ahead of it.</summary>
public class CarSlowDown : MonoBehaviour
{
    private Vehicle _vehicle;

    private void Awake()
    {
        _vehicle = GetComponentInParent<Vehicle>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_vehicle == null || !other.CompareTag("Vehicle")) return;

        // The collider may be a child of the car, so look upwards. (The original used GetComponent and could
        // throw a NullReferenceException.)
        Vehicle front = other.GetComponentInParent<Vehicle>();
        _vehicle.MatchSpeed(front);
    }
}
