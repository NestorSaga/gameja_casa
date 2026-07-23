using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Micasa
{
    public class HostWindowCamera : MonoBehaviour
    {
        public const float PPU = 100f;

        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr hWnd, out WinRect rect);
        [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr hWnd, ref WinPoint pt);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out WinRect rect);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr newProc);
        [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
        static extern IntPtr CallWindowProc(IntPtr prevProc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern uint GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] static extern uint SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);
        [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref DwmMargins pMarInset);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowW(string cls, string title);
        [StructLayout(LayoutKind.Sequential)] struct DwmMargins { public int cxLeftWidth, cxRightWidth, cyTopHeight, cyBottomHeight; }

        [StructLayout(LayoutKind.Sequential)] struct WinRect  { public int left, top, right, bottom; }
        [StructLayout(LayoutKind.Sequential)] struct WinPoint { public int x, y; }

        delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        const int  GWLP_WNDPROC     = -4;
        const int  GWL_STYLE        = -16;
        const int  GWL_EXSTYLE      = -20;
        const uint WM_MOVE          = 0x0003;
        const uint WM_MOVING        = 0x0216;
        const uint WS_CAPTION       = 0x00C00000;
        const uint WS_THICKFRAME    = 0x00040000;
        const uint SWP_NOMOVE       = 0x0002;
        const uint SWP_NOSIZE       = 0x0001;
        const uint SWP_NOZORDER     = 0x0004;
        const uint SWP_SHOWWINDOW   = 0x0040;
        const uint SWP_FRAMECHANGED = 0x0020;

        private Camera          camera;
        private IntPtr          hwnd;
        private IntPtr          prevWndProc;
        private WndProcDelegate wndProcDelegate;
        private GCHandle        wndProcHandle;

        private int  clientX, clientY;
        private int  ncOffsetX, ncOffsetY;
        private bool positionValid;

        private bool isCameraMode;
        private bool explorerMode    = false;
        private int  cameraViewIndex = -1;
        private int  cameraScreenPos = -1;
        private int  originalWidth;
        private int  originalHeight;

        private float   targetOrthoSize;
        private Vector3 targetPosition;
        [SerializeField] private float smoothSpeed = 8f;

        private bool animating;
        private bool isTransparent;
        private uint savedStyle;

        public bool ExplorerMode  => explorerMode;
        public bool IsTransparent => isTransparent;

        void Awake()
        {
            camera = GetComponent<Camera>() ?? Camera.main;
            if (camera == null) camera = FindAnyObjectByType<Camera>();
            if (camera == null) return;

            camera.orthographic    = true;
            camera.clearFlags      = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

            QualitySettings.vSyncCount  = 0;
            Application.targetFrameRate = 120;

            originalWidth  = Screen.width;
            originalHeight = Screen.height;

            isCameraMode    = AppBootstrap.CameraViewIndex >= 0;
            cameraViewIndex = AppBootstrap.CameraViewIndex;
            cameraScreenPos = AppBootstrap.CameraScreenPos;

            if (isCameraMode) SetupStaticView(cameraViewIndex);
            else              ShowFullLevel();

            targetPosition  = camera.transform.position;
            targetOrthoSize = camera.orthographicSize;
        }

        void Start()
        {
#if !UNITY_EDITOR
            StartCoroutine(isCameraMode ? PositionCameraWindow() : WaitForHandle());
#endif
        }

        void Update()
        {
            if (camera == null || isCameraMode) return;
            if (!animating)
                camera.orthographicSize = Mathf.Lerp(camera.orthographicSize, targetOrthoSize, Time.deltaTime * smoothSpeed);
            if (!explorerMode)
                camera.transform.position = Vector3.Lerp(camera.transform.position, targetPosition, Time.deltaTime * smoothSpeed);
        }

        void OnEnable()  => RenderPipelineManager.beginCameraRendering += OnBeginRender;
        void OnDisable() => RenderPipelineManager.beginCameraRendering -= OnBeginRender;
        void OnDestroy() => UnhookWndProc();

        // ── API pública ───────────────────────────────────────────────────

        public void EnterCameraMode(int viewIndex, int screenPos)
        {
            isCameraMode    = true;
            cameraViewIndex = viewIndex;
            cameraScreenPos = screenPos;
            UnhookWndProc();
            SetupStaticView(viewIndex);
#if !UNITY_EDITOR
            StartCoroutine(ResizeAndMoveToQuadrant(screenPos));
#endif
        }

        public void ToggleTransparency()
        {
            if (hwnd == IntPtr.Zero) return;
            if (isTransparent) DisableTransparency();
            else               EnableTransparency();
        }

        private void EnableTransparency()
        {
            savedStyle = GetWindowLong(hwnd, GWL_STYLE);

            // Sin borde
            SetWindowLong(hwnd, GWL_STYLE, savedStyle & ~WS_CAPTION & ~WS_THICKFRAME);
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED);

            // DWM usa el canal alpha del framebuffer (preserveFramebufferAlpha=1 lo preserva)
            var m = new DwmMargins { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            DwmExtendFrameIntoClientArea(hwnd, ref m);

            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            isTransparent = true;
        }

        private void DisableTransparency()
        {
            SetWindowLong(hwnd, GWL_STYLE, savedStyle);
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED);

            var m = new DwmMargins { cxLeftWidth = 0, cxRightWidth = 0, cyTopHeight = 0, cyBottomHeight = 0 };
            DwmExtendFrameIntoClientArea(hwnd, ref m);

            camera.backgroundColor = Color.black;
            isTransparent = false;
        }

        public void PlaySquishAnimation()
        {
            if (hwnd == IntPtr.Zero || animating) return;
            StartCoroutine(SquishCoroutine());
        }

        IEnumerator SquishCoroutine()
        {
            animating = true;
            GetWindowRect(hwnd, out var wr);
            int origW = wr.right  - wr.left;
            int origH = wr.bottom - wr.top;

            // Congelar el aspect de cámara: el contenido se deforma con la ventana
            camera.aspect = (float)origW / origH;

            yield return AnimateWindowToSize(origW * 2, origH / 3, 0.35f); // achate
            yield return AnimateWindowToSize(origW * 3, origH / 3, 0.25f); // ensanche
            yield return AnimateWindowToSize(origW,     origH,     0.4f);  // original

            camera.ResetAspect(); // Devuelve el aspect natural de la ventana
            animating = false;
        }

        IEnumerator AnimateWindowToSize(int targetW, int targetH, float duration)
        {
            GetWindowRect(hwnd, out var wr);
            int fromW   = wr.right  - wr.left;
            int fromH   = wr.bottom - wr.top;
            int centerX = (wr.left  + wr.right)  / 2;
            int centerY = (wr.top   + wr.bottom) / 2;

            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(t + Time.deltaTime / duration, 1f);
                float s = Mathf.SmoothStep(0f, 1f, t);
                int w = (int)Mathf.Lerp(fromW, targetW, s);
                int h = (int)Mathf.Lerp(fromH, targetH, s);
                SetWindowPos(hwnd, IntPtr.Zero, centerX - w / 2, centerY - h / 2, w, h, SWP_NOZORDER);
                yield return null;
            }
        }

        public void ToggleExplorerMode()
        {
            explorerMode = !explorerMode;
            if (!explorerMode) UpdateOverviewCamera();
        }

        public void ExitCameraMode()
        {
            isCameraMode    = false;
            cameraViewIndex = -1;
            ShowFullLevel();
#if !UNITY_EDITOR
            StartCoroutine(RestoreHostWindow());
#endif
        }

        // ── Modo cámara estática ──────────────────────────────────────────

        private void SetupStaticView(int index)
        {
            int col = index % AppBootstrap.CameraCols;
            int row = index / AppBootstrap.CameraCols;
            int sw  = Display.main.systemWidth;
            int sh  = Display.main.systemHeight;

            camera.transform.position = new Vector3(
                sw * (col + 0.5f) / AppBootstrap.CameraCols / PPU,
                sh * (1f - (row + 0.5f) / AppBootstrap.CameraRows) / PPU,
                -10f);

            camera.orthographicSize = (float)sh / AppBootstrap.CameraRows * 0.5f / PPU;
        }

        IEnumerator PositionCameraWindow()
        {
            while (hwnd == IntPtr.Zero)
            {
                hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                yield return null;
            }
            yield return MoveToQuadrant(cameraScreenPos);
        }

        IEnumerator ResizeAndMoveToQuadrant(int screenPos)
        {
            if (hwnd == IntPtr.Zero) yield break;
            int sw = Display.main.systemWidth;
            int sh = Display.main.systemHeight;
            Screen.SetResolution(sw / AppBootstrap.CameraCols, sh / AppBootstrap.CameraRows, FullScreenMode.Windowed);
            yield return null;
            yield return MoveToQuadrant(screenPos);
        }

        IEnumerator MoveToQuadrant(int screenPos)
        {
            int col  = screenPos % AppBootstrap.CameraCols;
            int row  = screenPos / AppBootstrap.CameraCols;
            int sw   = Display.main.systemWidth;
            int sh   = Display.main.systemHeight;
            int winW = sw / AppBootstrap.CameraCols;
            int winH = sh / AppBootstrap.CameraRows;

            SetWindowPos(hwnd, IntPtr.Zero,
                col * winW, row * winH, winW, winH,
                SWP_NOZORDER | SWP_SHOWWINDOW);
            yield break;
        }

        // ── Modo host ─────────────────────────────────────────────────────

        private void OnBeginRender(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != camera || isCameraMode) return;

            if (!explorerMode) { UpdateOverviewCamera(); return; }

#if !UNITY_EDITOR
            if (hwnd == IntPtr.Zero) return;

            GetClientRect(hwnd, out var cr);
            var pt = new WinPoint();
            ClientToScreen(hwnd, ref pt);

            camera.transform.position = new Vector3(
                (pt.x + cr.right  * 0.5f) / PPU,
                (Display.main.systemHeight - pt.y - cr.bottom * 0.5f) / PPU,
                -10f);
            targetOrthoSize = cr.bottom * 0.5f / PPU;
#endif
        }

        private void UpdateOverviewCamera()
        {
            int   sw           = Display.main.systemWidth;
            int   sh           = Display.main.systemHeight;
            float halfLevelW   = sw * 0.5f / PPU;
            float halfLevelH   = sh * 0.5f / PPU;
            float windowAspect = (float)Screen.width / Screen.height;

            targetPosition  = new Vector3(halfLevelW, halfLevelH, -10f);
            targetOrthoSize = Mathf.Max(halfLevelH, halfLevelW / windowAspect);
        }

        private IntPtr CustomWndProc(IntPtr hwndParam, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_MOVING)
            {
                var wr = Marshal.PtrToStructure<WinRect>(lParam);
                clientX = wr.left + ncOffsetX;
                clientY = wr.top  + ncOffsetY;
                positionValid = true;
            }
            else if (msg == WM_MOVE)
            {
                long lp = lParam.ToInt64();
                clientX = (short)(lp & 0xFFFF);
                clientY = (short)((lp >> 16) & 0xFFFF);
                positionValid = true;
            }
            return CallWindowProc(prevWndProc, hwndParam, msg, wParam, lParam);
        }

        private void HookWndProc()
        {
            GetWindowRect(hwnd, out var wr);
            var pt = new WinPoint();
            ClientToScreen(hwnd, ref pt);
            ncOffsetX = pt.x - wr.left;
            ncOffsetY = pt.y - wr.top;

            wndProcDelegate = CustomWndProc;
            if (wndProcHandle.IsAllocated) wndProcHandle.Free();
            wndProcHandle = GCHandle.Alloc(wndProcDelegate);

            prevWndProc = SetWindowLongPtr(hwnd, GWLP_WNDPROC,
                Marshal.GetFunctionPointerForDelegate(wndProcDelegate));

            if (prevWndProc == IntPtr.Zero)
            {
                wndProcHandle.Free();
                wndProcDelegate = null;
            }
        }

        private void UnhookWndProc()
        {
            if (prevWndProc == IntPtr.Zero || hwnd == IntPtr.Zero) return;
            SetWindowLongPtr(hwnd, GWLP_WNDPROC, prevWndProc);
            prevWndProc = IntPtr.Zero;
            if (wndProcHandle.IsAllocated) wndProcHandle.Free();
        }

        private void ShowFullLevel()
        {
            camera.transform.position = new Vector3(
                Display.main.systemWidth  * 0.5f / PPU,
                Display.main.systemHeight * 0.5f / PPU,
                -10f);
            camera.orthographicSize = Display.main.systemHeight * 0.5f / PPU;
        }

        IEnumerator WaitForHandle()
        {
            while (hwnd == IntPtr.Zero)
            {
                hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                if (hwnd == IntPtr.Zero)
                    hwnd = FindWindowW(null, Application.productName);
                yield return null;
            }
            HookWndProc();
        }

        IEnumerator RestoreHostWindow()
        {
            if (hwnd == IntPtr.Zero) yield break;

            Screen.SetResolution(originalWidth, originalHeight, FullScreenMode.Windowed);
            yield return null;

            int sw = Display.main.systemWidth;
            int sh = Display.main.systemHeight;
            SetWindowPos(hwnd, IntPtr.Zero,
                (sw - originalWidth)  / 2,
                (sh - originalHeight) / 2,
                originalWidth, originalHeight,
                SWP_NOZORDER | SWP_SHOWWINDOW);

            positionValid = false;
            HookWndProc();
        }
    }
}
