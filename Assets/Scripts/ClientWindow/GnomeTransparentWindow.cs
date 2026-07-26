using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Micasa
{
    public class GnomeTransparentWindow : MonoBehaviour
    {
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref DwmMargins pMarInset);
        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
        static extern bool SystemParametersInfoRect(uint uiAction, uint uiParam, ref WinRect pvParam, uint fWinIni);
        [DllImport("user32.dll")] static extern uint  GetWindowLong   (IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] static extern uint  SetWindowLong   (IntPtr hWnd, int nIndex, uint dwNewLong);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr newProc);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
        static extern IntPtr CallWindowProc(IntPtr prevProc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)] struct DwmMargins { public int cxLeftWidth, cxRightWidth, cyTopHeight, cyBottomHeight; }
        [StructLayout(LayoutKind.Sequential)] struct WinRect    { public int left, top, right, bottom; }

        delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        const int  GWL_STYLE        = -16;
        const int  GWL_EXSTYLE      = -20;
        const int  GWLP_WNDPROC     = -4;
        const uint WS_CAPTION       = 0x00C00000;
        const uint WS_THICKFRAME    = 0x00040000;
        const uint WS_EX_LAYERED    = 0x00080000;
        const uint WS_EX_TRANSPARENT= 0x00000020;
        const uint SWP_NOSIZE       = 0x0001;
        const uint SWP_FRAMECHANGED = 0x0020;
        const uint SPI_GETWORKAREA  = 0x0030;
        const uint WM_NCHITTEST     = 0x0084;
        const int  HTTRANSPARENT    = -1;
        const int  HTCLIENT         = 1;

        static readonly IntPtr HWND_BOTTOM  = new(1);
        static readonly IntPtr HWND_TOPMOST = new(-1);

        private IntPtr          hwnd;
        private IntPtr          prevWndProc;
        private WndProcDelegate wndProcDelegate;
        private GCHandle        wndProcHandle;

        // Actualizado cada frame en el hilo principal; leído en WndProc (mismo hilo en Unity/Windows).
        private bool mouseOverContent;

        void Start()
        {
            if (Camera.main != null)
            {
                Camera.main.clearFlags      = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }
#if !UNITY_EDITOR
            StartCoroutine(ApplyWhenReady());
#endif
        }

        void Update()
        {
#if !UNITY_EDITOR
            if (Camera.main == null) return;
            // Convierte la posición del ratón a coordenadas del mundo 2D y hace raycast.
            // El mesh necesita un Collider2D para ser detectable.
            Vector2 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseOverContent = Physics2D.Raycast(worldPos, Vector2.zero).collider != null;
#endif
        }

        void OnDestroy()
        {
#if !UNITY_EDITOR
            if (prevWndProc != IntPtr.Zero && hwnd != IntPtr.Zero)
                SetWindowLongPtr(hwnd, GWLP_WNDPROC, prevWndProc);
            if (wndProcHandle.IsAllocated) wndProcHandle.Free();
#endif
        }

        IntPtr CustomWndProc(IntPtr hwndParam, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_NCHITTEST)
                return new IntPtr(mouseOverContent ? HTCLIENT : HTTRANSPARENT);
            return CallWindowProc(prevWndProc, hwndParam, msg, wParam, lParam);
        }

        IEnumerator ApplyWhenReady()
        {
            while (hwnd == IntPtr.Zero)
            {
                hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                yield return null;
            }

            SetWindowPos(hwnd, HWND_BOTTOM, -Screen.width * 2, -Screen.height * 2, 0, 0, SWP_NOSIZE);
            yield return null;

            var m = new DwmMargins { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            DwmExtendFrameIntoClientArea(hwnd, ref m);

            uint style = GetWindowLong(hwnd, GWL_STYLE);
            SetWindowLong(hwnd, GWL_STYLE, style & ~(WS_CAPTION | WS_THICKFRAME));

            uint exStyle = (uint)(GetWindowLongPtr(hwnd, GWL_EXSTYLE)).ToInt64();
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);

            // Engancha WndProc para gestionar WM_NCHITTEST por píxel.
            wndProcDelegate = CustomWndProc;
            if (wndProcHandle.IsAllocated) wndProcHandle.Free();
            wndProcHandle = GCHandle.Alloc(wndProcDelegate);
            prevWndProc   = SetWindowLongPtr(hwnd, GWLP_WNDPROC,
                                Marshal.GetFunctionPointerForDelegate(wndProcDelegate));

            var workArea = new WinRect();
            SystemParametersInfoRect(SPI_GETWORKAREA, 0, ref workArea, 0);

            var args = System.Environment.GetCommandLineArgs();
            int x, y;
            if (System.Array.IndexOf(args, "--gnome2") >= 0)
            {
                x = workArea.left;
                y = workArea.bottom - Screen.height;
            }
            else if (System.Array.IndexOf(args, "--gnomeophone") >= 0)
            {
                x = workArea.left;
                y = workArea.top;
            }
            else
            {
                x = workArea.right  - Screen.width;
                y = workArea.bottom - Screen.height;
            }

            SetWindowPos(hwnd, HWND_TOPMOST, x, y, Screen.width, Screen.height, SWP_FRAMECHANGED);
        }
    }
}
