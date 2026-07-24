using System.Collections;
using Micasa.Bridge;
using UnityEngine;

namespace Micasa
{
    public class StageManager : MonoBehaviour
    {
        [SerializeField] private HostWindowCamera  hostCamera;
        [SerializeField] private DVDBounce         dvd;
        [SerializeField] private HostWindowManager hostManager;

        void Awake()
        {
            if (hostCamera  == null) hostCamera  = FindAnyObjectByType<HostWindowCamera>();
            if (dvd         == null) dvd         = FindAnyObjectByType<DVDBounce>();
            if (hostManager == null) hostManager = FindAnyObjectByType<HostWindowManager>();

            if (hostCamera  == null) Debug.LogWarning("[StageManager] HostWindowCamera no encontrado.");
            if (hostManager == null) Debug.LogWarning("[StageManager] HostWindowManager no encontrado — StartPuzzle/StopPuzzle no funcionarán.");
        }

        public void StartSequence(StageData data)
        {
            StopAllCoroutines();
            Debug.Log($"[StageManager] Starting '{data.name}' — {data.steps.Count} steps.");
            StartCoroutine(RunSequence(data));
        }

        IEnumerator RunSequence(StageData data)
        {
            for (int i = 0; i < data.steps.Count; i++)
            {
                var step = data.steps[i];
                Debug.Log($"[StageManager] Step {i}: waiting {step.delay}s.");
                yield return new WaitForSeconds(step.delay);
                Debug.Log($"[StageManager] Step {i}: executing {step.actions.Count} actions.");
                ExecuteStep(step);
            }
            Debug.Log("[StageManager] Sequence complete.");
        }

        void ExecuteStep(StageStep step)
        {
            SendWindowData(AppBootstrap.GnomeBridge,      step.gnome);
            SendWindowData(AppBootstrap.GnomophoneBridge, step.gnomeophone);
            SendWindowData(AppBootstrap.Gnome2Bridge,     step.gnome2);

            foreach (var action in step.actions)
            {
                Debug.Log($"[StageManager] Action: {action}");
                ExecuteAction(action, step);
            }
        }

        private static void SendWindowData(WindowBridge bridge, WindowStepData data)
        {
            if (bridge == null || !bridge.IsConnected) return;

            if (data.lines.Count > 0)
            {
                var seq = new DialogueSequence { lines = data.lines };
                bridge.Send(new BridgeMessage { type = "show-text-sequence", payload = JsonUtility.ToJson(seq) });
            }

            foreach (var ev in data.fmodPlay)
                if (!ev.IsNull)
                    bridge.Send(new BridgeMessage { type = "fmod-play", payload = ((System.Guid)ev.Guid).ToString() });

            foreach (var ev in data.fmodStop)
                if (!ev.IsNull)
                    bridge.Send(new BridgeMessage { type = "fmod-stop", payload = ((System.Guid)ev.Guid).ToString() });
        }

        void ExecuteAction(StageAction action, StageStep step)
        {
            switch (action)
            {
                case StageAction.OpenGnomeWindow:      AppBootstrap.LaunchGnomeWindow();       break;
                case StageAction.OpenGnomophoneWindow: AppBootstrap.LaunchGnomophoneWindow();  break;
                case StageAction.OpenGnome2Window:     AppBootstrap.LaunchGnome2Window();      break;
                case StageAction.StartPuzzle:          hostManager?.StartPuzzle();             break;
                case StageAction.StopPuzzle:           hostManager?.StopPuzzle();              break;
                case StageAction.ToggleTransparency:   hostCamera?.ToggleTransparency();       break;
                case StageAction.ToggleExplorerMode:   hostCamera?.ToggleExplorerMode();       break;
                case StageAction.PlaySquishAnimation:  hostCamera?.PlaySquishAnimation();      break;
                case StageAction.ToggleDVDBounce:      dvd?.Toggle();                          break;
                case StageAction.SetWindowsVolume:     WindowsVolume.Set(step.targetVolume);   break;
                case StageAction.SelfDestruct:         AppBootstrap.SelfDestruct();            break;
                case StageAction.StretchFull:          hostCamera?.PlayStretchFullAnimation();  break;
                case StageAction.StretchWide:          hostCamera?.PlayStretchWideAnimation();  break;
                case StageAction.StretchTall:          hostCamera?.PlayStretchTallAnimation();  break;
                case StageAction.StretchLoop:          hostCamera?.StartStretchLoop();          break;
                case StageAction.StopStretch:          hostCamera?.StopStretch();               break;
            }
        }
    }
}
