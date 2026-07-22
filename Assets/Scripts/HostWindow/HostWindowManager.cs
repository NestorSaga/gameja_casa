using Micasa.Bridge;
using UnityEngine;

namespace Micasa
{
    public class HostWindowManager : MonoBehaviour
    {
        private int _pingsSent;
        private int _pongsReceived;
        private string _status = "Waiting for client...";
        private DVDBounce _dvd;
        private HostWindowCamera _hostCamera;

        private bool _puzzleActive;

        void Start()
        {
            if (AppBootstrap.CameraViewIndex >= 0) { enabled = false; return; }

            _dvd        = FindAnyObjectByType<DVDBounce>();
            _hostCamera = FindAnyObjectByType<HostWindowCamera>();

            var bridge = WindowBridge.Instance;
            if (bridge == null) { _status = "No bridge — run from Bootstrap scene."; return; }

            bridge.OnConnected.AddListener(() => _status = "Client connected!");
            bridge.OnDisconnected.AddListener(() => _status = "Client disconnected.");
            bridge.OnMessageReceived.AddListener(OnMessage);

            if (bridge.IsConnected) _status = "Client connected!";
        }

        private void OnMessage(BridgeMessage msg)
        {
            if (msg.type != "pong") return;
            _pongsReceived++;
            _status = $"Got pong #{_pongsReceived}";
        }

        void OnGUI()
        {
            var area = new Rect(30, 30, 400, Screen.height - 60);
            GUILayout.BeginArea(area);

            if (_puzzleActive)
            {
                DrawPuzzleUI();
            }
            else
            {
                DrawHostUI();
            }

            GUILayout.EndArea();
        }

        private void DrawHostUI()
        {
            GUILayout.Label("HOST WINDOW", LargeLabel());
            GUILayout.Space(12);
            GUILayout.Label(_status);
            GUILayout.Space(20);
            GUILayout.Label($"Pings sent:      {_pingsSent}");
            GUILayout.Label($"Pongs received:  {_pongsReceived}");
            GUILayout.Space(20);

            var bridge = WindowBridge.Instance;

            if (!(bridge?.IsConnected ?? false))
            {
                if (GUILayout.Button("Open Client", GUILayout.Width(120), GUILayout.Height(44)))
                    AppBootstrap.LaunchClientProcess();
                GUILayout.Space(10);
            }

            GUI.enabled = bridge != null && bridge.IsConnected;
            if (GUILayout.Button("Ping  →", GUILayout.Width(120), GUILayout.Height(44)))
            {
                _pingsSent++;
                bridge.Send(new BridgeMessage { type = "ping", payload = _pingsSent.ToString() });
                _status = $"Sent ping #{_pingsSent}";
            }
            GUI.enabled = true;

            if (_dvd != null)
            {
                GUILayout.Space(10);
                if (GUILayout.Button(_dvd.IsBouncing ? "Stop DVD" : "DVD", GUILayout.Width(120), GUILayout.Height(44)))
                    _dvd.Toggle();
            }

            GUILayout.Space(20);
            if (GUILayout.Button("Squish Test", GUILayout.Width(120), GUILayout.Height(44)))
                _hostCamera?.PlaySquishAnimation();

            GUILayout.Space(4);
            bool transparent = _hostCamera != null && _hostCamera.IsTransparent;
            if (GUILayout.Button(transparent ? "Opaque" : "Transparent", GUILayout.Width(120), GUILayout.Height(44)))
                _hostCamera?.ToggleTransparency();

            GUILayout.Space(8);
            bool exploring = _hostCamera != null && _hostCamera.ExplorerMode;
            if (GUILayout.Button(exploring ? "Overview" : "Explorer", GUILayout.Width(120), GUILayout.Height(44)))
                _hostCamera?.ToggleExplorerMode();

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

        private void StartPuzzle()
        {
            // screenPositions[viewIndex] = cuadrante de pantalla asignado
            int[] screenPositions = ShuffledRange(AppBootstrap.CameraCount);
            int   hostViewIndex   = UnityEngine.Random.Range(0, AppBootstrap.CameraCount);
            int   hostScreenPos   = screenPositions[hostViewIndex];

            _puzzleActive = true;
            _hostCamera?.EnterCameraMode(hostViewIndex, hostScreenPos);
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

        private void StopPuzzle()
        {
            _puzzleActive = false;
            AppBootstrap.KillCameraProcesses();
            _hostCamera?.ExitCameraMode();
        }

        private static GUIStyle LargeLabel() =>
            new(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
    }
}
