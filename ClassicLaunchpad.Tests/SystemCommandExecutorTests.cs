using System;
using System.Runtime.InteropServices;
using Xunit;
using ClassicLaunchpad.Core;

namespace ClassicLaunchpad.Tests
{
    public class SystemCommandExecutorTests
    {
        [Fact]
        public void TestSystemCommandExecutor_ImplementsInterface()
        {
            var executor = new SystemCommandExecutor();
            Assert.True(executor is ISystemCommandExecutor);
        }

        [Fact]
        public void TestSystemCommandExecutor_ExecuteNone_DoesNotThrow()
        {
            var executor = new SystemCommandExecutor();
            var exception = Record.Exception(() => executor.Execute(SystemActionType.None));
            Assert.Null(exception);
        }

        [Fact]
        public void TestSystemCommandExecutor_MacMappings()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return;
            }

            var executor = new SystemCommandExecutor();
            string? runFile = null;
            string? runArgs = null;
            executor.ProcessStartHook = (file, args) =>
            {
                runFile = file;
                runArgs = args;
            };

            executor.Execute(SystemActionType.Lock);
            Assert.Equal("pmset", runFile);
            Assert.Equal("displaysleepnow", runArgs);

            executor.Execute(SystemActionType.Sleep);
            Assert.Equal("osascript", runFile);
            Assert.Contains("sleep", runArgs);

            executor.Execute(SystemActionType.Restart);
            Assert.Equal("osascript", runFile);
            Assert.Contains("restart", runArgs);

            executor.Execute(SystemActionType.Shutdown);
            Assert.Equal("osascript", runFile);
            Assert.Contains("shut down", runArgs);

            executor.Execute(SystemActionType.EmptyTrash);
            Assert.Equal("osascript", runFile);
            Assert.Contains("empty trash", runArgs);
        }

        [Fact]
        public void TestSystemCommandExecutor_WindowsMappings()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var executor = new SystemCommandExecutor();
            string? runFile = null;
            string? runArgs = null;
            executor.ProcessStartHook = (file, args) =>
            {
                runFile = file;
                runArgs = args;
            };

            executor.Execute(SystemActionType.Lock);
            Assert.Equal("PInvoke", runFile);
            Assert.Equal("LockWorkStation", runArgs);

            executor.Execute(SystemActionType.Sleep);
            Assert.Equal("PInvoke", runFile);
            Assert.Equal("SetSuspendState", runArgs);

            executor.Execute(SystemActionType.Restart);
            Assert.Equal("shutdown.exe", runFile);
            Assert.Equal("/r /t 0", runArgs);

            executor.Execute(SystemActionType.Shutdown);
            Assert.Equal("shutdown.exe", runFile);
            Assert.Equal("/s /t 0", runArgs);

            executor.Execute(SystemActionType.EmptyTrash);
            Assert.Equal("PInvoke", runFile);
            Assert.Equal("SHEmptyRecycleBin", runArgs);
        }
    }
}
