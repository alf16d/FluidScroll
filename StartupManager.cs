using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace FluidScroll
{
    internal static class StartupManager
    {
        private const string AppName = "FluidScroll";
        private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool IsEnabled
        {
            get
            {
                try
                {
                    using RegistryKey? key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, false);
                    return key?.GetValue(AppName) is string;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to read startup registry key: {ex.Message}");
                    return false;
                }
            }
        }

        public static void SetEnabled(bool enabled)
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
                if (key == null)
                {
                    return;
                }

                if (enabled)
                {
                    string? exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(exePath))
                    {
                        key.SetValue(AppName, $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to update startup registry key: {ex.Message}");
            }
        }
    }
}
