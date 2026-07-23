using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Micasa
{
    public class MainWindowSetup : MonoBehaviour
    {
        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        const uint SWP_FRAMECHANGED = 0x0020;

        void Awake()
        {
            var args = System.Environment.GetCommandLineArgs();
            foreach (var a in args)
            {
                if (a is "--gnome" or "--gnomeophone" or "--gnome2" or "--client" or "--camera")
                    return;
            }

            int winW = Display.main.systemWidth  / 2;
            int winH = Display.main.systemHeight / 2;
            Screen.SetResolution(winW, winH, FullScreenMode.Windowed);

            DontDestroyOnLoad(gameObject);
#if !UNITY_EDITOR
            StartCoroutine(CenterWindow(winW, winH));
#endif
        }

        IEnumerator CenterWindow(int winW, int winH)
        {
            IntPtr hwnd = IntPtr.Zero;
            while (hwnd == IntPtr.Zero)
            {
                hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                yield return null;
            }

            int x = (Display.main.systemWidth  - winW) / 2;
            int y = (Display.main.systemHeight - winH) / 2;
            SetWindowPos(hwnd, IntPtr.Zero, x, y, winW, winH, SWP_FRAMECHANGED);

            Destroy(gameObject);
        }
    }
}
