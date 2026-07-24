using UnityEngine;

namespace Micasa
{
    public class StageExit : MonoBehaviour
    {
        [SerializeField] private Sprite unlockedSprite;

        private SpriteRenderer sr;
        private Sprite         lockedSprite;

        void Awake()
        {
            sr           = GetComponent<SpriteRenderer>();
            lockedSprite = sr != null ? sr.sprite : null;
        }

        public void Unlock()
        {
            if (sr != null && unlockedSprite != null)
                sr.sprite = unlockedSprite;
        }

        public void Lock()
        {
            if (sr != null && lockedSprite != null)
                sr.sprite = lockedSprite;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (AppBootstrap.CameraViewIndex >= 0) return;
            if (other.CompareTag("Player"))
                GameManager.Instance?.LoadNextStage();
        }
    }
}
