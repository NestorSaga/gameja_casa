using UnityEngine;

namespace Micasa
{
    public class KillZone : MonoBehaviour
    {
        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
                GameManager.Instance?.KillPlayer();
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            var col = GetComponent<Collider2D>();
            if (col != null) Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        }
    }
}
