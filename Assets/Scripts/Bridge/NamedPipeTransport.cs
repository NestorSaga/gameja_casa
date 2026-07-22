using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using UnityEngine;

namespace Micasa.Bridge
{
    // Two unidirectional pipes to avoid Windows InOut concurrency issues:
    //   micasa-h2c — host writes, client reads
    //   micasa-c2h — client writes, host reads
    public class NamedPipeTransport : IDisposable
    {
        private const string H2C = "micasa-h2c";
        private const string C2H = "micasa-c2h";

        public event Action         Connected;
        public event Action         Disconnected;
        public event Action<string> LineReceived;

        private readonly ConcurrentQueue<string> _outbox = new();
        private PipeStream   _sendPipe;
        private PipeStream   _recvPipe;
        private volatile bool _running;

        public void StartHost()
        {
            _running = true;
            new Thread(HostLoop) { IsBackground = true, Name = "Pipe-Host" }.Start();
        }

        public void StartClient()
        {
            _running = true;
            new Thread(ClientLoop) { IsBackground = true, Name = "Pipe-Client" }.Start();
        }

        public void Send(string line) => _outbox.Enqueue(line);

        // ── Host ──────────────────────────────────────────────────────────

        private void HostLoop()
        {
            while (_running)
            {
                NamedPipeServerStream send = null;
                NamedPipeServerStream recv = null;
                try
                {
                    send = new NamedPipeServerStream(H2C, PipeDirection.Out, 1);
                    recv = new NamedPipeServerStream(C2H, PipeDirection.In,  1);
                    _sendPipe = send;
                    _recvPipe = recv;

                    Debug.Log("[Pipe] Host waiting for client…");
                    send.WaitForConnection();
                    if (!_running) break;
                    recv.WaitForConnection();
                    if (!_running) break;

                    Debug.Log("[Pipe] Client connected");
                    Connected?.Invoke();

                    var readThread = new Thread(() => ReadLoop(recv)) { IsBackground = true, Name = "Pipe-Read" };
                    readThread.Start();
                    WriteLoop(send);
                    readThread.Join(500);
                }
                catch (Exception e)
                {
                    if (_running) Debug.LogWarning($"[Pipe] Host error: {e.Message}");
                }
                finally
                {
                    SafeClose(send);
                    SafeClose(recv);
                }

                if (_running)
                {
                    Disconnected?.Invoke();
                    Debug.Log("[Pipe] Host awaiting reconnect…");
                    Thread.Sleep(500);
                }
            }
        }

        // ── Client ────────────────────────────────────────────────────────

        private void ClientLoop()
        {
            while (_running)
            {
                NamedPipeClientStream recv = null;
                NamedPipeClientStream send = null;
                try
                {
                    recv = new NamedPipeClientStream(".", H2C, PipeDirection.In);
                    send = new NamedPipeClientStream(".", C2H, PipeDirection.Out);
                    _recvPipe = recv;
                    _sendPipe = send;

                    Debug.Log("[Pipe] Client connecting…");
                    recv.Connect(10_000);
                    if (!_running) break;
                    send.Connect(10_000);
                    if (!_running) break;

                    Debug.Log("[Pipe] Connected to host");
                    Connected?.Invoke();

                    var readThread = new Thread(() => ReadLoop(recv)) { IsBackground = true, Name = "Pipe-Read" };
                    readThread.Start();
                    WriteLoop(send);
                    readThread.Join(500);
                }
                catch (Exception e)
                {
                    if (_running) Debug.LogWarning($"[Pipe] Client error: {e.Message}");
                }
                finally
                {
                    SafeClose(recv);
                    SafeClose(send);
                }

                if (_running)
                {
                    Disconnected?.Invoke();
                    Debug.Log("[Pipe] Client awaiting reconnect…");
                    Thread.Sleep(1000);
                }
            }
        }

        // ── I/O ───────────────────────────────────────────────────────────

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
            catch (Exception e)
            {
                if (_running) Debug.LogWarning($"[Pipe] Read error: {e.Message}");
            }
        }

        private void WriteLoop(PipeStream pipe)
        {
            try
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
            catch (Exception e)
            {
                if (_running) Debug.LogWarning($"[Pipe] Write error: {e.Message}");
            }
        }

        // ── Cleanup ───────────────────────────────────────────────────────

        private static void SafeClose(PipeStream pipe)
        {
            try { pipe?.Close(); } catch { }
        }

        public void Dispose()
        {
            _running = false;
            SafeClose(_sendPipe);
            SafeClose(_recvPipe);
        }
    }
}
