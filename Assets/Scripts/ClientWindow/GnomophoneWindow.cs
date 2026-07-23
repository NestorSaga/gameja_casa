using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Micasa
{
    public class GnomophoneWindow : MonoBehaviour
    {
        [DllImport("user32.dll")] static extern uint GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] static extern uint SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref DwmMargins pMarInset);
        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
        static extern bool SystemParametersInfoRect(uint uiAction, uint uiParam, ref WinRect pvParam, uint fWinIni);

        [StructLayout(LayoutKind.Sequential)] struct DwmMargins { public int cxLeftWidth, cxRightWidth, cyTopHeight, cyBottomHeight; }
        [StructLayout(LayoutKind.Sequential)] struct WinRect    { public int left, top, right, bottom; }

        const int  GWL_STYLE        = -16;
        const int  GWL_EXSTYLE      = -20;
        const uint WS_CAPTION       = 0x00C00000;
        const uint WS_THICKFRAME    = 0x00040000;
        const uint WS_EX_TOOLWINDOW = 0x00000080;
        const uint SWP_NOSIZE       = 0x0001;
        const uint SWP_FRAMECHANGED = 0x0020;
        const uint SPI_GETWORKAREA  = 0x0030;

        static readonly IntPtr HWND_BOTTOM  = new(1);
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

            SetWindowPos(hwnd, HWND_BOTTOM, -Screen.width * 2, -Screen.height * 2, 0, 0, SWP_NOSIZE);
            yield return null;

            var style = GetWindowLong(hwnd, GWL_STYLE);
            SetWindowLong(hwnd, GWL_STYLE, style & ~WS_CAPTION & ~WS_THICKFRAME);
            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);

            var m = new DwmMargins { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            DwmExtendFrameIntoClientArea(hwnd, ref m);

            var workArea = new WinRect();
            SystemParametersInfoRect(SPI_GETWORKAREA, 0, ref workArea, 0);
            int x = workArea.left;
            int y = workArea.bottom - Screen.height;
            SetWindowPos(hwnd, HWND_TOPMOST, x, y, Screen.width, Screen.height, SWP_FRAMECHANGED);
        }
    }
}
