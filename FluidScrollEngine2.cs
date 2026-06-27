using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace FluidScroll
{
    /// <summary>
    /// FluidScrollEngine2: A more advanced smooth scrolling engine inspired by smooth.c patterns.
    /// Features:
    /// - High-precision worker thread (1ms resolution)
    /// - Segmented scroll segments (multiple concurrent scrolls)
    /// - Linear/Inertial decay per segment
    /// </summary>
    public class FluidScrollEngine2 : IDisposable
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_MOUSEHWHEEL = 0x020E;
        private static readonly UIntPtr INJECTED_EXTRA_INFO = new UIntPtr(0x12345678);

        private IntPtr _hookID = IntPtr.Zero;
        private readonly LowLevelMouseProc _proc;

        private class ScrollSegment
        {
            public double TotalDelta;
            public double ConsumedDelta;
            public double DurationMs;
            public uint StartTime;
            public bool IsHorizontal;
        }

        private readonly ConcurrentQueue<ScrollSegment> _segments = new ConcurrentQueue<ScrollSegment>();
        private Thread? _workerThread;
        private volatile bool _isRunning;
        private volatile bool _isEnabled = true;

        public bool IsEnabled
        {
            get => _isEnabled;
            set => _isEnabled = value;
        }

        // Configuration
        public double SegmentDurationMs { get; set; } = 120.0;
        public double ScrollBoost { get; set; } = 1.5;

        public FluidScrollEngine2()
        {
            _proc = HookCallback;
        }

        public void Start()
        {
            _isRunning = true;
            _workerThread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest,
                Name = "FluidScrollWorker"
            };
            _workerThread.Start();

            _hookID = SetHook(_proc);
        }

        public void Stop()
        {
            _isRunning = false;
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
            _workerThread?.Join(500);
        }

        private void WorkerLoop()
        {
            timeBeginPeriod(1);
            
            // Track sub-pixel accumulators
            double accX = 0;
            double accY = 0;

            while (_isRunning)
            {
                if (!_isEnabled)
                {
                    while (_segments.TryDequeue(out _)) { }
                    accX = accY = 0;
                    Thread.Sleep(10);
                    continue;
                }

                uint now = timeGetTime();
                int segmentCount = _segments.Count;
                double frameDeltaX = 0;
                double frameDeltaY = 0;

                if (segmentCount > 0)
                {
                    // Process all active segments
                    int processed = 0;
                    int initialCount = segmentCount;
                    
                    for (int i = 0; i < initialCount; i++)
                    {
                        if (!_segments.TryDequeue(out var seg)) continue;

                        double elapsed = (double)(now - seg.StartTime);
                        if (elapsed >= seg.DurationMs)
                        {
                            // Finish segment
                            double remaining = seg.TotalDelta - seg.ConsumedDelta;
                            if (seg.IsHorizontal) frameDeltaX += remaining;
                            else frameDeltaY += remaining;
                            continue;
                        }

                        // Linear interpolation for simplicity (smooth.c uses more complex ones but this is the base)
                        // Progress = elapsed / duration
                        double progress = elapsed / seg.DurationMs;
                        
                        // We use a simple ease-out: 1 - (1-t)^2
                        double easeOut = 1.0 - Math.Pow(1.0 - progress, 2);
                        double targetConsumed = seg.TotalDelta * easeOut;
                        double toConsume = targetConsumed - seg.ConsumedDelta;

                        if (seg.IsHorizontal) frameDeltaX += toConsume;
                        else frameDeltaY += toConsume;

                        seg.ConsumedDelta = targetConsumed;
                        _segments.Enqueue(seg); // Put back for next frame
                        processed++;
                    }
                }

                // Apply frame deltas to accumulators
                accX += frameDeltaX;
                accY += frameDeltaY;

                int moveX = (int)accX;
                int moveY = (int)accY;

                if (moveX != 0)
                {
                    accX -= moveX;
                    SendWheelEvent(moveX, true);
                }
                if (moveY != 0)
                {
                    accY -= moveY;
                    SendWheelEvent(moveY, false);
                }

                Thread.Sleep(1);
            }

            timeEndPeriod(1);
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
            if (nCode >= 0 && _isEnabled)
            {
                if (wParam == (IntPtr)WM_MOUSEWHEEL || wParam == (IntPtr)WM_MOUSEHWHEEL)
                {
                    MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                    if (hookStruct.dwExtraInfo != INJECTED_EXTRA_INFO)
                    {
                        int delta = (short)(hookStruct.mouseData >> 16);
                        
                        _segments.Enqueue(new ScrollSegment
                        {
                            TotalDelta = delta * ScrollBoost,
                            ConsumedDelta = 0,
                            DurationMs = SegmentDurationMs,
                            StartTime = timeGetTime(),
                            IsHorizontal = (wParam == (IntPtr)WM_MOUSEHWHEEL)
                        });

                        return (IntPtr)1; // Block original
                    }
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void SendWheelEvent(int delta, bool isHorizontal)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = 0; // INPUT_MOUSE
            inputs[0].u.mi.dx = 0;
            inputs[0].u.mi.dy = 0;
            inputs[0].u.mi.mouseData = delta;
            inputs[0].u.mi.dwFlags = isHorizontal ? 0x01000u : 0x0800u;
            inputs[0].u.mi.time = 0;
            inputs[0].u.mi.dwExtraInfo = INJECTED_EXTRA_INFO;

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public void Dispose()
        {
            Stop();
        }

        // --- P/Invoke ---

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT { public POINT pt; public int mouseData; public int flags; public int time; public UIntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }
        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT { public int type; public InputUnion u; }
        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; }
        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT { public int dx; public int dy; public int mouseData; public uint dwFlags; public int time; public UIntPtr dwExtraInfo; }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint uMilliseconds);
        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint uMilliseconds);
        [DllImport("winmm.dll")]
        private static extern uint timeGetTime();
    }
}
