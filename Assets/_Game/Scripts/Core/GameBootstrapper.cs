using UnityEngine;
using UnityEngine.SceneManagement;

namespace TownBuilder
{
    /// <summary>Boot scene entry point: creates the GameRoot (services, save store) and opens the main menu.</summary>
    public sealed class GameBootstrapper : MonoBehaviour
    {
        [SerializeField] private GameContent content;

        private void Start()
        {
            if (GameRoot.GetOrCreate(content) == null) return;
            SceneManager.LoadScene(SceneNames.MainMenu);
        }
    }
}
