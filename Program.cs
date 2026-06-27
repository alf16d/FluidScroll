using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace FluidScroll
{
    internal static class Program
    {
        private const string MutexName = "FluidScroll_SingleInstance_Mutex";

        [STAThread]
        private static void Main()
        {
            using var mutex = new Mutex(true, MutexName, out bool createdNew);
            if (!createdNew)
            {
                return;
            }

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using var timerResolution = new TimerResolution();
            using var context = new TrayApplicationContext();
            Application.Run(context);
        }

        private sealed class TimerResolution : IDisposable
        {
            private readonly bool _started;

            public TimerResolution()
            {
                _started = timeBeginPeriod(1) == 0;
            }

            public void Dispose()
            {
                if (_started)
                {
                    timeEndPeriod(1);
                }
            }

            [DllImport("winmm.dll")]
            private static extern uint timeBeginPeriod(uint uMilliseconds);

            [DllImport("winmm.dll")]
            private static extern uint timeEndPeriod(uint uMilliseconds);
        }
    }
}
