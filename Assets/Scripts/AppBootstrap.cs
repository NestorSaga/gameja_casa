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
        [SerializeField] private string hostSceneName   = "HostScene";
        [SerializeField] private string clientSceneName = "ClientScene";
        [SerializeField] private int    windowWidth     = 800;
        [SerializeField] private int    windowHeight    = 600;

        public const int CameraCount = 8;
        public const int CameraCols  = 4;
        public const int CameraRows  = 2;

        // Qué slice del nivel muestra esta ventana
        public static int CameraViewIndex { get; private set; } = -1;
        // En qué cuadrante de pantalla aparece esta ventana
        public static int CameraScreenPos { get; private set; } = -1;

        private static readonly List<Process> _cameraProcesses = new();

        void Awake()
        {
            Application.runInBackground = true;

            var args = System.Environment.GetCommandLineArgs();
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

            var go = new GameObject("WindowBridge");
            go.AddComponent<WindowBridge>().Initialize(asHost: !isClient);
            SceneManager.LoadScene(isClient ? clientSceneName : hostSceneName);
        }

        // screenPositions[viewIndex] = cuadrante de pantalla asignado a esa vista
        public static void LaunchCameraProcesses(int skipViewIndex, int[] screenPositions)
        {
            int sw   = Display.main.systemWidth;
            int sh   = Display.main.systemHeight;
            int winW = sw / CameraCols;
            int winH = sh / CameraRows;

            _cameraProcesses.Clear();
            for (int v = 0; v < CameraCount; v++)
            {
                if (v == skipViewIndex) continue;
                int screenPos = screenPositions[v];
                int col = screenPos % CameraCols;
                int row = screenPos / CameraRows;
                int x   = col * winW;
                int y   = row * winH;

                // Unity aplica -screen-x/y antes del primer frame, la ventana aparece ya posicionada
                var args = $"--camera {v} {screenPos} -screen-width {winW} -screen-height {winH} -screen-x {x} -screen-y {y}";
                var p = Launch(args);
                if (p != null) _cameraProcesses.Add(p);
            }
        }

        public static void KillCameraProcesses()
        {
            foreach (var p in _cameraProcesses)
            {
                try { if (!p.HasExited) p.Kill(); }
                catch { }
            }
            _cameraProcesses.Clear();
        }

        public static void LaunchClientProcess() => Launch("--client");

        private static void ParseCameraArgs(string[] args, out int view, out int pos)
        {
            view = -1; pos = -1;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "--camera") continue;
                if (!int.TryParse(args[i + 1], out view)) { view = -1; return; }
                pos = view; // fallback: posición = vista
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
            return Process.Start(new ProcessStartInfo
            {
                FileName        = exe,
                Arguments       = arguments,
                UseShellExecute = true
            });
#endif
        }
    }
}
