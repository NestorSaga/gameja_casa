using UnityEngine;

namespace Micasa
{
    public class StageExit : MonoBehaviour
    {
        [SerializeField] private Sprite unlockedSprite;

        private SpriteRenderer sr;
        private Sprite         lockedSprite;
        private bool           unlocked = false;

        void Awake()
        {
            sr           = GetComponent<SpriteRenderer>();
            lockedSprite = sr != null ? sr.sprite : null;
        }

        public void Unlock()
        {
            unlocked = true;
            if (sr != null && unlockedSprite != null)
                sr.sprite = unlockedSprite;
        }

        public void Lock()
        {
            unlocked = false;
            if (sr != null && lockedSprite != null)
                sr.sprite = lockedSprite;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (AppBootstrap.CameraViewIndex >= 0) return;
            if (!unlocked) return;
            if (other.CompareTag("Player"))
                GameManager.Instance?.LoadNextStage();
        }
    }
}
