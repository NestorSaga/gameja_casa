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

        const int  GWLP_WNDPROC    = -4;
        const int  GWL_STYLE       = -16;
        const int  GWL_EXSTYLE     = -20;
        const uint WM_ACTIVATE     = 0x0006;
        const uint WM_ACTIVATEAPP  = 0x001C;
        const uint WM_MOVE         = 0x0003;
        const uint WM_MOVING       = 0x0216;
        const uint WS_CAPTION      = 0x00C00000;
        const uint WS_THICKFRAME   = 0x00040000;
        const uint SWP_NOMOVE      = 0x0002;
        const uint SWP_NOSIZE      = 0x0001;
        const uint SWP_NOZORDER    = 0x0004;
        const uint SWP_SHOWWINDOW  = 0x0040;
        const uint SWP_FRAMECHANGED = 0x0020;

        private Camera          _cam;
        private IntPtr          _hwnd;
        private IntPtr          _prevWndProc;
        private WndProcDelegate _wndProcDelegate;

        private int  _clientX, _clientY;
        private int  _ncOffsetX, _ncOffsetY;
        private bool _positionValid;

        private bool _isCameraMode;
        private bool _explorerMode    = false;
        private int  _cameraViewIndex = -1;
        private int  _cameraScreenPos = -1;
        private int  _originalWidth;
        private int  _originalHeight;

        private float   _targetOrthoSize;
        private Vector3 _targetPosition;
        [SerializeField] private float _smoothSpeed = 8f;

        public bool ExplorerMode => _explorerMode;

        void Awake()
        {
            _cam = GetComponent<Camera>() ?? Camera.main;
            if (_cam == null) _cam = FindAnyObjectByType<Camera>();
            if (_cam == null) return;

            _cam.orthographic    = true;
            _cam.clearFlags      = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Color.black;

            QualitySettings.vSyncCount  = 0;
            Application.targetFrameRate = 120;

            _originalWidth  = Screen.width;
            _originalHeight = Screen.height;

            _isCameraMode    = AppBootstrap.CameraViewIndex >= 0;
            _cameraViewIndex = AppBootstrap.CameraViewIndex;
            _cameraScreenPos = AppBootstrap.CameraScreenPos;

            if (_isCameraMode) SetupStaticView(_cameraViewIndex);
            else               ShowFullLevel();

            _targetPosition  = _cam.transform.position;
            _targetOrthoSize = _cam.orthographicSize;
        }

        void Start()
        {
#if !UNITY_EDITOR
            StartCoroutine(_isCameraMode ? PositionCameraWindow() : WaitForHandle());
#endif
        }

        void Update()
        {
            if (_cam == null || _isCameraMode) return;
            if (!_animating)
                _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _targetOrthoSize, Time.deltaTime * _smoothSpeed);
            if (!_explorerMode)
                _cam.transform.position = Vector3.Lerp(_cam.transform.position, _targetPosition, Time.deltaTime * _smoothSpeed);
        }

        void OnEnable()  => RenderPipelineManager.beginCameraRendering += OnBeginRender;
        void OnDisable() => RenderPipelineManager.beginCameraRendering -= OnBeginRender;
        void OnDestroy() => UnhookWndProc();

        // ── API pública ───────────────────────────────────────────────────

        public void EnterCameraMode(int viewIndex, int screenPos)
        {
            _isCameraMode    = true;
            _cameraViewIndex = viewIndex;
            _cameraScreenPos = screenPos;
            UnhookWndProc();
            SetupStaticView(viewIndex);
#if !UNITY_EDITOR
            StartCoroutine(ResizeAndMoveToQuadrant(screenPos));
#endif
        }

        private bool _animating;
        private bool _isTransparent;
        private uint _savedStyle;

        public bool IsTransparent => _isTransparent;

        public void ToggleTransparency()
        {
            if (_hwnd == IntPtr.Zero) return;
            if (_isTransparent) DisableTransparency();
            else                EnableTransparency();
        }

        private void EnableTransparency()
        {
            _savedStyle = GetWindowLong(_hwnd, GWL_STYLE);

            // Sin borde
            SetWindowLong(_hwnd, GWL_STYLE, _savedStyle & ~WS_CAPTION & ~WS_THICKFRAME);
            SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED);

            // DWM usa el canal alpha del framebuffer (preserveFramebufferAlpha=1 lo preserva)
            var m = new DwmMargins { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            DwmExtendFrameIntoClientArea(_hwnd, ref m);

            _cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _isTransparent = true;
        }

        private void DisableTransparency()
        {
            SetWindowLong(_hwnd, GWL_STYLE, _savedStyle);
            SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED);

            var m = new DwmMargins { cxLeftWidth = 0, cxRightWidth = 0, cyTopHeight = 0, cyBottomHeight = 0 };
            DwmExtendFrameIntoClientArea(_hwnd, ref m);

            _cam.backgroundColor = Color.black;
            _isTransparent = false;
        }

        public void PlaySquishAnimation()
        {
            if (_hwnd == IntPtr.Zero || _animating) return;
            StartCoroutine(SquishCoroutine());
        }

        IEnumerator SquishCoroutine()
        {
            _animating = true;
            GetWindowRect(_hwnd, out var wr);
            int origW = wr.right  - wr.left;
            int origH = wr.bottom - wr.top;

            // Congelar el aspect de cámara: el contenido se deforma con la ventana
            _cam.aspect = (float)origW / origH;

            yield return AnimateWindowToSize(origW * 2, origH / 3, 0.35f); // achate
            yield return AnimateWindowToSize(origW * 3, origH / 3, 0.25f); // ensanche
            yield return AnimateWindowToSize(origW,     origH,     0.4f);  // original

            _cam.ResetAspect(); // Devuelve el aspect natural de la ventana
            _animating = false;
        }

        IEnumerator AnimateWindowToSize(int targetW, int targetH, float duration)
        {
            GetWindowRect(_hwnd, out var wr);
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
                SetWindowPos(_hwnd, IntPtr.Zero, centerX - w / 2, centerY - h / 2, w, h, SWP_NOZORDER);
                yield return null;
            }
        }

        public void ToggleExplorerMode()
        {
            _explorerMode = !_explorerMode;
            if (!_explorerMode) UpdateOverviewCamera();
        }

        public void ExitCameraMode()
        {
            _isCameraMode = false;
            _cameraViewIndex = -1;
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

            _cam.transform.position = new Vector3(
                sw * (col + 0.5f) / AppBootstrap.CameraCols / PPU,
                sh * (1f - (row + 0.5f) / AppBootstrap.CameraRows) / PPU,
                -10f);

            _cam.orthographicSize = (float)sh / AppBootstrap.CameraRows * 0.5f / PPU;
        }

        IEnumerator PositionCameraWindow()
        {
            while (_hwnd == IntPtr.Zero)
            {
                _hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                yield return null;
            }
            yield return MoveToQuadrant(_cameraScreenPos);
        }

        IEnumerator ResizeAndMoveToQuadrant(int screenPos)
        {
            if (_hwnd == IntPtr.Zero) yield break;
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

            SetWindowPos(_hwnd, IntPtr.Zero,
                col * winW, row * winH, winW, winH,
                SWP_NOZORDER | SWP_SHOWWINDOW);
            yield break;
        }

        // ── Modo host ─────────────────────────────────────────────────────

        private void OnBeginRender(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != _cam || _isCameraMode) return;

            if (!_explorerMode) { UpdateOverviewCamera(); return; }

#if !UNITY_EDITOR
            if (_hwnd == IntPtr.Zero) return;

            GetClientRect(_hwnd, out var cr);
            var pt = new WinPoint();
            ClientToScreen(_hwnd, ref pt);

            _cam.transform.position = new Vector3(
                (pt.x + cr.right  * 0.5f) / PPU,
                (Display.main.systemHeight - pt.y - cr.bottom * 0.5f) / PPU,
                -10f);
            _targetOrthoSize = cr.bottom * 0.5f / PPU;
#endif
        }

        private void UpdateOverviewCamera()
        {
            int   sw           = Display.main.systemWidth;
            int   sh           = Display.main.systemHeight;
            float halfLevelW   = sw * 0.5f / PPU;
            float halfLevelH   = sh * 0.5f / PPU;
            float windowAspect = (float)Screen.width / Screen.height;

            _targetPosition  = new Vector3(halfLevelW, halfLevelH, -10f);
            _targetOrthoSize = Mathf.Max(halfLevelH, halfLevelW / windowAspect);
        }

        private IntPtr CustomWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            // Always report as active so Unity never enters its focus-loss pause path
            if (msg == WM_ACTIVATEAPP)
                return CallWindowProc(_prevWndProc, hwnd, msg, new IntPtr(1), lParam);
            if (msg == WM_ACTIVATE && wParam == IntPtr.Zero)
                return CallWindowProc(_prevWndProc, hwnd, msg, new IntPtr(1), lParam);

            if (msg == WM_MOVING)
            {
                var wr = Marshal.PtrToStructure<WinRect>(lParam);
                _clientX = wr.left + _ncOffsetX;
                _clientY = wr.top  + _ncOffsetY;
                _positionValid = true;
            }
            else if (msg == WM_MOVE)
            {
                long lp = lParam.ToInt64();
                _clientX = (short)(lp & 0xFFFF);
                _clientY = (short)((lp >> 16) & 0xFFFF);
                _positionValid = true;
            }
            return CallWindowProc(_prevWndProc, hwnd, msg, wParam, lParam);
        }

        private void HookWndProc()
        {
            GetWindowRect(_hwnd, out var wr);
            var pt = new WinPoint();
            ClientToScreen(_hwnd, ref pt);
            _ncOffsetX = pt.x - wr.left;
            _ncOffsetY = pt.y - wr.top;

            _wndProcDelegate = CustomWndProc;
            _prevWndProc = SetWindowLongPtr(_hwnd, GWLP_WNDPROC,
                Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));

            if (_prevWndProc == IntPtr.Zero)
                _wndProcDelegate = null;
        }

        private void UnhookWndProc()
        {
            if (_prevWndProc == IntPtr.Zero || _hwnd == IntPtr.Zero) return;
            SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _prevWndProc);
            _prevWndProc = IntPtr.Zero;
        }

        private void ShowFullLevel()
        {
            _cam.transform.position = new Vector3(
                Display.main.systemWidth  * 0.5f / PPU,
                Display.main.systemHeight * 0.5f / PPU,
                -10f);
            _cam.orthographicSize = Display.main.systemHeight * 0.5f / PPU;
        }

        IEnumerator WaitForHandle()
        {
            while (_hwnd == IntPtr.Zero)
            {
                _hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                if (_hwnd == IntPtr.Zero)
                    _hwnd = FindWindowW(null, Application.productName);
                yield return null;
            }
            HookWndProc();
        }

        IEnumerator RestoreHostWindow()
        {
            if (_hwnd == IntPtr.Zero) yield break;

            Screen.SetResolution(_originalWidth, _originalHeight, FullScreenMode.Windowed);
            yield return null;

            int sw = Display.main.systemWidth;
            int sh = Display.main.systemHeight;
            SetWindowPos(_hwnd, IntPtr.Zero,
                (sw - _originalWidth)  / 2,
                (sh - _originalHeight) / 2,
                _originalWidth, _originalHeight,
                SWP_NOZORDER | SWP_SHOWWINDOW);

            _positionValid = false;
            HookWndProc();
        }
    }
}
