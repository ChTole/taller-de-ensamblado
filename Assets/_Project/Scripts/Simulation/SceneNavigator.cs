using AssemblyWorkshop.Common;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AssemblyWorkshop.Simulation
{
    /// <summary>
    /// Loads a scene by name, or closes the application. Buttons call LoadScene from On Click (),
    /// passing the scene name, or Quit.
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

        /// <summary>
        /// Closes the application. In the editor it stops Play Mode. In a web build it does nothing:
        /// a page cannot close itself, so the web version should not show this button.
        /// </summary>
        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}