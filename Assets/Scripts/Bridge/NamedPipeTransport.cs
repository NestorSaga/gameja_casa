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
        private readonly string h2cName;
        private readonly string c2hName;

        public NamedPipeTransport(string h2c = "micasa-h2c", string c2h = "micasa-c2h")
        {
            h2cName = h2c;
            c2hName = c2h;
        }

        public event Action         Connected;
        public event Action         Disconnected;
        public event Action<string> LineReceived;

        private readonly ConcurrentQueue<string> outbox = new();
        private PipeStream    sendPipe;
        private PipeStream    recvPipe;
        private volatile bool running;
        private Thread        loopThread;

        public void StartHost()
        {
#if UNITY_EDITOR
            return;
#endif
            running    = true;
            loopThread = new Thread(HostLoop) { IsBackground = true, Name = "Pipe-Host" };
            loopThread.Start();
        }

        public void StartClient()
        {
#if UNITY_EDITOR
            return;
#endif
            running    = true;
            loopThread = new Thread(ClientLoop) { IsBackground = true, Name = "Pipe-Client" };
            loopThread.Start();
        }

        public void Send(string line) => outbox.Enqueue(line);

        // ── Host ──────────────────────────────────────────────────────────

        private void HostLoop()
        {
            while (running)
            {
                NamedPipeServerStream send = null;
                NamedPipeServerStream recv = null;
                try
                {
                    send = new NamedPipeServerStream(h2cName, PipeDirection.Out, 1);
                    recv = new NamedPipeServerStream(c2hName, PipeDirection.In,  1);
                    sendPipe = send;
                    recvPipe = recv;

                    Debug.Log("[Pipe] Host waiting for client…");
                    send.WaitForConnection();
                    if (!running) break;
                    recv.WaitForConnection();
                    if (!running) break;

                    Debug.Log("[Pipe] Client connected");
                    Connected?.Invoke();

                    var readThread = new Thread(() => ReadLoop(recv)) { IsBackground = true, Name = "Pipe-Read" };
                    readThread.Start();
                    WriteLoop(send, readThread);
                    readThread.Join(500);
                }
                catch (ThreadInterruptedException) { break; }
                catch (Exception e)
                {
                    if (running) Debug.LogWarning($"[Pipe] Host error: {e.Message}");
                }
                finally
                {
                    SafeClose(send);
                    SafeClose(recv);
                }

                if (running)
                {
                    Disconnected?.Invoke();
                    Debug.Log("[Pipe] Host awaiting reconnect…");
                    try { Thread.Sleep(500); } catch (ThreadInterruptedException) { break; }
                }
            }
        }

        // ── Client ────────────────────────────────────────────────────────

        private void ClientLoop()
        {
            while (running)
            {
                NamedPipeClientStream recv = null;
                NamedPipeClientStream send = null;
                try
                {
                    recv = new NamedPipeClientStream(".", h2cName, PipeDirection.In);
                    send = new NamedPipeClientStream(".", c2hName, PipeDirection.Out);
                    recvPipe = recv;
                    sendPipe = send;

                    Debug.Log("[Pipe] Client connecting…");
                    recv.Connect(10_000);
                    if (!running) break;
                    send.Connect(10_000);
                    if (!running) break;

                    Debug.Log("[Pipe] Connected to host");
                    Connected?.Invoke();

                    var readThread = new Thread(() => ReadLoop(recv)) { IsBackground = true, Name = "Pipe-Read" };
                    readThread.Start();
                    WriteLoop(send, readThread);
                    readThread.Join(500);
                }
                catch (ThreadInterruptedException) { break; }
                catch (Exception e)
                {
                    if (running) Debug.LogWarning($"[Pipe] Client error: {e.Message}");
                }
                finally
                {
                    SafeClose(recv);
                    SafeClose(send);
                }

                if (running)
                {
                    Disconnected?.Invoke();
                    Debug.Log("[Pipe] Client awaiting reconnect…");
                    try { Thread.Sleep(1000); } catch (ThreadInterruptedException) { break; }
                }
            }
        }

        // ── I/O ───────────────────────────────────────────────────────────

        private void ReadLoop(PipeStream pipe)
        {
            try
            {
                var reader = new StreamReader(pipe);
                while (running)
                {
                    var line = reader.ReadLine();
                    if (line == null) break;
                    LineReceived?.Invoke(line);
                }
            }
            catch (Exception e)
            {
                if (running) Debug.LogWarning($"[Pipe] Read error: {e.Message}");
            }
        }

        private void WriteLoop(PipeStream pipe, Thread readThread)
        {
            try
            {
                var writer = new StreamWriter(pipe) { AutoFlush = true };
                while (running && pipe.IsConnected && readThread.IsAlive)
                {
                    if (outbox.TryDequeue(out var line))
                        writer.WriteLine(line);
                    else
                        Thread.Sleep(10);
                }
            }
            catch (Exception e)
            {
                if (running) Debug.LogWarning($"[Pipe] Write error: {e.Message}");
            }
        }

        // ── Cleanup ───────────────────────────────────────────────────────

        private static void SafeClose(PipeStream pipe)
        {
            try { pipe?.Close(); } catch { }
        }

        public void Dispose()
        {
            running = false;
            SafeClose(sendPipe);
            SafeClose(recvPipe);
            try { loopThread?.Interrupt(); } catch { }
            loopThread?.Join(500);
        }
    }
}
