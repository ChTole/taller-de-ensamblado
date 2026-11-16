using AssemblyWorkshop.Common;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AssemblyWorkshop.Simulation
{
    /// <summary>
    /// Loads a scene by name. Buttons call LoadScene from On Click (), passing the scene name.
    /// </summary>
    [DisallowMultipleComponent]
    public class SceneNavigator : MonoBehaviour
    {
        public void LoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Guard.Error(this, "The scene name is empty.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Guard.Error(this, $"Scene '{sceneName}' is not in the build's scene list.");
                return;
            }

            SceneManager.LoadScene(sceneName);
        }
    }
}
