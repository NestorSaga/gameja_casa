using System.Collections.Generic;
using UnityEngine;

namespace Micasa
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] List<Stage>   stages       = new();
        [SerializeField] StageManager  stageManager;
        [SerializeField] GameObject    loadingScreen;

        private HostWindowCamera hostCamera;

        [Header("Stage 2 Collectible Texts")]
        [SerializeField] private string stage2Text_collectible2;
        [SerializeField] private string stage2Text_collectible3;

        [Header("Debug")]
        [SerializeField] private bool debugMode       = false;
        [SerializeField] private int  debugStartIndex = 0;

        public int  CurrentStageIndex    { get; private set; } = 0;
        public int  CollectablesGathered { get; private set; } = 0;

        Stage CurrentStage => stages.Count > 0 && CurrentStageIndex < stages.Count
            ? stages[CurrentStageIndex]
            : null;

        int EffectiveCollectiblesRequired
        {
            get
            {
                for (int i = CurrentStageIndex; i >= 0; i--)
                    if (stages[i].HasData) return stages[i].CollectiblesRequired;
                return 0;
            }
        }

        public bool AllCollected => EffectiveCollectiblesRequired > 0 &&
                                    CollectablesGathered >= EffectiveCollectiblesRequired;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            hostCamera = FindAnyObjectByType<HostWindowCamera>();

            if (debugMode)
            {
                if (debugStartIndex > 0 && debugStartIndex < stages.Count)
                {
                    CurrentStageIndex = debugStartIndex;
                    Debug.Log($"[GameManager] DEBUG: empezando en stage index={debugStartIndex}");
                }
                else if (debugStartIndex != 0)
                    Debug.LogWarning($"[GameManager] DEBUG: index {debugStartIndex} fuera de rango, empezando desde 0");
            }

            for (int i = 0; i < stages.Count; i++)
                stages[i].gameObject.SetActive(i == CurrentStageIndex);

            if (debugMode && CurrentStageIndex > 0)
                stages[CurrentStageIndex].ActivatePlayer();
        }

        void Start()
        {
            if (AppBootstrap.CameraViewIndex >= 0) return;
            if (CurrentStage != null && CurrentStage.HasData)
                stageManager?.StartSequence(CurrentStage.Data);
        }

        public void AddCollectable()
        {
            CollectablesGathered++;

            int required = EffectiveCollectiblesRequired;
            Debug.Log($"[GameManager] {CollectablesGathered}/{required}");

            if (required == 0)
            {
                Debug.LogWarning($"[GameManager] Stage {CurrentStageIndex} tiene CollectiblesRequired=0. ¿Falta asignar el StageData en el componente Stage?");
                return;
            }

            if (CurrentStage.HasData && CurrentStage.Data.id == 2)
                TriggerStage2StretchEffect();

            if (AllCollected)
                CurrentStage.UnlockGoal();
        }

        private void TriggerStage2StretchEffect()
        {
            switch (CollectablesGathered)
            {
                case 2:
                    hostCamera?.PlayStretchTallAnimation();
                    stageManager?.SendGnomeText(stage2Text_collectible2);
                    break;
                case 3:
                    hostCamera?.StartStretchLoop();
                    stageManager?.SendGnomeText(stage2Text_collectible3);
                    break;
            }
        }

        public void LoadNextStage()
        {
            if (CurrentStage.HasData && CurrentStage.Data.id == 2)
                hostCamera?.StopStretch();

            DeactivateAllPlayers();
            CurrentStage?.gameObject.SetActive(false);
            CurrentStageIndex++;
            CollectablesGathered = 0;

            if (CurrentStageIndex >= stages.Count)
            {
                Debug.Log("[GameManager] No hay más stages.");
                return;
            }

            var next = stages[CurrentStageIndex];
            if (next.HasData)
            {
                loadingScreen?.SetActive(true);
                stageManager?.StartSequence(next.Data);
            }
            else
            {
                next.gameObject.SetActive(true);
                next.ActivatePlayer();
                FindAnyObjectByType<CameraPlayerSync>()?.SendStageSync();
            }
        }

        public void LoadStageFromData(StageData data)
        {
            var stage = stages.Find(s => s.HasData && s.Data == data);
            if (stage == null)
            {
                Debug.LogWarning($"[GameManager] LoadStageFromData: ningún stage tiene el StageData '{data?.name}'.");
                return;
            }
            loadingScreen?.SetActive(false);
            stage.gameObject.SetActive(true);
            stage.ActivatePlayer();
            FindAnyObjectByType<CameraPlayerSync>()?.SendStageSync();
        }

        public void UnlockCurrentGoal() => CurrentStage?.UnlockGoal();

        public void ToggleLoadingScreen()
        {
            if (loadingScreen != null)
                loadingScreen.SetActive(!loadingScreen.activeSelf);
        }

        public void ForceActiveStage(int index)
        {
            if (index < 0 || index >= stages.Count) return;
            for (int i = 0; i < stages.Count; i++)
                stages[i].gameObject.SetActive(i == index);
        }

        private void DeactivateAllPlayers()
        {
            foreach (var go in GameObject.FindGameObjectsWithTag("Player"))
                go.SetActive(false);
        }

        public void KillPlayer()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            player.transform.SetParent(null, true);
            if (CurrentStage != null)
                player.transform.position = CurrentStage.SpawnPosition;

            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;

            CollectablesGathered = 0;
            CurrentStage?.ResetCollectibles();
        }
    }
}
