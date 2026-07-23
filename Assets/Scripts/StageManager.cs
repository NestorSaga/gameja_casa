using System.Collections;
using FMODUnity;
using Micasa.Bridge;
using UnityEngine;

namespace Micasa
{
    public class StageManager : MonoBehaviour
    {
        [SerializeField] StageData stageData;

        private HostWindowCamera  hostCamera;
        private DVDBounce         dvd;
        private HostWindowManager hostManager;

        void Start()
        {
            hostCamera  = FindAnyObjectByType<HostWindowCamera>();
            dvd         = FindAnyObjectByType<DVDBounce>();
            hostManager = FindAnyObjectByType<HostWindowManager>();

            if (stageData != null)
                StartCoroutine(RunSequence());
        }

        IEnumerator RunSequence()
        {
            foreach (var step in stageData.steps)
            {
                yield return new WaitForSeconds(step.delay);
                ExecuteStep(step);
            }
        }

        void ExecuteStep(StageStep step)
        {
            SendWindowData(AppBootstrap.GnomeBridge,      step.gnome);
            SendWindowData(AppBootstrap.GnomophoneBridge, step.gnomeophone);
            SendWindowData(AppBootstrap.Gnome2Bridge,     step.gnome2);

            foreach (var action in step.actions)
                ExecuteAction(action, step);
        }

        private static void SendWindowData(WindowBridge bridge, WindowStepData data)
        {
            if (bridge == null || !bridge.IsConnected) return;

            if (!string.IsNullOrEmpty(data.text))
                bridge.Send(new BridgeMessage { type = "show-text", payload = data.text });

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
            }
        }
    }
}
