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

        private IntPtr _hwnd;
        public  bool   IsBouncing { get; private set; }
        private float  _x, _y;
        private float  _vx, _vy;
        private int    _winW, _winH;
        private uint   _savedStyle;

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
            if (_hwnd == IntPtr.Zero) return;

            GetWindowRect(_hwnd, out var wr);
            _x    = wr.left;
            _y    = wr.top;
            _winW = wr.right  - wr.left;
            _winH = wr.bottom - wr.top;

            _vx = Speed;
            _vy = Speed * 0.75f;

            _savedStyle = GetWindowLong(_hwnd, GWL_STYLE);
            SetWindowLong(_hwnd, GWL_STYLE, _savedStyle & ~WS_CAPTION);
            SetWindowPos(_hwnd, IntPtr.Zero, wr.left, wr.top, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);

            IsBouncing = true;
        }

        private void StopBounce()
        {
            IsBouncing = false;
            if (_hwnd == IntPtr.Zero) return;

            SetWindowLong(_hwnd, GWL_STYLE, _savedStyle);
            SetWindowPos(_hwnd, IntPtr.Zero, (int)_x, (int)_y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
        }

        void Update()
        {
            if (!IsBouncing || _hwnd == IntPtr.Zero) return;

            int   sw = Display.main.systemWidth;
            int   sh = Display.main.systemHeight;
            float dt = Time.deltaTime;

            _x += _vx * dt;
            _y += _vy * dt;

            if (_x < 0)          { _x = 0;          _vx =  Mathf.Abs(_vx); }
            if (_x + _winW > sw) { _x = sw - _winW; _vx = -Mathf.Abs(_vx); }
            if (_y < 0)          { _y = 0;           _vy =  Mathf.Abs(_vy); }
            if (_y + _winH > sh) { _y = sh - _winH; _vy = -Mathf.Abs(_vy); }

            // WM_MOVE fires synchronously inside SetWindowPos, so HostWindowCamera
            // receives the updated client position before beginCameraRendering this frame.
            SetWindowPos(_hwnd, IntPtr.Zero, (int)_x, (int)_y, 0, 0, SWP_NOSIZE | SWP_NOZORDER);
        }

        IEnumerator WaitForHandle()
        {
            while (_hwnd == IntPtr.Zero)
            {
                _hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                yield return null;
            }
        }
    }
}
