using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ClassicLaunchpad.Core
{
    public interface ISystemCommandExecutor
    {
        void Execute(SystemActionType action);
    }

    public class SystemCommandExecutor : ISystemCommandExecutor
    {
        public Action<string, string>? ProcessStartHook { get; set; }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LockWorkStation();

        [DllImport("powrprof.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetSuspendState(
            [MarshalAs(UnmanagedType.Bool)] bool hibernate,
            [MarshalAs(UnmanagedType.Bool)] bool forceCritical,
            [MarshalAs(UnmanagedType.Bool)] bool disableWakeEvent);

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

        public void Execute(SystemActionType action)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ExecuteWindows(action);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                ExecuteMac(action);
            }
            // For other platforms, do nothing
        }

        private void ExecuteWindows(SystemActionType action)
        {
            try
            {
                switch (action)
                {
                    case SystemActionType.Lock:
                        // Cannot easily hook direct P/Invoke calls, but they are guarded for Windows
                        if (ProcessStartHook != null)
                        {
                            ProcessStartHook("PInvoke", "LockWorkStation");
                            break;
                        }
                        LockWorkStation();
                        break;
                    case SystemActionType.Sleep:
                        if (ProcessStartHook != null)
                        {
                            ProcessStartHook("PInvoke", "SetSuspendState");
                            break;
                        }
                        SetSuspendState(false, false, false);
                        break;
                    case SystemActionType.Restart:
                        ExecuteProcess("shutdown.exe", "/r /t 0");
                        break;
                    case SystemActionType.Shutdown:
                        ExecuteProcess("shutdown.exe", "/s /t 0");
                        break;
                    case SystemActionType.EmptyTrash:
                        if (ProcessStartHook != null)
                        {
                            ProcessStartHook("PInvoke", "SHEmptyRecycleBin");
                            break;
                        }
                        SHEmptyRecycleBin(IntPtr.Zero, null, 7); // SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND
                        break;
                }
            }
            catch
            {
                // Prevent crash
            }
        }

        private void ExecuteMac(SystemActionType action)
        {
            try
            {
                switch (action)
                {
                    case SystemActionType.Lock:
                        ExecuteProcess("pmset", "displaysleepnow");
                        break;
                    case SystemActionType.Sleep:
                        ExecuteProcess("osascript", "-e \"tell application \\\"System Events\\\" to sleep\"");
                        break;
                    case SystemActionType.Restart:
                        ExecuteProcess("osascript", "-e \"tell application \\\"System Events\\\" to restart\"");
                        break;
                    case SystemActionType.Shutdown:
                        ExecuteProcess("osascript", "-e \"tell application \\\"System Events\\\" to shut down\"");
                        break;
                    case SystemActionType.EmptyTrash:
                        ExecuteProcess("osascript", "-e \"tell application \\\"Finder\\\" to empty trash\"");
                        break;
                }
            }
            catch
            {
                // Prevent crash
            }
        }

        private void ExecuteProcess(string fileName, string arguments)
        {
            try
            {
                if (ProcessStartHook != null)
                {
                    ProcessStartHook(fileName, arguments);
                    return;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(psi)?.Dispose();
            }
            catch
            {
                // Ignore failure
            }
        }
    }
}
