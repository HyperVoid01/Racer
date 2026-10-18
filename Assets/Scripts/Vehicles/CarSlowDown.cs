using UnityEngine;

public class CarSlowDown : MonoBehaviour
{
    private Vehicle vehicle;
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Vehicle"))
        {
            vehicle = GetComponentInParent<Vehicle>();
            vehicle.Decelerate(other.GetComponent<Vehicle>());
        }
    }
}
