using UnityEngine;

namespace Micasa
{
    public class StageExit : MonoBehaviour
    {
        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
                GameManager.Instance?.LoadNextStage();
        }
    }
}
