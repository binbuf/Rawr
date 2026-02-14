using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Rawr.Core.Interfaces;

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
