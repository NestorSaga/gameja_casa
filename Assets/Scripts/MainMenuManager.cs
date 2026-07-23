using UnityEngine;
using UnityEngine.SceneManagement;

namespace Micasa
{
    public class MainMenuManager : MonoBehaviour
    {
        public void StartGame() => SceneManager.LoadScene("HostScene");
    }
}
