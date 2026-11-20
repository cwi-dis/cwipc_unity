using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Loads another scene, by name. Meant to be called from UI buttons, wired in the Inspector
/// (Button.OnClick, with the scene name as argument), as in the cwipc_scene_switcher prefab.
/// The scene must be in the build list. See cwi-dis/cwipc_unity#78.
/// </summary>
public class SampleSceneSwitcher : MonoBehaviour
{
    public void LoadScene(string sceneName)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"{name}: scene \"{sceneName}\" is not in the build list");
            return;
        }
        SceneManager.LoadScene(sceneName);
    }
}
