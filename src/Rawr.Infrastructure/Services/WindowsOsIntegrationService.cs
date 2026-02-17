using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Rawr.Core.Interfaces;
using Rawr.Core.Models;

namespace Rawr.Infrastructure.Services;

[SupportedOSPlatform("windows")]
public class WindowsOsIntegrationService : IOsIntegrationService
{
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "Rawr";

    public void SetStartWithOs(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\"");
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch (Exception)
        {
            // Log error or ignore if we don't have permissions
        }
    }

    public bool IsFullscreen()
    {
        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero) return false;

        GetWindowRect(foregroundWindow, out var rect);
        
        // Use monitor info for better accuracy
        var monitor = MonitorFromWindow(foregroundWindow, MONITOR_DEFAULTTOPRIMARY);
        var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        
        if (GetMonitorInfo(monitor, ref monitorInfo))
        {
            // Check if window rect matches or exceeds monitor rect
            return rect.Left <= monitorInfo.rcMonitor.Left &&
                   rect.Top <= monitorInfo.rcMonitor.Top &&
                   rect.Right >= monitorInfo.rcMonitor.Right &&
                   rect.Bottom >= monitorInfo.rcMonitor.Bottom;
        }

        return false;
    }

    public FocusAssistState GetFocusAssistState()
    {
        try
        {
            // Windows 10+ Focus Assist / Quiet Hours via WNF (Windows Notification Facility)
            // We use the undocumented NtQueryWnfStateData API to read the quiet hours state
            int result = SHQueryUserNotificationState(out var state);
            if (result == 0) // S_OK
            {
                return state switch
                {
                    QUERY_USER_NOTIFICATION_STATE.QUNS_NOT_PRESENT => FocusAssistState.Off,
                    QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY => FocusAssistState.PriorityOnly,
                    QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN => FocusAssistState.AlarmsOnly,
                    QUERY_USER_NOTIFICATION_STATE.QUNS_PRESENTATION_MODE => FocusAssistState.AlarmsOnly,
                    QUERY_USER_NOTIFICATION_STATE.QUNS_ACCEPTS_NOTIFICATIONS => FocusAssistState.Off,
                    QUERY_USER_NOTIFICATION_STATE.QUNS_QUIET_TIME => FocusAssistState.PriorityOnly,
                    QUERY_USER_NOTIFICATION_STATE.QUNS_APP => FocusAssistState.PriorityOnly,
                    _ => FocusAssistState.Off,
                };
            }
        }
        catch
        {
            // Ignore errors, assume Focus Assist is off
        }

        return FocusAssistState.Off;
    }

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out QUERY_USER_NOTIFICATION_STATE pquns);

    private enum QUERY_USER_NOTIFICATION_STATE
    {
        QUNS_NOT_PRESENT = 1,
        QUNS_BUSY = 2,
        QUNS_RUNNING_D3D_FULL_SCREEN = 3,
        QUNS_PRESENTATION_MODE = 4,
        QUNS_ACCEPTS_NOTIFICATIONS = 5,
        QUNS_QUIET_TIME = 6,
        QUNS_APP = 7
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private const uint MONITOR_DEFAULTTOPRIMARY = 0x00000001;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }
}
