using UnityEngine;

namespace Micasa
{
    [RequireComponent(typeof(Collider2D))]
    public class IntroTextTrigger : MonoBehaviour
    {
        void OnTriggerEnter2D(Collider2D other)
        {
            if (AppBootstrap.CameraViewIndex >= 0) return;
            if (!other.CompareTag("Player")) return;
            GameManager.Instance?.ShowIntroText();
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (AppBootstrap.CameraViewIndex >= 0) return;
            if (!other.CompareTag("Player")) return;
            GameManager.Instance?.HideIntroText();
        }
    }
}
