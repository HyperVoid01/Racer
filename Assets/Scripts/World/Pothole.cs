using UnityEngine;

/// <summary>Slows the taxi down to <see cref="speedSet"/> when driven over.</summary>
public class Pothole : ScrollingObject
{
    [Tooltip("Taxi speed is capped to this when hitting the pothole.")]
    [SerializeField] private int speedSet = 15;
    [SerializeField, Range(0f, 1f)] private float impactStrength = 0.45f;

    private bool _hit;

    protected override void OnEnable()
    {
        base.OnEnable();
        _hit = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hit || !other.CompareTag("Player")) return;

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Driving) return;

        _hit = true;

        float strength = impactStrength;
        if (gm.GameSpeed > speedSet)
        {
            gm.GameSpeed = speedSet;
        }
        else
        {
            strength *= 0.5f; // Gentle bump when already slow.
        }

        AudioManager.Play("Pothole");
        gm.ReportImpact(strength);
        Haptics.Light();
    }
}
