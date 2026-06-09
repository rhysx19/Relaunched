using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using ClassicLaunchpad.Core;

namespace ClassicLaunchpad.Tests
{
    public class SettingsStoreTests
    {
        [Fact]
        public async Task TestMockSettingsStore_SavesAndLoadsSuccessfully()
        {
            // Arrange
            var tempFile = Path.Combine(Path.GetTempPath(), "SettingsStoreTests_" + System.Guid.NewGuid().ToString("N") + ".json");
            try
            {
                ISettingsStore store = new MockSettingsStore(tempFile);
                var config = new LayoutConfig
                {
                    Columns = 5,
                    Rows = 5,
                    IconSize = 72,
                    PageOrder = new List<string> { "app1" }
                };

                // Act
                await store.SaveLayoutAsync(config);
                var loaded = await store.LoadLayoutAsync();

                // Assert
                Assert.NotNull(loaded);
                Assert.Equal(5, loaded.Columns);
                Assert.Equal(72, loaded.IconSize);
                Assert.Contains("app1", loaded.PageOrder);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public async Task TestSettingsStore_SavesAndLoadsSuccessfully()
        {
            // Arrange
            var tempFile = Path.Combine(Path.GetTempPath(), "SettingsStoreTests_" + System.Guid.NewGuid().ToString("N") + ".json");
            try
            {
                ISettingsStore store = new SettingsStore(tempFile);
                var config = new LayoutConfig
                {
                    Columns = 5,
                    Rows = 5,
                    IconSize = 72,
                    PageOrder = new List<string> { "app1" }
                };

                // Act
                await store.SaveLayoutAsync(config);
                var loaded = await store.LoadLayoutAsync();

                // Assert
                Assert.NotNull(loaded);
                Assert.Equal(5, loaded.Columns);
                Assert.Equal(72, loaded.IconSize);
                Assert.Contains("app1", loaded.PageOrder);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public async Task TestSettingsStore_SaveLayout_ThrowsOnWriteFailure()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "SettingsStoreDir_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                // FilePath is a directory, writing to it will throw an exception
                ISettingsStore store = new SettingsStore(tempDir);
                var config = new LayoutConfig { Columns = 5 };

                // Act & Assert
                await Assert.ThrowsAnyAsync<System.Exception>(() => store.SaveLayoutAsync(config));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
}
