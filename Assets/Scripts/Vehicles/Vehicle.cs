using System.Collections;
using UnityEngine;

public class Vehicle : MonoBehaviour
{
    public float _vehicleSpeed;
    [SerializeField] private float deceleration;
    private void Start()
    {
        _vehicleSpeed = Random.Range(VehicleSpawner.Instance.vehicleMinSpeed, VehicleSpawner.Instance.vehicleMaxSpeed);
        
        StartCoroutine(MoveVehicle());
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Destroyer"))
        {
            Destroy(gameObject);
        }
    }

    private IEnumerator MoveVehicle()
    {
        while (true)
        {
            Vector3 targetPosition;
            
            if (GameManager.Instance.atStop || GameManager.Instance.gameOver)
            {
                targetPosition = new Vector3(transform.position.x, transform.position.y, transform.position.z + (_vehicleSpeed * 15));
            }
            else
            {
                targetPosition = new Vector3(transform.position.x, transform.position.y, transform.position.z - (_vehicleSpeed * GameManager.Instance.gameSpeed));
            }
            Vector3 newLocation = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime);
            transform.position = newLocation;
            yield return null;
        }
    }

    public void Decelerate(Vehicle frontVehicle)
    {
        _vehicleSpeed = frontVehicle._vehicleSpeed;
    }
}
