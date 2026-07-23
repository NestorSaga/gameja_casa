using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Micasa
{
    public class DVDBounce : MonoBehaviour
    {
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out WinRect rect);
        [DllImport("user32.dll")] static extern uint GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] static extern uint SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

        [StructLayout(LayoutKind.Sequential)] struct WinRect { public int left, top, right, bottom; }

        const int  GWL_STYLE        = -16;
        const uint WS_CAPTION       = 0x00C00000;
        const uint SWP_NOSIZE       = 0x0001;
        const uint SWP_NOZORDER     = 0x0004;
        const uint SWP_FRAMECHANGED = 0x0020;

        const float Speed = 300f;

        private IntPtr hwnd;
        public  bool   IsBouncing { get; private set; }
        private float  x, y;
        private float  vx, vy;
        private int    winW, winH;
        private uint   savedStyle;

        void Start()
        {
            if (AppBootstrap.CameraViewIndex >= 0) { enabled = false; return; }
#if !UNITY_EDITOR
            StartCoroutine(WaitForHandle());
#endif
        }

        public void Toggle()
        {
            if (IsBouncing) StopBounce();
            else            StartBounce();
        }

        private void StartBounce()
        {
            if (hwnd == IntPtr.Zero) return;

            GetWindowRect(hwnd, out var wr);
            x    = wr.left;
            y    = wr.top;
            winW = wr.right  - wr.left;
            winH = wr.bottom - wr.top;

            vx = Speed;
            vy = Speed * 0.75f;

            savedStyle = GetWindowLong(hwnd, GWL_STYLE);
            SetWindowLong(hwnd, GWL_STYLE, savedStyle & ~WS_CAPTION);
            SetWindowPos(hwnd, IntPtr.Zero, wr.left, wr.top, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);

            IsBouncing = true;
        }

        private void StopBounce()
        {
            IsBouncing = false;
            if (hwnd == IntPtr.Zero) return;

            SetWindowLong(hwnd, GWL_STYLE, savedStyle);
            SetWindowPos(hwnd, IntPtr.Zero, (int)x, (int)y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
        }

        void Update()
        {
            if (!IsBouncing || hwnd == IntPtr.Zero) return;

            int   sw = Display.main.systemWidth;
            int   sh = Display.main.systemHeight;
            float dt = Time.deltaTime;

            x += vx * dt;
            y += vy * dt;

            if (x < 0)         { x = 0;         vx =  Mathf.Abs(vx); }
            if (x + winW > sw) { x = sw - winW; vx = -Mathf.Abs(vx); }
            if (y < 0)         { y = 0;          vy =  Mathf.Abs(vy); }
            if (y + winH > sh) { y = sh - winH; vy = -Mathf.Abs(vy); }

            // WM_MOVE fires synchronously inside SetWindowPos, so HostWindowCamera
            // receives the updated client position before beginCameraRendering this frame.
            SetWindowPos(hwnd, IntPtr.Zero, (int)x, (int)y, 0, 0, SWP_NOSIZE | SWP_NOZORDER);
        }

        IEnumerator WaitForHandle()
        {
            while (hwnd == IntPtr.Zero)
            {
                hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                yield return null;
            }
        }
    }
}
