using UnityEngine;

namespace Micasa
{
    public class Collectible : MonoBehaviour
    {
        void OnTriggerEnter2D(Collider2D other)
        {
            if (AppBootstrap.CameraViewIndex >= 0) return;
            if (!other.CompareTag("Player")) return;
            GameManager.Instance?.AddCollectable();
            gameObject.SetActive(false);
        }
    }
}
