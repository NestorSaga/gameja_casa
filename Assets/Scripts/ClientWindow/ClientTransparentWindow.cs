using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Micasa
{
    public class ClientTransparentWindow : MonoBehaviour
    {
        [DllImport("user32.dll")] static extern uint GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] static extern uint SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref DwmMargins pMarInset);

        [StructLayout(LayoutKind.Sequential)] struct DwmMargins { public int cxLeftWidth, cxRightWidth, cyTopHeight, cyBottomHeight; }

        const int  GWL_STYLE        = -16;
        const int  GWL_EXSTYLE      = -20;
        const uint WS_CAPTION       = 0x00C00000;
        const uint WS_THICKFRAME    = 0x00040000;
        const uint WS_EX_TOOLWINDOW = 0x00000080;
        const uint SWP_NOMOVE       = 0x0002;
        const uint SWP_NOSIZE       = 0x0001;
        const uint SWP_FRAMECHANGED = 0x0020;

        static readonly IntPtr HWND_TOPMOST = new(-1);

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

        IEnumerator ApplyWhenReady()
        {
            IntPtr hwnd = IntPtr.Zero;
            while (hwnd == IntPtr.Zero)
            {
                hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                yield return null;
            }

            // Sin borde, topmost, sin entrada en barra de tareas
            var style = GetWindowLong(hwnd, GWL_STYLE);
            SetWindowLong(hwnd, GWL_STYLE, style & ~WS_CAPTION & ~WS_THICKFRAME);
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED);

            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);

            // DWM composita usando el canal alpha del framebuffer
            var m = new DwmMargins { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            DwmExtendFrameIntoClientArea(hwnd, ref m);
        }
    }
}
