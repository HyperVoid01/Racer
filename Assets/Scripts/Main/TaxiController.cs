using System.Collections;
using UnityEngine;

public class TaxiController : MonoBehaviour
{
    [Header("Taxi Base Stats")]
    [SerializeField] private float baseAcceleration = 1f;
    [SerializeField] private float baseDeceleration = 1f;
    [SerializeField] private float baseBraking = 2f;
    [SerializeField] private float minimumSpeed = 1f;
    [SerializeField] private float baseTopSpeed = 100f;
    [SerializeField] private float baseStrafeSpeed = 1f;
    [SerializeField] private float wallCollisionSpeedDecrease = 5f;
    [SerializeField] private float crashModifier = 0.1f;

    [Header("Taxi Stats")]
    public float acceleration;
    public float deceleration;
    public float braking;
    public float topSpeed;
    public float strafeSpeed;
    
    private bool _onLeftWall = false;
    private bool _onRightWall = false;
    private bool ded = false;

    public float minWhistle = 5f;
    public float maxWhistle = 15f;
    
    private Transform _taxiStartTransform;

    private void Start()
    {
        _taxiStartTransform = transform;

        acceleration = baseAcceleration + UpgradeManager.Instance.accelerationModifier;
        deceleration = baseDeceleration - UpgradeManager.Instance.decelerationModifier;
        braking = baseBraking + UpgradeManager.Instance.brakingModifier;
        topSpeed = baseTopSpeed + UpgradeManager.Instance.topSpeedModifier;
        strafeSpeed = baseStrafeSpeed + UpgradeManager.Instance.strafeSpeedModifier;
        
        AudioManager.Instance.Play("Taxi Idle");
        StartCoroutine(PlayWhistle());
    }

    private void Update()
    {
        if (GameManager.Instance.gameOver)
        {
            if (ded == false)
            {
                AudioManager.Instance.StopAll();
                AudioManager.Instance.Play("Taxi crash");
                AudioManager.Instance.Play("Mama Crash");
                ded = true;
            }
            
            OnCrash();
            return;
        }
        
        if (UIController.instance._isPaused)
        {
            return;
        }
        
        if (Input.GetKey(KeyCode.W) && !GameManager.Instance.atStop)
        {
            Accelerate();
        }
        else if (Input.GetKey(KeyCode.S) && !GameManager.Instance.atStop)
        {
            Brake();
        }
        else if (!GameManager.Instance.atStop && !Input.GetKey(KeyCode.W))
        {
            Decelerate();
        }
        
        if (Input.GetKeyDown(KeyCode.W) && !GameManager.Instance.atStop)
        {
            AudioManager.Instance.Play("Taxi Accelerating");
        }
        else if (Input.GetKeyDown(KeyCode.S) && !GameManager.Instance.atStop)
        {
            AudioManager.Instance.Play("Taxi braking");
        }
        
        if (Input.GetKeyUp(KeyCode.W))
        {
            AudioManager.Instance.Stop("Taxi Accelerating");
            AudioManager.Instance.Play("Taxi Idle");
        }
        else if (Input.GetKeyUp(KeyCode.S))
        {
            AudioManager.Instance.Stop("Taxi braking");
            AudioManager.Instance.Play("Taxi Idle");
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            AudioManager.Instance.Play("Taxi Hooting");
        }
        
        if (Input.GetKey(KeyCode.A) && !_onLeftWall && GameManager.Instance.gameSpeed >= 5)
        {
            StrafeLeft();
            _onRightWall = false;
            Decelerate();
        }
        
        if (Input.GetKey(KeyCode.D) && !_onRightWall && GameManager.Instance.gameSpeed >= 5)
        {
            StrafeRight();
            _onLeftWall = false;
            Decelerate();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("KillZone") || other.CompareTag("Vehicle"))
        {
            GameManager.Instance.gameOver = true;
        }

        if (other.CompareTag("LeftWall"))
        {
            _onLeftWall = true;
            GameManager.Instance.gameSpeed -= wallCollisionSpeedDecrease;
        }

        if (other.CompareTag("RightWall"))
        {
            _onRightWall = true;
            GameManager.Instance.gameSpeed -= wallCollisionSpeedDecrease;
        }

        if (other.CompareTag("TaxiStopEntrance"))
        {
            _onLeftWall = false;
        }
    }
    
    private void Accelerate()
    {
        GameManager.Instance.gameSpeed = Mathf.Lerp(
            GameManager.Instance.gameSpeed,
            topSpeed,
            acceleration * Time.deltaTime
        );
    }
    
    private void Decelerate()
    {
        if (GameManager.Instance.gameSpeed > minimumSpeed)
        {
            GameManager.Instance.gameSpeed -= deceleration * Time.deltaTime;
        }
        else if (GameManager.Instance.gameSpeed <= minimumSpeed)
        {
            GameManager.Instance.gameSpeed = minimumSpeed;
        }
    }

    private void Brake()
    {
        if (GameManager.Instance.gameSpeed > minimumSpeed)
        {
            GameManager.Instance.gameSpeed -= braking * Time.deltaTime;
        }
        else if (GameManager.Instance.gameSpeed <= minimumSpeed)
        {
            GameManager.Instance.gameSpeed = minimumSpeed;
        }
    }

    private void StrafeLeft()
    {
        Vector3 targetPosition = new Vector3(transform.position.x - strafeSpeed, transform.position.y, transform.position.z);
        Vector3 newLocation = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime);
        transform.position = newLocation;
    }
    
    private void StrafeRight()
    {
        Vector3 targetPosition = new Vector3(transform.position.x + strafeSpeed, transform.position.y, transform.position.z);
        Vector3 newLocation = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime);
        transform.position = newLocation;
    }

    public void OnCrash()
    {
        StopAllCoroutines();
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.None;
        rb.useGravity = true;
        Vector3 randomVelocity = Random.insideUnitSphere * GameManager.Instance.gameSpeed;
        GameManager.Instance.gameSpeed = 0;
        rb.AddForce(randomVelocity * crashModifier, ForceMode.Impulse);
    }

    private IEnumerator PlayWhistle()
    {
        while (!GameManager.Instance.gameOver && !UIController.instance._isPaused)
        {
            AudioManager.Instance.Play("Whistle");
            
            yield return new WaitForSeconds(Random.Range(minWhistle, maxWhistle));
        }
    }
}
