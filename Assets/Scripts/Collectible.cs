using UnityEngine;

namespace Micasa
{
    public class Collectible : MonoBehaviour
    {
        [SerializeField] bool isLastCollectible;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (AppBootstrap.CameraViewIndex >= 0) return;
            if (!other.CompareTag("Player")) return;
            GameManager.Instance?.AddCollectable();
            if (isLastCollectible)
                GameManager.Instance?.OnLastCollectibleGrabbed();
            gameObject.SetActive(false);
        }
    }
}
