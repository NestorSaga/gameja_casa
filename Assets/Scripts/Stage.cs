using UnityEngine;

namespace Micasa
{
    public class Stage : MonoBehaviour
    {
        [SerializeField] StageData data;
        [SerializeField] Transform   spawnPoint;
        [SerializeField] Collider2D  goal;

        public Vector3 SpawnPosition       => spawnPoint != null ? spawnPoint.position : Vector3.zero;
        public int     CollectiblesRequired => data != null ? data.collectiblesRequired : 3;

        void Start()
        {
            if (goal != null) goal.enabled = false;
            GameManager.Instance?.SetStage(this);
        }

        public void UnlockGoal()
        {
            if (goal != null) goal.enabled = true;
        }
    }
}
