using System;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.Events;

namespace Micasa.Bridge
{
    public class WindowBridge : MonoBehaviour
    {
        public static WindowBridge Instance { get; private set; }
        public bool IsConnected { get; private set; }

        public UnityEvent OnConnected = new();
        public UnityEvent OnDisconnected = new();
        public UnityEvent<BridgeMessage> OnMessageReceived = new();

        private NamedPipeTransport              transport;
        private readonly ConcurrentQueue<Action> mainThread = new();

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
        }

        public void Initialize(bool asHost, string h2c = "micasa-h2c", string c2h = "micasa-c2h")
        {
            transport = new NamedPipeTransport(h2c, c2h);

            transport.Connected += () => mainThread.Enqueue(() =>
            {
                IsConnected = true;
                OnConnected.Invoke();
            });

            transport.Disconnected += () => mainThread.Enqueue(() =>
            {
                IsConnected = false;
                OnDisconnected.Invoke();
            });

            transport.LineReceived += line => mainThread.Enqueue(() =>
            {
                var msg = JsonUtility.FromJson<BridgeMessage>(line);
                OnMessageReceived.Invoke(msg);
            });

            if (asHost) transport.StartHost();
            else        transport.StartClient();
        }

        public void Send(BridgeMessage msg) => transport?.Send(JsonUtility.ToJson(msg));

        void Update()
        {
            while (mainThread.TryDequeue(out var action)) action();
        }

        void OnDestroy()
        {
            transport?.Dispose();
            IsConnected = false;
            if (Instance == this) Instance = null;
        }
    }
}
