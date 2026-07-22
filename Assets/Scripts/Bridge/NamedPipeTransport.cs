using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using UnityEngine;

namespace Micasa.Bridge
{
    // Uses two unidirectional pipes to avoid Windows InOut pipe concurrency issues:
    //   micasa-h2c — host writes, client reads
    //   micasa-c2h — client writes, host reads
    public class NamedPipeTransport : IDisposable
    {
        private const string H2C = "micasa-h2c";
        private const string C2H = "micasa-c2h";

        public event Action Connected;
        public event Action<string> LineReceived;

        private readonly ConcurrentQueue<string> _outbox = new();
        private PipeStream _sendPipe;
        private PipeStream _recvPipe;
        private volatile bool _running;

        public void StartHost()
        {
            _running = true;
            new Thread(HostLoop) { IsBackground = true }.Start();
        }

        public void StartClient()
        {
            _running = true;
            new Thread(ClientLoop) { IsBackground = true }.Start();
        }

        public void Send(string line) => _outbox.Enqueue(line);

        private void HostLoop()
        {
            try
            {
                var send = new NamedPipeServerStream(H2C, PipeDirection.Out, 1);
                var recv = new NamedPipeServerStream(C2H, PipeDirection.In, 1);
                _sendPipe = send;
                _recvPipe = recv;

                Debug.Log("[Pipe] Host waiting for client...");
                send.WaitForConnection();
                recv.WaitForConnection();
                Debug.Log("[Pipe] Client connected");
                Connected?.Invoke();

                new Thread(() => ReadLoop(recv)) { IsBackground = true }.Start();
                WriteLoop(send);
            }
            catch (Exception e) when (_running)
            {
                Debug.LogError($"[Pipe] Host error: {e.Message}");
            }
        }

        private void ClientLoop()
        {
            try
            {
                var recv = new NamedPipeClientStream(".", H2C, PipeDirection.In);
                var send = new NamedPipeClientStream(".", C2H, PipeDirection.Out);
                _recvPipe = recv;
                _sendPipe = send;

                Debug.Log("[Pipe] Client connecting...");
                recv.Connect(10_000);
                send.Connect(10_000);
                Debug.Log("[Pipe] Connected to host");
                Connected?.Invoke();

                new Thread(() => ReadLoop(recv)) { IsBackground = true }.Start();
                WriteLoop(send);
            }
            catch (Exception e) when (_running)
            {
                Debug.LogError($"[Pipe] Client error: {e.Message}");
            }
        }

        private void ReadLoop(PipeStream pipe)
        {
            try
            {
                var reader = new StreamReader(pipe);
                while (_running)
                {
                    var line = reader.ReadLine();
                    if (line == null) break;
                    LineReceived?.Invoke(line);
                }
            }
            catch (Exception e) when (_running)
            {
                Debug.LogWarning($"[Pipe] Read error: {e.Message}");
            }
        }

        private void WriteLoop(PipeStream pipe)
        {
            var writer = new StreamWriter(pipe) { AutoFlush = true };
            while (_running && pipe.IsConnected)
            {
                if (_outbox.TryDequeue(out var line))
                    writer.WriteLine(line);
                else
                    Thread.Sleep(10);
            }
        }

        public void Dispose()
        {
            _running = false;
            _sendPipe?.Close();
            _recvPipe?.Close();
        }
    }
}
