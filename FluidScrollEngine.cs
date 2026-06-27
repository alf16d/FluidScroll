using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FluidScroll
{
    public class FluidScrollEngine : IDisposable
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_MOUSEHWHEEL = 0x020E;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;
        private const uint MOUSEEVENTF_HWHEEL = 0x01000;

        private IntPtr _hookID = IntPtr.Zero;
        private readonly LowLevelMouseProc _proc;
        
        // Magic number to identify our generated events
        private static readonly UIntPtr INJECTED_EXTRA_INFO = new UIntPtr(0x12345678);

        // Accumulated scroll deltas
        private double _pendingScrollY = 0;
        private double _pendingScrollX = 0;
        private double _accumulatorY = 0;
        private double _accumulatorX = 0;
        
        // Time tracking for velocity/inertia calculation
        private DateTime _lastScrollTimeY = DateTime.MinValue;
        private DateTime _lastScrollTimeX = DateTime.MinValue;
        
        private readonly object _scrollLock = new object();
        
        private volatile bool _isEnabled = true;
        public bool IsEnabled 
        { 
            get => _isEnabled;
            set 
            {
                _isEnabled = value;
                if (!_isEnabled)
                {
                    lock (_scrollLock)
                    {
                        _pendingScrollY = _pendingScrollX = 0;
                        _accumulatorY = _accumulatorX = 0;
                    }
                }
            }
        }

        // Tunable scroll settings
        public double AccelDeltaMs { get; set; } = 70.0;
        public double AccelMaxX { get; set; } = 7.0;
        public double ScrollBoost { get; set; } = 2.0;
        public double CatchupPerMs { get; set; } = 0.005;
        public double FrictionPerMs { get; set; } = 0.998;

        private CancellationTokenSource? _loopCts;
        private Task? _loopTask;

        public FluidScrollEngine()
        {
            _proc = HookCallback;
        }

        public void Start()
        {
            if (_hookID != IntPtr.Zero)
            {
                return;
            }

            _hookID = SetHook(_proc);
            if (_hookID == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to install the low-level mouse hook.");
            }

            _loopCts = new CancellationTokenSource();
            _loopTask = Task.Run(() => SmoothScrollLoop(_loopCts.Token));
        }

        public void Stop()
        {
            _loopCts?.Cancel();
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }

            try
            {
                _loopTask?.Wait(500);
            }
            catch (AggregateException ex)
            {
                ex.Handle(e => e is TaskCanceledException or OperationCanceledException);
            }

            _loopCts?.Dispose();
            _loopCts = null;
            _loopTask = null;
        }

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule!)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName!), 0);
            }
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
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
                lock (_scrollLock)
                {
                    if (wParam == (IntPtr)WM_MOUSEWHEEL)
                    {
                        UpdatePendingScroll(ref _pendingScrollY, ref _lastScrollTimeY, delta);
                    }
                    else
                    {
                        UpdatePendingScroll(ref _pendingScrollX, ref _lastScrollTimeX, delta);
                    }
                }

                return (IntPtr)1; 
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void UpdatePendingScroll(ref double pending, ref DateTime lastTime, int delta)
        {
            DateTime now = DateTime.UtcNow;
            double timeDeltaMs = (now - lastTime).TotalMilliseconds;
            double normalizedDelta = Math.Abs(delta) / 120.0;
            double multiplier = 1.0;

            if (timeDeltaMs > 0 && timeDeltaMs < AccelDeltaMs)
            {
                double t = timeDeltaMs / AccelDeltaMs;
                double boost = AccelMaxX - (AccelMaxX - 1.0) * t;
                multiplier = 1.0 + (boost - 1.0) * normalizedDelta;
            }

            pending += delta * multiplier * ScrollBoost;
            lastTime = now;
        }

        private void SmoothScrollLoop(CancellationToken token)
        {
            Stopwatch sw = Stopwatch.StartNew();
            double lastTime = sw.Elapsed.TotalMilliseconds;

            while (!token.IsCancellationRequested)
            {
                double currentTime = sw.Elapsed.TotalMilliseconds;
                double dt = currentTime - lastTime;
                lastTime = currentTime;

                if (!_isEnabled)
                {
                    Thread.Sleep(10);
                    continue;
                }

                if (dt > 0)
                {
                    double toSendY = 0;
                    double toSendX = 0;

                    lock (_scrollLock)
                    {
                        if (Math.Abs(_pendingScrollY) > 0.001 || Math.Abs(_accumulatorY) > 0.001)
                        {
                            double consumption = 1.0 - Math.Pow(1.0 - CatchupPerMs, dt);
                            double movement = _pendingScrollY * consumption;

                            _pendingScrollY -= movement;
                            _pendingScrollY *= Math.Pow(FrictionPerMs, dt);

                            _accumulatorY += movement;
                        }
                        else
                        {
                            _pendingScrollY = 0;
                        }

                        if (Math.Abs(_accumulatorY) >= 1.0)
                        {
                            toSendY = Math.Truncate(_accumulatorY);
                            _accumulatorY -= toSendY;
                        }

                        if (Math.Abs(_pendingScrollX) > 0.001 || Math.Abs(_accumulatorX) > 0.001)
                        {
                            double consumption = 1.0 - Math.Pow(1.0 - CatchupPerMs, dt);
                            double movement = _pendingScrollX * consumption;

                            _pendingScrollX -= movement;
                            _pendingScrollX *= Math.Pow(FrictionPerMs, dt);

                            _accumulatorX += movement;
                        }
                        else
                        {
                            _pendingScrollX = 0;
                        }

                        if (Math.Abs(_accumulatorX) >= 1.0)
                        {
                            toSendX = Math.Truncate(_accumulatorX);
                            _accumulatorX -= toSendX;
                        }
                    }

                    if (toSendY != 0) SendWheelEvent((int)toSendY, false);
                    if (toSendX != 0) SendWheelEvent((int)toSendX, true);
                }

                Thread.Sleep(1);
            }
        }

        private void SendWheelEvent(int delta, bool isHorizontal)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi.dx = 0;
            inputs[0].u.mi.dy = 0;
            inputs[0].u.mi.mouseData = delta;
            inputs[0].u.mi.dwFlags = isHorizontal ? MOUSEEVENTF_HWHEEL : MOUSEEVENTF_WHEEL;
            inputs[0].u.mi.time = 0;
            inputs[0].u.mi.dwExtraInfo = INJECTED_EXTRA_INFO;

            uint sent = SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
            if (sent != 1)
            {
                Debug.WriteLine($"SendInput failed: {Marshal.GetLastWin32Error()}");
            }
        }

        public void Dispose()
        {
            Stop();
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
