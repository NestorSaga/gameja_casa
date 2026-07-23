using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Micasa.Bridge;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Micasa
{
    public class AppBootstrap : MonoBehaviour
    {
        [SerializeField] private string hostSceneName        = "HostScene";
        [SerializeField] private string clientSceneName      = "ClientScene";
        [SerializeField] private string gnomeSceneName       = "GnomeScene";
        [SerializeField] private string gnomophoneSceneName  = "GnomophoneScene";
        [SerializeField] private string gnome2SceneName      = "Gnome2Scene";
        [SerializeField] private int    windowWidth          = 800;
        [SerializeField] private int    windowHeight         = 600;

        public const int CameraCount = 8;
        public const int CameraCols  = 4;
        public const int CameraRows  = 2;

        public static int CameraViewIndex { get; private set; } = -1;
        public static int CameraScreenPos { get; private set; } = -1;

        public static WindowBridge GnomeBridge      { get; private set; }
        public static WindowBridge GnomophoneBridge { get; private set; }
        public static WindowBridge Gnome2Bridge     { get; private set; }

        private static readonly List<Process> childProcesses  = new();
        private static readonly List<Process> cameraProcesses = new();

        void OnApplicationFocus(bool _) => Application.runInBackground = true;

        void Awake()
        {
            Application.runInBackground = true;
            Application.wantsToQuit    += OnWantsToQuit;

            var args = System.Environment.GetCommandLineArgs();

            if (args.Contains("--gnome"))
            {
                Screen.SetResolution(windowWidth, windowHeight, FullScreenMode.Windowed);
                CreateBridge(asHost: false, "micasa-gnome-h2c", "micasa-gnome-c2h");
                SceneManager.LoadScene(gnomeSceneName);
                return;
            }

            if (args.Contains("--gnomeophone"))
            {
                Screen.SetResolution(windowWidth, windowHeight, FullScreenMode.Windowed);
                CreateBridge(asHost: false, "micasa-gnomeophone-h2c", "micasa-gnomeophone-c2h");
                SceneManager.LoadScene(gnomophoneSceneName);
                return;
            }

            if (args.Contains("--gnome2"))
            {
                Screen.SetResolution(windowWidth, windowHeight, FullScreenMode.Windowed);
                CreateBridge(asHost: false, "micasa-gnome2-h2c", "micasa-gnome2-c2h");
                SceneManager.LoadScene(gnome2SceneName);
                return;
            }

            ParseCameraArgs(args, out int view, out int pos);
            CameraViewIndex = view;
            CameraScreenPos = pos;

            if (CameraViewIndex >= 0)
            {
                int sw = Display.main.systemWidth;
                int sh = Display.main.systemHeight;
                Screen.SetResolution(sw / CameraCols, sh / CameraRows, FullScreenMode.Windowed);
                SceneManager.LoadScene(hostSceneName);
                return;
            }

            bool isClient = args.Contains("--client");
            Screen.SetResolution(windowWidth, windowHeight, FullScreenMode.Windowed);

            if (isClient)
            {
                CreateBridge(asHost: false, "micasa-h2c", "micasa-c2h");
                SceneManager.LoadScene(clientSceneName);
            }
            else
            {
                // Main player launch: show MainMenu first; bridges are created in InitializeHost()
                SceneManager.LoadScene("MainMenu");
            }
        }

        private static WindowBridge CreateBridge(bool asHost, string h2c, string c2h)
        {
            var go = new GameObject($"WindowBridge-{h2c}");
            var bridge = go.AddComponent<WindowBridge>();
            bridge.Initialize(asHost, h2c, c2h);
            return bridge;
        }

        public static void LaunchCameraProcesses(int skipViewIndex, int[] screenPositions)
        {
            int sw   = Display.main.systemWidth;
            int sh   = Display.main.systemHeight;
            int winW = sw / CameraCols;
            int winH = sh / CameraRows;

            cameraProcesses.Clear();
            for (int v = 0; v < CameraCount; v++)
            {
                if (v == skipViewIndex) continue;
                int screenPos = screenPositions[v];
                int col = screenPos % CameraCols;
                int row = screenPos / CameraRows;
                int x   = col * winW;
                int y   = row * winH;

                var args = $"--camera {v} {screenPos} -screen-width {winW} -screen-height {winH} -screen-x {x} -screen-y {y}";
                var p = Launch(args);
                if (p != null) cameraProcesses.Add(p);
            }
        }

        public static void KillCameraProcesses()
        {
            foreach (var p in cameraProcesses)
            {
                try { if (!p.HasExited) p.Kill(); }
                catch { }
            }
            cameraProcesses.Clear();
        }

        private static bool OnWantsToQuit()
        {
            KillAllChildren();
#if !UNITY_EDITOR
            System.Diagnostics.Process.GetCurrentProcess().Kill();
            return false; // unreachable — process is already dead
#else
            return true;
#endif
        }

        private static void KillAllChildren()
        {
            foreach (var p in childProcesses)
            {
                try { if (!p.HasExited) p.Kill(); }
                catch { }
            }
            childProcesses.Clear();
        }

        public static void InitializeHost()
        {
            if (WindowBridge.Instance != null) return;
            CreateBridge(asHost: true, "micasa-h2c",            "micasa-c2h");
            GnomeBridge      = CreateBridge(asHost: true, "micasa-gnome-h2c",       "micasa-gnome-c2h");
            GnomophoneBridge = CreateBridge(asHost: true, "micasa-gnomeophone-h2c", "micasa-gnomeophone-c2h");
            Gnome2Bridge     = CreateBridge(asHost: true, "micasa-gnome2-h2c",       "micasa-gnome2-c2h");
        }

        public static void LaunchClientProcess()      => Launch("--client");
        public static void LaunchGnomeWindow()        => Launch("--gnome");
        public static void LaunchGnomophoneWindow()   => Launch("--gnomeophone");
        public static void LaunchGnome2Window()       => Launch("--gnome2");

        private static void ParseCameraArgs(string[] args, out int view, out int pos)
        {
            view = -1; pos = -1;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "--camera") continue;
                if (!int.TryParse(args[i + 1], out view)) { view = -1; return; }
                pos = view;
                if (i + 2 < args.Length) int.TryParse(args[i + 2], out pos);
                return;
            }
        }

        private static Process Launch(string arguments)
        {
#if UNITY_EDITOR
            UnityEngine.Debug.Log($"[Bootstrap] Editor: would launch '{arguments}'");
            return null;
#else
            var exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exe)) return null;
            var p = Process.Start(new ProcessStartInfo
            {
                FileName        = exe,
                Arguments       = arguments,
                UseShellExecute = true
            });
            if (p != null) childProcesses.Add(p);
            return p;
#endif
        }
    }
}
