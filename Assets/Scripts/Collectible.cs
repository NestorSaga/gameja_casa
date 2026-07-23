using UnityEngine;

namespace Micasa
{
    public class Collectible : MonoBehaviour
    {
        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            GameManager.Instance?.AddCollectable();
            Destroy(gameObject);
        }
    }
}
