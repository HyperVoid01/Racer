using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runs once before the first scene loads, on every platform.
/// Applies platform settings (frame rate, orientation, multitouch) and owns app-lifecycle hooks
/// such as flushing the save file when the app is backgrounded on mobile.
/// Nothing needs to be placed in a scene for this to work.
/// </summary>
public class Bootstrap : MonoBehaviour
{
    /// <summary>
    /// True on phones/tablets. Uses UnityEngine.Device so the editor's Device Simulator counts as mobile
    /// (the simulator only fakes the UnityEngine.Device versions of Application/Screen/SystemInfo).
    /// </summary>
    public static bool IsMobile =>
        UnityEngine.Device.Application.isMobilePlatform ||
        UnityEngine.Device.Application.platform == RuntimePlatform.Android ||
        UnityEngine.Device.Application.platform == RuntimePlatform.IPhonePlayer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SaveSystem.Load();

        Input.multiTouchEnabled = true;

        if (IsMobile)
        {
            // Mobile defaults to 30 fps. A runner feels much better at 60.
            Application.targetFrameRate = 60;

            // Landscape only, either way round.
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
        }

        var runner = new GameObject("[Bootstrap]");
        runner.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(runner);
        runner.AddComponent<Bootstrap>();
    }

    private void OnEnable()
    {
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        // Pooled objects belonged to the scene that was just unloaded.
        PrefabPool.Clear();
        UIPunch.Clear();
        SaveSystem.Flush();
    }

    private void OnApplicationPause(bool paused)
    {
        // Mobile apps can be killed while in the background without OnApplicationQuit being called.
        if (paused) SaveSystem.Flush();
    }

    private void OnApplicationQuit()
    {
        SaveSystem.Flush();
    }
}
