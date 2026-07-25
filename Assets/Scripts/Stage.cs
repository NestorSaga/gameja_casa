using System.Collections.Generic;
using UnityEngine;

namespace Micasa
{
    public class Stage : MonoBehaviour
    {
        [SerializeField] StageData   data;
        [SerializeField] Transform   spawnPoint;
        [SerializeField] Collider2D  goal;
        [SerializeField] GameObject  player;

        private List<Collectible> collectibles = new();

        public StageData Data                => data;
        public bool      HasData             => data != null;
        public Vector3   SpawnPosition       => spawnPoint != null ? spawnPoint.position : Vector3.zero;
        public int       CollectiblesRequired => data != null ? data.collectiblesRequired : 0;
        public bool      IsGoalUnlocked      => goal != null && goal.enabled;

        void Start()
        {
            LockGoal();
            collectibles.Clear();
            collectibles.AddRange(GetComponentsInChildren<Collectible>(true));
        }

        public void UnlockGoal()
        {
            if (goal != null) goal.enabled = true;
            goal?.GetComponent<StageExit>()?.Unlock();
        }

        public void LockGoal()
        {
            if (goal != null) goal.enabled = false;
            goal?.GetComponent<StageExit>()?.Lock();
        }

        public void ResetCollectibles()
        {
            foreach (var c in collectibles)
                if (c != null) c.gameObject.SetActive(true);
            LockGoal();
        }

        public void ActivatePlayer()
        {
            if (player == null) return;
            player.transform.position = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
            player.SetActive(true);
        }

        public void DeactivatePlayer()
        {
            if (player != null) player.SetActive(false);
        }
    }
}
