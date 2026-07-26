using UnityEngine;

namespace Micasa
{
    [RequireComponent(typeof(Collider2D))]
    public class StageAdvanceTrigger : MonoBehaviour
    {
        [SerializeField] private int    triggerOnStageId = 6;
        [SerializeField] private string playerTag        = "Player";

        void OnTriggerEnter2D(Collider2D other)
        {
            if (AppBootstrap.CameraViewIndex >= 0) return;
            if (!other.CompareTag(playerTag)) return;
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentStageId != triggerOnStageId) return;

            GameManager.Instance.LoadNextStageNoLoadingScreen();
        }
    }
}
