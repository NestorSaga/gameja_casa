using System.Collections;
using System.Collections.Generic;
using FMODUnity;
using Micasa.Bridge;
using UnityEngine;

namespace Micasa
{
    public class StageManager : MonoBehaviour
    {
        [SerializeField] private HostWindowCamera  hostCamera;
        [SerializeField] private DVDBounce         dvd;
        [SerializeField] private HostWindowManager hostManager;
        [SerializeField] private GameObject        lockGameObject;

        private StageData _currentData;
        private readonly Dictionary<string, FMOD.Studio.EventInstance> _hostPlaying = new();

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
            _currentData = data;
            for (int i = 0; i < data.steps.Count; i++)
            {
                var step = data.steps[i];
                Debug.Log($"[StageManager] Step {i}: waiting {step.delay}s.");
                yield return new WaitForSeconds(step.delay);
                Debug.Log($"[StageManager] Step {i}: executing {step.actions.Count} actions.");
                ExecuteStep(step);
            }
            _currentData = null;
            Debug.Log("[StageManager] Sequence complete.");
        }

        void ExecuteStep(StageStep step)
        {
            ExecuteHostFmod(step.hostFmodPlay, step.hostFmodStop);
            SendWindowData(AppBootstrap.GnomeBridge,      step.gnome);
            SendWindowData(AppBootstrap.GnomophoneBridge, step.gnomeophone);
            SendWindowData(AppBootstrap.Gnome2Bridge,     step.gnome2);

            foreach (var action in step.actions)
            {
                Debug.Log($"[StageManager] Action: {action}");
                ExecuteAction(action, step);
            }
        }

        public void SendGnomeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var stepData = new WindowStepData();
            stepData.lines.Add(new DialogueLine { text = text });
            SendWindowData(AppBootstrap.GnomeBridge, stepData);
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
                case StageAction.OpenGnomeWindow:       AppBootstrap.LaunchGnomeWindow();       break;
                case StageAction.OpenGnomophoneWindow:  AppBootstrap.LaunchGnomophoneWindow();  break;
                case StageAction.OpenGnome2Window:      AppBootstrap.LaunchGnome2Window();      break;
                case StageAction.CloseGnomeWindow:      AppBootstrap.CloseGnomeWindow();        break;
                case StageAction.CloseGnomophoneWindow: AppBootstrap.CloseGnomophoneWindow();   break;
                case StageAction.CloseGnome2Window:     AppBootstrap.CloseGnome2Window();       break;
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
                case StageAction.StretchLoop:          hostCamera?.StartStretchLoop();                              break;
                case StageAction.StopStretch:          hostCamera?.StopStretch();                                   break;
                case StageAction.HideLoadingScreen:    GameManager.Instance?.LoadStageFromData(_currentData);       break;
                case StageAction.LockGame:             lockGameObject?.SetActive(true);                             break;
                case StageAction.OpenURL:              if (!string.IsNullOrEmpty(step.url)) Application.OpenURL(step.url); break;
                case StageAction.GenerateFile:         GenerateFile(step);                                          break;
                case StageAction.ToggleLoadingScreen:  GameManager.Instance?.ToggleLoadingScreen();                 break;
                case StageAction.TogglePhysics:        Physics2D.simulationMode = Physics2D.simulationMode == SimulationMode2D.FixedUpdate
                                                           ? SimulationMode2D.Script
                                                           : SimulationMode2D.FixedUpdate;                          break;
                case StageAction.SpawnObject:          GameManager.Instance?.SpawnObject();                         break;
                case StageAction.RestorePlayerControl: GameManager.Instance?.RestorePlayerControl();                 break;
                case StageAction.GnomeAppear:          GameManager.Instance?.GnomeAppear();                          break;
            }
        }
        private void ExecuteHostFmod(List<EventReference> toPlay, List<EventReference> toStop)
        {
            foreach (var ev in toPlay)
            {
                if (ev.IsNull) continue;
                string key = ((System.Guid)ev.Guid).ToString();
                if (key == "00000000-0000-0000-0000-000000000000") continue;
                if (_hostPlaying.ContainsKey(key)) continue;
                try
                {
                    var inst = RuntimeManager.CreateInstance(ev);
                    inst.start();
                    _hostPlaying[key] = inst;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[StageManager] FMOD play error ({key}): {e.Message}");
                }
            }

            foreach (var ev in toStop)
            {
                if (ev.IsNull) continue;
                string key = ((System.Guid)ev.Guid).ToString();
                if (!_hostPlaying.TryGetValue(key, out var inst)) continue;
                inst.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                inst.release();
                _hostPlaying.Remove(key);
            }
        }

        void OnDestroy()
        {
            foreach (var inst in _hostPlaying.Values)
            {
                inst.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                inst.release();
            }
            _hostPlaying.Clear();
        }


        private void GenerateFile(StageStep step)
        {
            if (step.fileTemplate == null)
            {
                Debug.LogWarning("[StageManager] GenerateFile: no hay fileTemplate asignado.");
                return;
            }

            string fileName = string.IsNullOrEmpty(step.outputFileName)
                ? step.fileTemplate.name + ".txt"
                : step.outputFileName;

            string content = step.fileTemplate.text;
            if (fileName == "escritura_de_propiedad.txt")
            {
                content = content
                    .Replace("+++", SystemInfo.processorType)
                    .Replace("---", System.DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            }

            string path = WriteFileWithFallback(fileName, content);
            if (path != null && fileName == "escritura_de_propiedad.txt")
                StartCoroutine(WatchEscritura(path));
        }

        private static string WriteFileWithFallback(string fileName, string content)
        {
            string gameDir = System.IO.Path.GetDirectoryName(Application.dataPath) ?? "";
            string primary = System.IO.Path.Combine(gameDir, fileName);
            try
            {
                System.IO.File.WriteAllText(primary, content, System.Text.Encoding.UTF8);
                Debug.Log($"[StageManager] Archivo escrito en '{primary}'.");
                return primary;
            }
            catch { }

            string desktop  = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
            string fallback = System.IO.Path.Combine(desktop, fileName);
            try
            {
                System.IO.File.WriteAllText(fallback, content, System.Text.Encoding.UTF8);
                Debug.LogWarning($"[StageManager] Sin permisos en '{gameDir}', archivo en Escritorio: '{fallback}'.");
                return fallback;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[StageManager] No se pudo escribir '{fileName}': {e.Message}");
                return null;
            }
        }

        private IEnumerator WatchEscritura(string path)
        {
            const string prefix = "Yo, el/la abajo firmante:";
            while (true)
            {
                yield return new WaitForSeconds(0.5f);
                if (!System.IO.File.Exists(path)) continue;

                string[] lines = null;
                try
                {
                    // FileShare.ReadWrite permite leer aunque el editor tenga el archivo abierto.
                    using var fs = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
                    using var sr = new System.IO.StreamReader(fs, System.Text.Encoding.UTF8);
                    lines = sr.ReadToEnd().Split('\n');
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[StageManager] WatchEscritura: no se pudo leer '{path}': {e.Message}");
                    continue; // el coroutine sigue vivo, reintenta en 0.5s
                }

                foreach (var rawLine in lines)
                {
                    string line = rawLine.TrimEnd('\r');
                    if (!line.StartsWith(prefix)) continue;
                    if (line.Substring(prefix.Length).Trim().Length > 0)
                    {
                        var gnome = FindAnyObjectByType<ExploreWaypatroller>();
                        GameManager.Instance?.StartFirmaDialogue(() => gnome?.Vanish());
                        yield break;
                    }
                    break;
                }
            }
        }
    }
}
