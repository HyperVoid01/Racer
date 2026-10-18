using UnityEngine;

public class TaxiVisuals : MonoBehaviour
{
    [SerializeField] private GameObject[] taxiPrefabs;
    private GameObject _currentTaxiPrefab;

    public static TaxiVisuals Instance;

    private void Awake()
    {
        Instance = this;
    }
    
    private void Start()
    {
        switch (UpgradeManager.Instance.rims)
        {
            case 0: // Base
            {
                _currentTaxiPrefab = Instantiate(taxiPrefabs[0]);
                break;
            }
            case 1: // Rims only
            {
                _currentTaxiPrefab = Instantiate(taxiPrefabs[1]);
                break;
            }
            case 2: // Rims and Spoiler
            {
                _currentTaxiPrefab = Instantiate(taxiPrefabs[2]);
                break;
            }
        }
        _currentTaxiPrefab.transform.parent = this.transform;
    }

    public void UpdateTaxiVisual()
    {
        UpgradeMenuUI.instance.ResetRotation();
        Destroy(_currentTaxiPrefab);
        switch (UpgradeManager.Instance.rims)
        {
            case 0: // Base
            {
                _currentTaxiPrefab = Instantiate(taxiPrefabs[0]);
                break;
            }
            case 1: // Rims only
            {
                _currentTaxiPrefab = Instantiate(taxiPrefabs[1]);
                break;
            }
            case 2: // Rims and Spoiler
            {
                _currentTaxiPrefab = Instantiate(taxiPrefabs[2]);
                break;
            }
        }
        _currentTaxiPrefab.transform.parent = this.transform;
        
    }
}
