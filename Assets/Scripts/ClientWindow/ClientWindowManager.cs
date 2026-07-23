using Micasa.Bridge;
using UnityEngine;

namespace Micasa
{
    public class ClientWindowManager : MonoBehaviour
    {
        private int    pingsReceived;
        private int    pongsSent;
        private string status = "Connecting to host...";

        void Start()
        {
            var bridge = WindowBridge.Instance;
            if (bridge == null) { status = "No bridge — run from Bootstrap scene."; return; }

            bridge.OnConnected.AddListener(() => status = "Connected to host!");
            bridge.OnDisconnected.AddListener(() => status = "Host disconnected.");
            bridge.OnMessageReceived.AddListener(OnMessage);

            if (bridge.IsConnected) status = "Connected to host!";
        }

        private void OnMessage(BridgeMessage msg)
        {
            if (msg.type != "ping") return;
            pingsReceived++;
            status = $"Got ping #{pingsReceived}";

            pongsSent++;
            WindowBridge.Instance.Send(new BridgeMessage { type = "pong", payload = pongsSent.ToString() });
        }

        void OnGUI()
        {
            var area = new Rect(30, 30, 400, Screen.height - 60);
            GUILayout.BeginArea(area);

            GUILayout.Label("CLIENT WINDOW", LargeLabel());
            GUILayout.Space(12);
            GUILayout.Label(status);
            GUILayout.Space(20);
            GUILayout.Label($"Pings received:  {pingsReceived}");
            GUILayout.Label($"Pongs sent:      {pongsSent}");
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
