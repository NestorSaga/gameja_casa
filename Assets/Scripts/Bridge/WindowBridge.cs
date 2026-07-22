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

        private NamedPipeTransport _transport;
        private readonly ConcurrentQueue<Action> _mainThread = new();

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Initialize(bool asHost)
        {
            _transport = new NamedPipeTransport();

            _transport.Connected += () => _mainThread.Enqueue(() =>
            {
                IsConnected = true;
                OnConnected.Invoke();
            });

            _transport.Disconnected += () => _mainThread.Enqueue(() =>
            {
                IsConnected = false;
                OnDisconnected.Invoke();
            });

            _transport.LineReceived += line => _mainThread.Enqueue(() =>
            {
                var msg = JsonUtility.FromJson<BridgeMessage>(line);
                OnMessageReceived.Invoke(msg);
            });

            if (asHost) _transport.StartHost();
            else _transport.StartClient();
        }

        public void Send(BridgeMessage msg) => _transport?.Send(JsonUtility.ToJson(msg));

        void Update()
        {
            while (_mainThread.TryDequeue(out var action)) action();
        }

        void OnDestroy()
        {
            _transport?.Dispose();
            IsConnected = false;
            Instance = null;
        }
    }
}
