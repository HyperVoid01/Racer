using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Garage screen: buy upgrades, preview the taxi and its stats, then start a run.</summary>
public class UpgradeMenuUI : MonoBehaviour
{
    // These mirror the base stats on the TaxiController in GameLoop (used only for the stat bars).
    private const float BaseTopSpeed = 40f;
    private const float BaseBrakingDisplay = 2f;
    private const float BaseHandlingDisplay = 3f;

    public static UpgradeMenuUI instance;

    [Header("UI Elements")]
    public TextMeshProUGUI cashText;
    [Header("Costs")]
    public TextMeshProUGUI engineCost;
    public TextMeshProUGUI rimsCost;
    public TextMeshProUGUI radioCost;

    [Header("Labels")]
    public TextMeshProUGUI engineLabel;
    public TextMeshProUGUI rimsLabel;
    public TextMeshProUGUI radioLabel;

    public Slider topSpeedSlider;
    public Slider brakingSlider;
    public Slider handlingSlider;

    public TextMeshProUGUI topSpeedValue;
    public TextMeshProUGUI brakingValue;
    public TextMeshProUGUI handlingValue;

    public Slider taxiSpinSlider;
    public GameObject taxiSpinPoint;

    [Header("Feel")]
    [SerializeField] private float spinResponse = 10f;
    [SerializeField] private float statBarResponse = 6f;
    [SerializeField] private Color affordableColor = Color.white;
    [SerializeField] private Color unaffordableColor = new Color(1f, 0.45f, 0.4f);
    [SerializeField] private Color maxedColor = new Color(0.7f, 0.7f, 0.7f);
    [SerializeField] private Color errorColor = new Color(1f, 0.25f, 0.2f);

    private float _restW = 1f;
    private float _currentYaw;
    private float _targetTopSpeed, _targetBraking, _targetHandling;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        // The original wrote the slider straight into the rotation's Y component and kept the authored W.
        // We reproduce that angle mapping properly: yaw = 2 * atan2(slider, w).
        if (taxiSpinPoint != null) _restW = Mathf.Max(0.05f, Mathf.Abs(taxiSpinPoint.transform.rotation.w));
    }

    private void Start()
    {
        UpgradeManager.Instance.Upgraded += OnUpgraded;
        Statistics.Instance.CurrencyChanged += OnCurrencyChanged;

        ResetRotation();
        _currentYaw = TargetYaw();
        ApplyYaw(_currentYaw);

        UpdateUI();
        SnapStatBars();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (UpgradeManager.Exists) UpgradeManager.Instance.Upgraded -= OnUpgraded;
        if (Statistics.Exists) Statistics.Instance.CurrencyChanged -= OnCurrencyChanged;
    }

    private void Update()
    {
        //SpinTaxi();
        AnimateStatBars();
    }

    // ---------------------------------------------------------------- Showroom

    public void SpinTaxi()
    {
        if (taxiSpinPoint == null) return;
        _currentYaw = Mathf.LerpAngle(_currentYaw, TargetYaw(), 1f - Mathf.Exp(-spinResponse * Time.unscaledDeltaTime));
        ApplyYaw(_currentYaw);
    }

    public void ResetRotation()
    {
        if (taxiSpinSlider != null) taxiSpinSlider.value = -1f;
    }

    private float TargetYaw()
    {
        float value = taxiSpinSlider != null ? taxiSpinSlider.value : -1f;
        return 2f * Mathf.Atan2(value, _restW) * Mathf.Rad2Deg;
    }

    private void ApplyYaw(float yaw)
    {
        taxiSpinPoint.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    // ---------------------------------------------------------------- Display

    public void UpdateUI()
    {
        cashText.SetText("CASH:\nR{0}", Statistics.Instance.Currency);

        UpdateEntry(UpgradeType.Engine, "Engine", engineLabel, engineCost);
        UpdateEntry(UpgradeType.Rims, "Rims", rimsLabel, rimsCost);
        UpdateEntry(UpgradeType.Radio, "Radio", radioLabel, radioCost);

        UpdateStatsUI();
    }

    private void UpdateEntry(UpgradeType type, string title, TextMeshProUGUI label, TextMeshProUGUI cost)
    {
        UpgradeManager upgrades = UpgradeManager.Instance;
        if (label != null) label.text = $"{title}\n({upgrades.GetLevel(type)}/{upgrades.GetMaxLevel(type)})";
        if (cost == null) return;

        if (upgrades.IsMaxed(type))
        {
            cost.text = "Max";
            UIPunch.SetRestColor(cost, maxedColor);
        }
        else
        {
            cost.SetText("R{0}", upgrades.GetCost(type));
            UIPunch.SetRestColor(cost, upgrades.CanAfford(type) ? affordableColor : unaffordableColor);
        }
    }

    public void UpdateStatsUI()
    {
        UpgradeManager upgrades = UpgradeManager.Instance;
        _targetTopSpeed = upgrades.TopSpeedModifier + BaseTopSpeed;
        _targetBraking = upgrades.BrakingModifier + BaseBrakingDisplay;
        _targetHandling = upgrades.StrafeSpeedModifier + BaseHandlingDisplay;

        if (topSpeedValue != null) topSpeedValue.SetText("{0}", GameManager.ToKmh(_targetTopSpeed));
        if (brakingValue != null) brakingValue.SetText("{0}", Mathf.Round(_targetBraking * 10f));
        if (handlingValue != null) handlingValue.SetText("{0}", Mathf.Round(_targetHandling * 10f));
    }

    private void SnapStatBars()
    {
        if (topSpeedSlider != null) topSpeedSlider.value = _targetTopSpeed;
        if (brakingSlider != null) brakingSlider.value = _targetBraking;
        if (handlingSlider != null) handlingSlider.value = _targetHandling;
    }

    private void AnimateStatBars()
    {
        float blend = 1f - Mathf.Exp(-statBarResponse * Time.unscaledDeltaTime);
        if (topSpeedSlider != null) topSpeedSlider.value = Mathf.Lerp(topSpeedSlider.value, _targetTopSpeed, blend);
        if (brakingSlider != null) brakingSlider.value = Mathf.Lerp(brakingSlider.value, _targetBraking, blend);
        if (handlingSlider != null) handlingSlider.value = Mathf.Lerp(handlingSlider.value, _targetHandling, blend);
    }

    private void OnUpgraded(UpgradeType type) => UpdateUI();

    private void OnCurrencyChanged(int currency) => UpdateUI();

    // ---------------------------------------------------------------- Buttons (wired in the scene)

    public void OnEngineUpgrade() => TryBuy(UpgradeType.Engine, engineLabel, engineCost);

    public void OnRimsUpgrade()
    {
        if (TryBuy(UpgradeType.Rims, rimsLabel, rimsCost) && TaxiVisuals.Instance != null)
        {
            ResetRotation();
            TaxiVisuals.Instance.Refresh(true);
        }
    }

    public void OnRadioUpgrade() => TryBuy(UpgradeType.Radio, radioLabel, radioCost);

    private bool TryBuy(UpgradeType type, TextMeshProUGUI label, TextMeshProUGUI cost)
    {
        UpgradeManager upgrades = UpgradeManager.Instance;

        if (upgrades.TryPurchase(type))
        {
            AudioManager.Play("Button Select");
            if (label != null) UIPunch.Punch(this, label.transform, 0.2f);
            UIPunch.Punch(this, cashText.transform, 0.15f);
            Haptics.Light();
            return true;
        }

        // Clear "no" feedback instead of silently ignoring the tap.
        AudioManager.Play("Button Select");
        if (upgrades.IsMaxed(type))
        {
            if (cost != null) UIPunch.Shake(this, cost.rectTransform, 8f);
        }
        else
        {
            UIPunch.Shake(this, cashText.rectTransform);
            UIPunch.Flash(this, cashText, errorColor);
        }
        return false;
    }

    public void OnPlayButtonClicked()
    {
        AudioManager.Play("Button Select");
        SceneManager.LoadScene("GameLoop");
    }

    public void OnBackButtonClicked()
    {
        AudioManager.Play("Button Select");
        SceneManager.LoadScene("MainMenu");
    }
}
