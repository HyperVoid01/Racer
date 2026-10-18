using System;
using UnityEngine;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

public class Radio : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button forwardButton;
    [SerializeField] private Button rewindButton;
    
    public int trackIndex = 1;
    public bool paused = false;
    
    private void Start()
    {
        if (UpgradeManager.Instance.radioPurchased)
        {
            StopTrack();
            trackIndex = Random.Range(1, 6);
            PlayTrack(trackIndex);
        }
    }

    public void Update()
    {
        if (UIController.instance._isPaused)
        {
            Pause();
        }
        else if (!paused && !UIController.instance._isPaused)
        {
            UnPause();
        }
    }

    public void StopTrack()
    {
        switch (trackIndex)
        {
            case 1:
            {
                AudioManager.Instance.Stop("Track1");
                break;
            }
            case 2:
            {
                AudioManager.Instance.Stop("Track2");
                break;
            }
            case 3:
            {
                AudioManager.Instance.Stop("Track3");
                break;
            }
            case 4:
            {
                AudioManager.Instance.Stop("Track4");
                break;
            }
            case 5:
            {
                AudioManager.Instance.Stop("Track5");
                break;
            }
        }
    }

    public void PlayTrack(int trackIndex)
    {
        switch (trackIndex)
        {
            case 1:
            {
                AudioManager.Instance.Play("Track1");
                break;
            }
            case 2:
            {
                AudioManager.Instance.Play("Track2");
                break;
            }
            case 3:
            {
                AudioManager.Instance.Play("Track3");
                break;
            }
            case 4:
            {
                AudioManager.Instance.Play("Track4");
                break;
            }
            case 5:
            {
                AudioManager.Instance.Play("Track5");
                break;
            }
        }
        paused = false;
    }

    public void Forward()
    {
        StopTrack();

        trackIndex++;
        
        if (trackIndex > 5)
        {
            trackIndex = 1;
        }
        
        PlayTrack(trackIndex);
    }

    public void Rewind()
    {
        StopTrack();
        
        trackIndex--;
        
        if (trackIndex < 0)
        {
            trackIndex = 5;
        }
        
        PlayTrack(trackIndex);
    }

    public void Pause()
    {
        switch (trackIndex)
        {
            case 1:
            {
                AudioManager.Instance.Pause("Track1");
                break;
            }
            case 2:
            {
                AudioManager.Instance.Pause("Track2");
                break;
            }
            case 3:
            {
                AudioManager.Instance.Pause("Track3");
                break;
            }
            case 4:
            {
                AudioManager.Instance.Pause("Track4");
                break;
            }
            case 5:
            {
                AudioManager.Instance.Pause("Track5");
                break;
            }
        }
        
        paused = true;
    }

    public void UnPause()
    {
        switch (trackIndex)
        {
            case 1:
            {
                AudioManager.Instance.UnPause("Track1");
                break;
            }
            case 2:
            {
                AudioManager.Instance.UnPause("Track2");
                break;
            }
            case 3:
            {
                AudioManager.Instance.UnPause("Track3");
                break;
            }
            case 4:
            {
                AudioManager.Instance.UnPause("Track4");
                break;
            }
            case 5:
            {
                AudioManager.Instance.UnPause("Track5");
                break;
            }
        }

        paused = false;
    }
}
