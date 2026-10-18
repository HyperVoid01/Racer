using System;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    // SUBSYSTEM
    
    [Header("Modifiers")]
    public float accelerationModifier = 1f;
    public float decelerationModifier = 1f;
    public float brakingModifier = 1f;
    public float topSpeedModifier = 1f;
    public float strafeSpeedModifier = 1f;
    
    [Header("Modifier Increments")]
    public float accelerationModifierIncrement = 1f;
    public float decelerationIncrement = 1f;
    public float brakingIncrement = 1f;
    public float topSpeedIncrement = 1f;
    public float strafeSpeedIncrement = 1f;
    
    [Header("Upgrades")]
    public int engine = 1;
    public int rims = 1;
    public bool radioPurchased = false;
    
    [Header("Upgrade Limit")]
    public int engineUpgradeLimit = 5;
    public int rimsUpgradeLimit = 5;
    
    [Header("Upgrade Costs")]
    public int engineCost = 100;
    public int rimsCost = 70;
    public int radioCost = 120;
    
    [Header("Upgrade Cost Multiplier")]
    public float costMultiplier = 1.5f;

    public static UpgradeManager Instance;
    
    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        
        DontDestroyOnLoad(gameObject);
        Instance = this;
    }

    public bool CheckSufficientFunds(int cost)
    {
        if (Statistics.Instance.currency >= cost)
        {
            Statistics.Instance.currency -= cost;
            return true;
        }
        return false;
    }

    public void UpgradeEngine()
    {
        AudioManager.Instance.Play("Button Select");
        if (engine == engineUpgradeLimit) return;
        
        if (CheckSufficientFunds(engineCost))
        {
            engine++;
            accelerationModifier += accelerationModifierIncrement;
            decelerationModifier += decelerationIncrement;
            topSpeedModifier += topSpeedIncrement;

            float tempCost = engineCost * costMultiplier;
            engineCost = (int)tempCost;
            
            UpgradeMenuUI.instance.UpdateUI();
        }

    }

    public void UpgradeRims()
    {
        AudioManager.Instance.Play("Button Select");
        if (rims == rimsUpgradeLimit) return;
        
        if (CheckSufficientFunds(rimsCost))
        {
            rims++;
            brakingModifier += brakingIncrement;
            strafeSpeedModifier += strafeSpeedIncrement;
            
            float tempCost = rimsCost * costMultiplier;
            rimsCost = (int)tempCost;
            
            UpgradeMenuUI.instance.UpdateUI();
        }
    }

    public void UpgradeRadio()
    {
        AudioManager.Instance.Play("Button Select");
        if (radioPurchased) return;
        
        if (CheckSufficientFunds(radioCost))
        {
            radioPurchased = true;
            
            UpgradeMenuUI.instance.UpdateUI();
        }
    }
}
