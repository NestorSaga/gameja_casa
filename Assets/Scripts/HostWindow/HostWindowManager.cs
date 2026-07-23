using Micasa.Bridge;
using UnityEngine;

namespace Micasa
{
    public class HostWindowManager : MonoBehaviour
    {
        private int              pingsSent;
        private int              pongsReceived;
        private string           status = "Waiting for client...";
        private DVDBounce        dvd;
        private HostWindowCamera hostCamera;
        private bool             puzzleActive;

        void Start()
        {
            if (AppBootstrap.CameraViewIndex >= 0) { enabled = false; return; }

            AppBootstrap.InitializeHost();

            dvd        = FindAnyObjectByType<DVDBounce>();
            hostCamera = FindAnyObjectByType<HostWindowCamera>();

            var bridge = WindowBridge.Instance;
            if (bridge == null) { status = "No bridge."; return; }

            bridge.OnConnected.AddListener(() => status = "Client connected!");
            bridge.OnDisconnected.AddListener(() => status = "Client disconnected.");
            bridge.OnMessageReceived.AddListener(OnMessage);

            if (bridge.IsConnected) status = "Client connected!";
        }

        private void OnMessage(BridgeMessage msg)
        {
            if (msg.type != "pong") return;
            pongsReceived++;
            status = $"Got pong #{pongsReceived}";
        }

        void OnGUI()
        {
            var area = new Rect(30, 30, 400, Screen.height - 60);
            GUILayout.BeginArea(area);

            if (puzzleActive)
                DrawPuzzleUI();
            else
                DrawHostUI();

            GUILayout.EndArea();
        }

        private void DrawHostUI()
        {
            GUILayout.Label("HOST WINDOW", LargeLabel());
            GUILayout.Space(12);
            GUILayout.Label(status);
            GUILayout.Space(20);
            GUILayout.Label($"Pings sent:      {pingsSent}");
            GUILayout.Label($"Pongs received:  {pongsReceived}");
            GUILayout.Space(20);

            var bridge = WindowBridge.Instance;

            if (!(bridge?.IsConnected ?? false))
            {
                if (GUILayout.Button("Open Client", GUILayout.Width(120), GUILayout.Height(44)))
                    AppBootstrap.LaunchClientProcess();
                GUILayout.Space(4);
            }

            GUILayout.Label("Gnome Windows");
            if (GUILayout.Button("Gnome",       GUILayout.Width(120), GUILayout.Height(36)))
                AppBootstrap.LaunchGnomeWindow();
            GUILayout.Space(2);
            if (GUILayout.Button("Gnomeophone", GUILayout.Width(120), GUILayout.Height(36)))
                AppBootstrap.LaunchGnomophoneWindow();
            GUILayout.Space(2);
            if (GUILayout.Button("Gnome 2",     GUILayout.Width(120), GUILayout.Height(36)))
                AppBootstrap.LaunchGnome2Window();
            GUILayout.Space(10);

            GUI.enabled = bridge != null && bridge.IsConnected;
            if (GUILayout.Button("Ping  →", GUILayout.Width(120), GUILayout.Height(44)))
            {
                pingsSent++;
                bridge.Send(new BridgeMessage { type = "ping", payload = pingsSent.ToString() });
                status = $"Sent ping #{pingsSent}";
            }
            GUI.enabled = true;

            if (dvd != null)
            {
                GUILayout.Space(10);
                if (GUILayout.Button(dvd.IsBouncing ? "Stop DVD" : "DVD", GUILayout.Width(120), GUILayout.Height(44)))
                    dvd.Toggle();
            }

            GUILayout.Space(20);
            if (GUILayout.Button("Squish Test", GUILayout.Width(120), GUILayout.Height(44)))
                hostCamera?.PlaySquishAnimation();

            GUILayout.Space(4);
            bool transparent = hostCamera != null && hostCamera.IsTransparent;
            if (GUILayout.Button(transparent ? "Opaque" : "Transparent", GUILayout.Width(120), GUILayout.Height(44)))
                hostCamera?.ToggleTransparency();

            GUILayout.Space(8);
            bool exploring = hostCamera != null && hostCamera.ExplorerMode;
            if (GUILayout.Button(exploring ? "Overview" : "Explorer", GUILayout.Width(120), GUILayout.Height(44)))
                hostCamera?.ToggleExplorerMode();

            GUILayout.Space(8);
            if (GUILayout.Button("Start Puzzle", GUILayout.Width(120), GUILayout.Height(44)))
                StartPuzzle();
        }

        private void DrawPuzzleUI()
        {
            GUILayout.Label("PUZZLE MODE", LargeLabel());
            GUILayout.Space(20);
            if (GUILayout.Button("Close Cameras", GUILayout.Width(140), GUILayout.Height(44)))
                StopPuzzle();
        }

        public void StartPuzzle()
        {
            // screenPositions[viewIndex] = cuadrante de pantalla asignado
            int[] screenPositions = ShuffledRange(AppBootstrap.CameraCount);
            int   hostViewIndex   = UnityEngine.Random.Range(0, AppBootstrap.CameraCount);
            int   hostScreenPos   = screenPositions[hostViewIndex];

            puzzleActive = true;
            hostCamera?.EnterCameraMode(hostViewIndex, hostScreenPos);
            AppBootstrap.LaunchCameraProcesses(hostViewIndex, screenPositions);
        }

        private static int[] ShuffledRange(int count)
        {
            var arr = new int[count];
            for (int i = 0; i < count; i++) arr[i] = i;
            for (int i = count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
            return arr;
        }

        public void StopPuzzle()
        {
            puzzleActive = false;
            AppBootstrap.KillCameraProcesses();
            hostCamera?.ExitCameraMode();
        }

        private static GUIStyle LargeLabel() =>
            new(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
    }
}
