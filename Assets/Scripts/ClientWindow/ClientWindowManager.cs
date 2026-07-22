using Micasa.Bridge;
using UnityEngine;

namespace Micasa
{
    public class ClientWindowManager : MonoBehaviour
    {
        private int _pingsReceived;
        private int _pongsSent;
        private string _status = "Connecting to host...";

        void Start()
        {
            var bridge = WindowBridge.Instance;
            if (bridge == null) { _status = "No bridge — run from Bootstrap scene."; return; }

            bridge.OnConnected.AddListener(() => _status = "Connected to host!");
            bridge.OnDisconnected.AddListener(() => _status = "Host disconnected.");
            bridge.OnMessageReceived.AddListener(OnMessage);

            if (bridge.IsConnected) _status = "Connected to host!";
        }

        private void OnMessage(BridgeMessage msg)
        {
            if (msg.type != "ping") return;
            _pingsReceived++;
            _status = $"Got ping #{_pingsReceived}";

            _pongsSent++;
            WindowBridge.Instance.Send(new BridgeMessage { type = "pong", payload = _pongsSent.ToString() });
        }

        void OnGUI()
        {
            var area = new Rect(30, 30, 400, Screen.height - 60);
            GUILayout.BeginArea(area);

            GUILayout.Label("CLIENT WINDOW", LargeLabel());
            GUILayout.Space(12);
            GUILayout.Label(_status);
            GUILayout.Space(20);
            GUILayout.Label($"Pings received:  {_pingsReceived}");
            GUILayout.Label($"Pongs sent:      {_pongsSent}");
            GUILayout.Space(8);
            GUILayout.Label("(auto-pongs on every ping)", SmallLabel());

            GUILayout.EndArea();
        }

        private static GUIStyle LargeLabel() =>
            new(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };

        private static GUIStyle SmallLabel() =>
            new(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Italic };
    }
}
