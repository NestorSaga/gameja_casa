using System;
using System.Collections;
using System.Collections.Generic;
using Micasa.Bridge;
using TMPro;
using UnityEngine;

namespace Micasa
{
    [Serializable]
    public struct FirmaLine
    {
        public string text;
        public float  delay;
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] List<Stage>   stages       = new();
        [SerializeField] StageManager  stageManager;
        [SerializeField] GameObject    loadingScreen;
        [SerializeField] GameObject    spawnPrefab;
        [SerializeField] Transform     spawnPoint;
        [SerializeField] GameObject    gnomePrefab;
        [SerializeField] Transform     spawnPrefabPoint;
        [SerializeField] TextMeshProUGUI gnomeAppearTextUI;

        [Header("Firma Dialogue")]
        [SerializeField] private List<FirmaLine> firmaDialogue = new();

        [Header("Gnome Appear Text")]
        [SerializeField] private string gnomePhrase1;
        [SerializeField] private string gnomePhrase2;
        [SerializeField] private string gnomePhrase3;
        [SerializeField] private float  gnomeDelay1 = 2f;
        [SerializeField] private float  gnomeDelay2 = 2f;
        [SerializeField] private float  gnomeDelay3 = 2f;

        private HostWindowCamera hostCamera;

        [Header("Stage 2 Collectible Texts")]
        [SerializeField] private string stage2Text_collectible2;
        [SerializeField] private string stage2Text_collectible3;

        [Header("Last Collectible")]
        [SerializeField] private string           lastCollectibleText;
        [SerializeField] private List<DialogueLine> lastCollectibleGnome2Lines = new();

        [Header("Intro Text")]
        [SerializeField] private TextMeshProUGUI introTextUI;
        [SerializeField] private string          introText;

        [Header("Debug")]
        [SerializeField] private bool debugMode       = false;
        [SerializeField] private int  debugStartIndex = 0;

        public int  CurrentStageIndex    { get; private set; } = 0;
        public int  CurrentStageId       => CurrentStage?.HasData == true ? CurrentStage.Data.id : -1;
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

            if (AppBootstrap.CameraViewIndex >= 0)
            {
                loadingScreen?.SetActive(false);
                return;
            }

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
                RestorePlayerControl();
                FindAnyObjectByType<CameraPlayerSync>()?.SendStageSync();
            }
        }

        public void LoadNextStageNoLoadingScreen()
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
            next.gameObject.SetActive(true);
            next.ActivatePlayer();
            RestorePlayerControl();
            FindAnyObjectByType<CameraPlayerSync>()?.SendStageSync();
            if (next.HasData)
                stageManager?.StartSequence(next.Data);
            else
            {
                next.gameObject.SetActive(true);
                next.ActivatePlayer();
                RestorePlayerControl();
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
            RestorePlayerControl();
            FindAnyObjectByType<CameraPlayerSync>()?.SendStageSync();
        }

        public void SpawnObject()
        {
            if (spawnPrefab == null) { Debug.LogWarning("[GameManager] SpawnObject: spawnPrefab no asignado."); return; }
            Vector3 pos = spawnPrefabPoint != null ? spawnPrefabPoint.position : Vector3.zero;
            Instantiate(spawnPrefab, pos, Quaternion.identity);
            DisablePlayerControl();
        }

        public void GnomeAppear()
        {
            if (gnomePrefab == null) { Debug.LogWarning("[GameManager] GnomeAppear: gnomePrefab no asignado."); return; }
            Vector3 pos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            var clone = Instantiate(gnomePrefab, pos, Quaternion.identity);
            if (gnomeAppearTextUI != null)
                StartCoroutine(ShowGnomePhrases(clone));
        }

        private IEnumerator ShowGnomePhrases(GameObject gnome)
        {
            gnomeAppearTextUI.text = gnomePhrase1;
            gnomeAppearTextUI.gameObject.SetActive(true);
            yield return new WaitForSeconds(gnomeDelay1);
            gnomeAppearTextUI.text = gnomePhrase2;
            yield return new WaitForSeconds(gnomeDelay2);
            gnomeAppearTextUI.text = gnomePhrase3;
            yield return new WaitForSeconds(gnomeDelay3);
            gnomeAppearTextUI.text = string.Empty;
            gnome.GetComponentInChildren<ExploreWaypatroller>()?.Activate();
        }

        public void DisablePlayerControl()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            var controller = player.GetComponent<PlayerController2D>();
            if (controller != null) controller.enabled = false;
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        public void RestorePlayerControl()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            var controller = player.GetComponent<PlayerController2D>();
            if (controller != null) controller.enabled = true;
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

        public void ShowIntroText()
        {
            if (introTextUI == null) return;
            introTextUI.text = introText;
            introTextUI.gameObject.SetActive(true);
        }

        public void HideIntroText()
        {
            if (introTextUI == null) return;
            introTextUI.text = string.Empty;
            introTextUI.gameObject.SetActive(false);
        }

        public void OnLastCollectibleGrabbed()
        {
            if (gnomeAppearTextUI != null && !string.IsNullOrEmpty(lastCollectibleText))
            {
                gnomeAppearTextUI.text = lastCollectibleText;
                gnomeAppearTextUI.gameObject.SetActive(true);
            }

            if (lastCollectibleGnome2Lines.Count > 0 && AppBootstrap.Gnome2Bridge != null && AppBootstrap.Gnome2Bridge.IsConnected)
            {
                var seq = new DialogueSequence { lines = lastCollectibleGnome2Lines };
                AppBootstrap.Gnome2Bridge.Send(new BridgeMessage
                {
                    type    = "show-text-sequence",
                    payload = JsonUtility.ToJson(seq)
                });
            }
        }

        public void StartFirmaDialogue(Action onComplete) => StartCoroutine(RunFirmaDialogue(onComplete));

        private IEnumerator RunFirmaDialogue(Action onComplete)
        {
            if (gnomeAppearTextUI != null)
            {
                gnomeAppearTextUI.gameObject.SetActive(true);
                foreach (var entry in firmaDialogue)
                {
                    gnomeAppearTextUI.text = entry.text;
                    yield return new WaitForSeconds(entry.delay);
                }
                gnomeAppearTextUI.text = string.Empty;
                gnomeAppearTextUI.gameObject.SetActive(false);
            }
            onComplete?.Invoke();
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
