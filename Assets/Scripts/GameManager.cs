using UnityEngine;
using UnityEngine.SceneManagement;

namespace Micasa
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public int  CurrentStage         { get; private set; } = 1;
        public int  CollectablesGathered  { get; private set; } = 0;
        public bool AllCollected          => currentStage != null &&
                                             CollectablesGathered >= currentStage.CollectiblesRequired;

        Stage currentStage;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SetStage(Stage stage)
        {
            currentStage     = stage;
            CollectablesGathered = 0;
        }

        public void AddCollectable()
        {
            CollectablesGathered++;
            Debug.Log($"[GameManager] {CollectablesGathered}/{currentStage?.CollectiblesRequired}");

            if (AllCollected)
                currentStage.UnlockGoal();
        }

        public void KillPlayer()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            player.transform.SetParent(null, true);
            if (currentStage != null)
                player.transform.position = currentStage.SpawnPosition;

            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        public void LoadNextStage()
        {
            CurrentStage++;
            int next = SceneManager.GetActiveScene().buildIndex + 1;
            if (next < SceneManager.sceneCountInBuildSettings)
                SceneManager.LoadScene(next);
            else
                Debug.Log("[GameManager] No hay más stages.");
        }
    }
}
