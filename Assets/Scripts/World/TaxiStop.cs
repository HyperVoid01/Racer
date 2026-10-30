using System.Collections;
using UnityEngine;

/// <summary>
/// The pick-up bay. Pull into it to stop: everyone on board gets off (and pays), then new passengers board
/// and the delivery timer restarts.
/// </summary>
public class TaxiStop : MonoBehaviour
{
    [Header("Stopping")]
    [Tooltip("How quickly the taxi brakes inside the bay (exponential, per second).")]
    [SerializeField] private float stopBrakeRate = 4f;
    [Tooltip("Minimum braking in units/s² so the taxi doesn't creep to a halt.")]
    [SerializeField] private float minStopDeceleration = 20f;

    [Header("Passengers")]
    [SerializeField] private int minPassengers = 5;
    [Tooltip("Inclusive.")]
    [SerializeField] private int maxPassengers = 10;
    [Tooltip("Longest the drop-off or the pick-up phase can take.")]
    [SerializeField] private float loadingDuration = 5f;
    [Tooltip("Caps the time per passenger so small groups don't drag.")]
    [SerializeField] private float maxSecondsPerPassenger = 0.4f;

    [Header("Call-out")]
    [Tooltip("Passengers shout \"stop here\" when the bay is this close ahead.")]
    [SerializeField] private float callOutDistance = 140f;

    private bool _used;
    private bool _calledOut;
    private Coroutine _routine;

    private void OnEnable()
    {
        // Pooled: reset for reuse.
        _used = false;
        _calledOut = false;
        _routine = null;
    }

    private void Update()
    {
        if (_calledOut || _used) return;

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Driving || gm.Passengers == 0) return;

        float z = transform.position.z;
        if (z > 0f && z < callOutDistance)
        {
            _calledOut = true;
            AudioManager.Play("Stop");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_used || !other.CompareTag("Player")) return;

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Driving) return;

        _used = true;
        _routine = StartCoroutine(ServeStop(gm));
    }

    private IEnumerator ServeStop(GameManager gm)
    {
        gm.EnterStop();
        if (Timer.Instance != null) Timer.Instance.StopTimer();
        AudioManager.Play("Hey taxi");

        // Ease to a halt instead of an instant stop.
        while (gm.GameSpeed > 0.05f)
        {
            if (gm.IsGameOver) yield break;
            float decel = Mathf.Max(gm.GameSpeed * stopBrakeRate, minStopDeceleration);
            gm.GameSpeed = Mathf.MoveTowards(gm.GameSpeed, 0f, decel * Time.deltaTime);
            yield return null;
        }
        gm.GameSpeed = 0f;

        // Drop off everyone. (The original loop compared against a shrinking count and only dropped off half.)
        int dropping = gm.Passengers;
        if (dropping > 0)
        {
            var wait = new WaitForSeconds(PerPassengerDelay(dropping));
            for (int i = 0; i < dropping; i++)
            {
                if (gm.IsGameOver) yield break;
                if (gm.TryRemovePassenger()) gm.AddScore(gm.FarePerPassenger);
                yield return wait;
            }
        }

        // Pick up new passengers, limited by free seats.
        int boarding = Mathf.Min(Random.Range(minPassengers, maxPassengers + 1), gm.MaxPassengers - gm.Passengers);
        if (boarding > 0)
        {
            var wait = new WaitForSeconds(PerPassengerDelay(boarding));
            for (int i = 0; i < boarding; i++)
            {
                if (gm.IsGameOver) yield break;
                gm.TryAddPassenger();
                yield return wait;
            }
        }

        if (gm.IsGameOver) yield break;

        if (gm.Passengers > 0 && Timer.Instance != null) Timer.Instance.StartTimer();
        AudioManager.Play("Sharp");
        gm.LeaveStop();
        _routine = null;
    }

    private float PerPassengerDelay(int count)
    {
        // Never divides by zero (the original could wait forever when the count hit 0).
        return Mathf.Min(loadingDuration / Mathf.Max(1, count), maxSecondsPerPassenger);
    }

    private void OnDisable()
    {
        // If the bay is recycled mid-stop for any reason, never leave the game stuck in AtStop.
        if (_routine != null && GameManager.Instance != null) GameManager.Instance.LeaveStop();
        _routine = null;
    }
}
