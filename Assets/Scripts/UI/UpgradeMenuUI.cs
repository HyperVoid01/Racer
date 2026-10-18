using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UpgradeMenuUI : MonoBehaviour
{
    // CLIENT / INTERFACE
    
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
    private Quaternion rotation;

    public static UpgradeMenuUI instance;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }
    
    private void Start()
    {
        taxiSpinSlider.value = -1;
        rotation = taxiSpinPoint.transform.rotation;
        rotation.y = -1;
        taxiSpinPoint.transform.rotation = rotation;
        
        UpdateUI();
    }

    private void Update()
    {
        SpinTaxi();
    }

    public void SpinTaxi()
    {
        rotation.y = taxiSpinSlider.value;
        taxiSpinPoint.transform.rotation = rotation;
    }

    public void ResetRotation()
    {
        taxiSpinSlider.value = -1;
        rotation = taxiSpinPoint.transform.rotation;
        rotation.y = -1;
        taxiSpinPoint.transform.rotation = rotation;
    }

    public void UpdateUI()
    {
        cashText.text = $"CASH:\nR{Statistics.Instance.currency}";
        
        engineCost.text = $"R{UpgradeManager.Instance.engineCost.ToString()}";
        rimsCost.text = $"R{UpgradeManager.Instance.rimsCost.ToString()}";
        
        engineLabel.text = $"Engine\n({UpgradeManager.Instance.engine}/{UpgradeManager.Instance.engineUpgradeLimit})";
        rimsLabel.text = $"Rims\n({UpgradeManager.Instance.rims}/{UpgradeManager.Instance.rimsUpgradeLimit})";

        if (UpgradeManager.Instance.radioPurchased)
        {
            radioLabel.text = "Radio\n(1/1)";
        }
        else
        {
            radioLabel.text = "Radio\n(0/1)";
        }

        if (UpgradeManager.Instance.engine == UpgradeManager.Instance.engineUpgradeLimit)
        {
            engineCost.text = "Max";
        }

        if (UpgradeManager.Instance.rims == UpgradeManager.Instance.rimsUpgradeLimit)
        {
            rimsCost.text = "Max";
        }

        if (UpgradeManager.Instance.radioPurchased)
        {
            radioCost.text = "Max";
        }
        
        UpdateStatsUI();
    }

    public void UpdateStatsUI()
    {
        topSpeedSlider.value = UpgradeManager.Instance.topSpeedModifier + 40;
        brakingSlider.value = UpgradeManager.Instance.brakingModifier + 2;
        handlingSlider.value = UpgradeManager.Instance.strafeSpeedModifier + 3;
        
        topSpeedValue.text = (UpgradeManager.Instance.topSpeedModifier + 40 + 20).ToString();
        brakingValue.text = ((UpgradeManager.Instance.brakingModifier + 2) * 10).ToString();
        handlingValue.text = ((UpgradeManager.Instance.strafeSpeedModifier + 3) * 10).ToString();
    }

    public void OnEngineUpgrade()
    {
        UpgradeManager.Instance.UpgradeEngine();
    }

    public void OnRimsUpgrade()
    {
        UpgradeManager.Instance.UpgradeRims();
        TaxiVisuals.Instance.UpdateTaxiVisual();
    }

    public void OnRadioUpgrade()
    {
        if (!UpgradeManager.Instance.radioPurchased)
        {
            UpgradeManager.Instance.UpgradeRadio();
        }
    }

    public void OnPlayButtonClicked()
    {
        AudioManager.Instance.Play("Button Select");
        SceneManager.LoadScene("GameLoop");
    }
    
    public void OnBackButtonClicked()
    {
        AudioManager.Instance.Play("Button Select");
        SceneManager.LoadScene("MainMenu");
    }
}
