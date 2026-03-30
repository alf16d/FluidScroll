using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FluidScroll
{
    static class Program
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_MOUSEHWHEEL = 0x020E;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;
        private const uint MOUSEEVENTF_HWHEEL = 0x01000;
        
        private static IntPtr _hookID = IntPtr.Zero;
        private static LowLevelMouseProc _proc = HookCallback;
        
        // Magic number to identify our generated events
        private static readonly UIntPtr INJECTED_EXTRA_INFO = new UIntPtr(0x12345678);

        // Accumulated scroll deltas
        private static double _pendingScrollY = 0;
        private static double _pendingScrollX = 0;
        
        // Time tracking for velocity/inertia calculation
        private static DateTime _lastScrollTimeY = DateTime.MinValue;
        private static DateTime _lastScrollTimeX = DateTime.MinValue;
        
        private static readonly object _scrollLock = new object();
        
        private static bool _isEnabled = true;
        private static NotifyIcon _trayIcon = null!; // Initialized in Main

        private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "FluidScroll";

        // High resolution timer
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        public static extern uint TimeBeginPeriod(uint uMilliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        public static extern uint TimeEndPeriod(uint uMilliseconds);

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Request 1ms timer resolution for smoother Thread.Sleep
            TimeBeginPeriod(1);

            _hookID = SetHook(_proc);

            // Start smooth scrolling loop in a background task
            Task.Run(SmoothScrollLoop);

            // Setup Tray Icon
            using Stream iconStream = typeof(Program).Assembly.GetManifestResourceStream("FluidScroll.imgs.icon_256.ico")!;
            _trayIcon = new NotifyIcon()
            {
                Icon = new Icon(iconStream),
                Text = "FluidScroll",
                ContextMenuStrip = new ContextMenuStrip()
            };

            // Auto-enable startup on first run
            if (!IsStartupEnabled())
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
                string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (key != null && exePath != null)
                {
                    key.SetValue(AppName, $"\"{exePath}\"");
                }
            }

            var toggleMenuItem = new ToolStripMenuItem("Disable FluidScroll", null, ToggleEnabled);
            _trayIcon.ContextMenuStrip.Items.Add(toggleMenuItem);

            var startupMenuItem = new ToolStripMenuItem("Start with Windows", null, ToggleStartup);
            startupMenuItem.Checked = IsStartupEnabled();
            _trayIcon.ContextMenuStrip.Items.Add(startupMenuItem);

            _trayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _trayIcon.ContextMenuStrip.Items.Add("Exit", null, Exit);
            
            _trayIcon.Visible = true;

            // Application.Run provides the message loop needed for SetWindowsHookEx
            Application.Run();

            UnhookWindowsHookEx(_hookID);
            TimeEndPeriod(1);
            _trayIcon.Dispose();
        }

        private static bool IsStartupEnabled()
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(StartupRegistryKey, false);
            return key?.GetValue(AppName) != null;
        }

        private static void ToggleStartup(object? sender, EventArgs e)
        {
            var item = (ToolStripMenuItem)sender!;
            bool isCurrentlyEnabled = IsStartupEnabled();

            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
            if (key == null) return;

            if (isCurrentlyEnabled)
            {
                key.DeleteValue(AppName, false);
                item.Checked = false;
            }
            else
            {
                string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (exePath != null)
                {
                    key.SetValue(AppName, $"\"{exePath}\"");
                    item.Checked = true;
                }
            }
        }
        
        private static void ToggleEnabled(object sender, EventArgs e)
        {
            _isEnabled = !_isEnabled;
            var item = (ToolStripMenuItem)sender;
            
            if (_isEnabled)
            {
                item.Text = "Disable FluidScroll";
                _trayIcon.Text = "FluidScroll (Enabled)";
            }
            else
            {
                item.Text = "Enable FluidScroll";
                _trayIcon.Text = "FluidScroll (Disabled)";
                
                // Clear any pending momentum when disabling
                lock (_scrollLock)
                {
                    _pendingScrollY = 0;
                    _pendingScrollX = 0;
                }
            }
        }

        private static void Exit(object sender, EventArgs e)
        {
            _trayIcon.Visible = false;
            Application.Exit();
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (!_isEnabled)
            {
                return CallNextHookEx(_hookID, nCode, wParam, lParam);
            }

            if (nCode >= 0 && (wParam == (IntPtr)WM_MOUSEWHEEL || wParam == (IntPtr)WM_MOUSEHWHEEL))
            {
                MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                if (hookStruct.dwExtraInfo == INJECTED_EXTRA_INFO)
                {
                    return CallNextHookEx(_hookID, nCode, wParam, lParam);
                }

                int delta = (short)(hookStruct.mouseData >> 16);
                DateTime now = DateTime.UtcNow;

                lock (_scrollLock)
                {
                    if (wParam == (IntPtr)WM_MOUSEWHEEL)
                    {
                        double timeDeltaMs = (now - _lastScrollTimeY).TotalMilliseconds;
                        double multiplier = 1.0;

                        // Aggressive inertia for fast scrolls (like SmoothScroll app)
                        if (timeDeltaMs > 0 && timeDeltaMs < 80)
                        {
                            // Exponential multiplier the faster you scroll
                            multiplier = 1.0 + Math.Pow((80.0 / timeDeltaMs), 1.5) * 0.4;
                        }

                        // Cap the maximum multiplier so a single flick doesn't scroll to the bottom of the internet
                        multiplier = Math.Min(multiplier, 15.0);

                        _pendingScrollY += delta * multiplier;
                        _lastScrollTimeY = now;
                    }
                    else
                    {
                        double timeDeltaMs = (now - _lastScrollTimeX).TotalMilliseconds;
                        double multiplier = 1.0;

                        if (timeDeltaMs > 0 && timeDeltaMs < 80)
                        {
                            multiplier = 1.0 + Math.Pow((80.0 / timeDeltaMs), 1.5) * 0.4;
                        }
                        
                        multiplier = Math.Min(multiplier, 15.0);

                        _pendingScrollX += delta * multiplier;
                        _lastScrollTimeX = now;
                    }
                }

                return (IntPtr)1; 
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private static void SmoothScrollLoop()
        {
            const int intervalMs = 7; // ~144 fps update rate
            const double friction = 0.94; // Even higher friction for very long, fluid gliding (macOS / SmoothScroll style)
            const double animationSpeed = 0.12; // Slower immediate catch-up, feels more "buttery" and fluid
            const double minScrollDelta = 0.5;

            while (true)
            {
                if (!_isEnabled)
                {
                    Thread.Sleep(intervalMs);
                    continue;
                }

                double currentScrollY = 0;
                double currentScrollX = 0;

                lock (_scrollLock)
                {
                    if (Math.Abs(_pendingScrollY) > minScrollDelta)
                    {
                        currentScrollY = _pendingScrollY * animationSpeed;
                        _pendingScrollY -= currentScrollY;
                        _pendingScrollY *= friction; 
                    }
                    else
                    {
                        currentScrollY = _pendingScrollY;
                        _pendingScrollY = 0;
                    }

                    if (Math.Abs(_pendingScrollX) > minScrollDelta)
                    {
                        currentScrollX = _pendingScrollX * animationSpeed;
                        _pendingScrollX -= currentScrollX;
                        _pendingScrollX *= friction; 
                    }
                    else
                    {
                        currentScrollX = _pendingScrollX;
                        _pendingScrollX = 0;
                    }
                }

                int scrollToSendY = (int)Math.Round(currentScrollY);
                int scrollToSendX = (int)Math.Round(currentScrollX);

                if (scrollToSendY != 0)
                {
                    SendWheelEvent(scrollToSendY, false);
                    lock (_scrollLock)
                    {
                        _pendingScrollY += (currentScrollY - scrollToSendY);
                    }
                }

                if (scrollToSendX != 0)
                {
                    SendWheelEvent(scrollToSendX, true);
                    lock (_scrollLock)
                    {
                        _pendingScrollX += (currentScrollX - scrollToSendX);
                    }
                }

                Thread.Sleep(intervalMs);
            }
        }

        private static void SendWheelEvent(int delta, bool isHorizontal)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi.dx = 0;
            inputs[0].u.mi.dy = 0;
            inputs[0].u.mi.mouseData = delta;
            inputs[0].u.mi.dwFlags = isHorizontal ? MOUSEEVENTF_HWHEEL : MOUSEEVENTF_WHEEL;
            inputs[0].u.mi.time = 0;
            inputs[0].u.mi.dwExtraInfo = INJECTED_EXTRA_INFO;

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        // --- P/Invoke Definitions ---

        private const int INPUT_MOUSE = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public int mouseData;
            public int flags;
            public int time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public int mouseData;
            public uint dwFlags;
            public int time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public int time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    }
}