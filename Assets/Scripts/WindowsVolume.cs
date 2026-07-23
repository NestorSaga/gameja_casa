using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Micasa
{
    public static class WindowsVolume
    {
        static readonly Guid CLSID_MMDeviceEnumerator = new("A95664D2-9614-4F35-A746-DE8DB63617E6");

        [DllImport("ole32.dll")]
        static extern int CoCreateInstance(
            ref Guid rclsid, IntPtr pUnkOuter, uint dwClsContext,
            ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);

        public static void Set(float volume01)
        {
            try
            {
                var clsid = CLSID_MMDeviceEnumerator;
                var iid   = typeof(IMMDeviceEnumerator).GUID;
                int hr = CoCreateInstance(ref clsid, IntPtr.Zero, 1 /*INPROC_SERVER*/, ref iid, out var unk);
                if (hr != 0)
                {
                    Debug.LogWarning($"[WindowsVolume] CoCreateInstance HRESULT 0x{hr:X8}");
                    return;
                }

                var enumerator = (IMMDeviceEnumerator)unk;
                enumerator.GetDefaultAudioEndpoint(0 /*eRender*/, 1 /*eMultimedia*/, out var device);
                var empty = Guid.Empty;

#if !UNITY_EDITOR
                // Master endpoint volume — only in builds (would silence all system audio in editor)
                var epIid = typeof(IAudioEndpointVolume).GUID;
                device.Activate(ref epIid, 1, IntPtr.Zero, out var epObj);
                ((IAudioEndpointVolume)epObj).SetMasterVolumeLevelScalar(Mathf.Clamp01(volume01), ref empty);
#endif

                // Per-app volume for this process (Windows Volume Mixer)
                var smIid = typeof(IAudioSessionManager2).GUID;
                device.Activate(ref smIid, 1, IntPtr.Zero, out var smObj);
                var sessionMgr = (IAudioSessionManager2)smObj;
                sessionMgr.GetSessionEnumerator(out var sessionEnum);
                sessionEnum.GetCount(out int count);

                int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
                for (int i = 0; i < count; i++)
                {
                    sessionEnum.GetSession(i, out var session);
                    var ctrl2 = session as IAudioSessionControl2;
                    if (ctrl2 == null) continue;
                    ctrl2.GetProcessId(out uint sessionPid);
                    if ((int)sessionPid != pid) continue;
                    var sv = session as ISimpleAudioVolume;
                    sv?.SetMasterVolume(Mathf.Clamp01(volume01), ref empty);
                    break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WindowsVolume] {e.Message}");
            }
        }
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int dwStateMask, out object ppDevices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        int Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr pNotify);
        int UnregisterControlChangeNotify(IntPtr pNotify);
        int GetChannelCount(out uint pnChannelCount);
        int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        int GetMasterVolumeLevel(out float pfLevelDB);
        int GetMasterVolumeLevelScalar(out float pfLevel);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionManager2
    {
        int GetAudioSessionControl(ref Guid AudioSessionGuid, uint StreamFlags,
            [MarshalAs(UnmanagedType.IUnknown)] out object SessionControl);
        int GetSimpleAudioVolume(ref Guid AudioSessionGuid, uint StreamFlags,
            [MarshalAs(UnmanagedType.IUnknown)] out object AudioVolume);
        int GetSessionEnumerator(out IAudioSessionEnumerator SessionList);
        int RegisterSessionNotification(IntPtr SessionNotification);
        int UnregisterSessionNotification(IntPtr SessionNotification);
        int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionID, IntPtr duckNotification);
        int UnregisterDuckNotification(IntPtr duckNotification);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionEnumerator
    {
        int GetCount(out int SessionCount);
        int GetSession(int SessionCount,
            [MarshalAs(UnmanagedType.IUnknown)] out object Session);
    }

    // All vtable slots in order: IAudioSessionControl (9) then IAudioSessionControl2 extras (5)
    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionControl2
    {
        int GetState(out int pRetVal);
        int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        int GetGroupingParam(out Guid pRetVal);
        int SetGroupingParam(ref Guid Override, ref Guid EventContext);
        int RegisterAudioSessionNotification(IntPtr NewNotifications);
        int UnregisterAudioSessionNotification(IntPtr NewNotifications);
        int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        int GetProcessId(out uint pRetVal);
        int IsSystemSoundsSession();
        int SetDuckingPreference(bool optOut);
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ISimpleAudioVolume
    {
        int SetMasterVolume(float fLevel, ref Guid EventContext);
        int GetMasterVolume(out float pfLevel);
        int SetMute(bool bMute, ref Guid EventContext);
        int GetMute(out bool pbMute);
    }
}
