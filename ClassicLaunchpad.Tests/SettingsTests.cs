using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace ClassicLaunchpad.Tests
{
    public class SettingsTests : IDisposable
    {
        private readonly List<string> _tempFiles = new List<string>();

        private string GetTempFilePath()
        {
            var path = Path.Combine(Path.GetTempPath(), "SettingsTests_" + Guid.NewGuid().ToString("N") + ".json");
            _tempFiles.Add(path);
            return path;
        }

        public void Dispose()
        {
            foreach (var file in _tempFiles)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [Fact]
        public async Task LoadLayoutAsync_WhenFileDoesNotExist_ReturnsDefaultLayoutConfig()
        {
            // Arrange
            var filePath = GetTempFilePath();
            var store = new ClassicLaunchpad.Core.MockSettingsStore(filePath);

            // Act
            var config = await store.LoadLayoutAsync();

            // Assert
            Assert.NotNull(config);
            Assert.Equal(7, config.Columns);
            Assert.Equal(5, config.Rows);
            Assert.Equal(80, config.IconSize);
            Assert.Empty(config.PageOrder);
            Assert.Empty(config.Folders);
        }

        [Fact]
        public async Task SaveLayoutAsync_CreatesFileWithJsonContent()
        {
            // Arrange
            var filePath = GetTempFilePath();
            var store = new ClassicLaunchpad.Core.MockSettingsStore(filePath);
            var config = new ClassicLaunchpad.Core.LayoutConfig
            {
                Columns = 8,
                Rows = 6,
                IconSize = 96
            };

            // Act
            await store.SaveLayoutAsync(config);

            // Assert
            Assert.True(File.Exists(filePath));
            var json = await File.ReadAllTextAsync(filePath);
            Assert.Contains("\"Columns\": 8", json);
            Assert.Contains("\"Rows\": 6", json);
            Assert.Contains("\"IconSize\": 96", json);
        }

        [Fact]
        public async Task LoadLayoutAsync_WhenFileExists_LoadsLayoutConfigCorrectly()
        {
            // Arrange
            var filePath = GetTempFilePath();
            var store = new ClassicLaunchpad.Core.MockSettingsStore(filePath);
            var expected = new ClassicLaunchpad.Core.LayoutConfig
            {
                Columns = 4,
                Rows = 4,
                IconSize = 64
            };
            await store.SaveLayoutAsync(expected);

            // Act
            var actual = await store.LoadLayoutAsync();

            // Assert
            Assert.NotNull(actual);
            Assert.Equal(expected.Columns, actual.Columns);
            Assert.Equal(expected.Rows, actual.Rows);
            Assert.Equal(expected.IconSize, actual.IconSize);
        }

        [Fact]
        public async Task LoadLayoutAsync_WhenFileContainsInvalidJson_ReturnsDefaultLayoutConfig()
        {
            // Arrange
            var filePath = GetTempFilePath();
            await File.WriteAllTextAsync(filePath, "{ invalid json }");
            var store = new ClassicLaunchpad.Core.MockSettingsStore(filePath);

            // Act
            var config = await store.LoadLayoutAsync();

            // Assert
            Assert.NotNull(config);
            Assert.Equal(7, config.Columns); // default value
        }

        [Fact]
        public async Task SaveAndLoad_WithComplexLayout_RoundtripsSuccessfully()
        {
            // Arrange
            var filePath = GetTempFilePath();
            var store = new ClassicLaunchpad.Core.MockSettingsStore(filePath);
            var config = new ClassicLaunchpad.Core.LayoutConfig
            {
                Columns = 5,
                Rows = 5,
                IconSize = 72,
                PageOrder = new List<string> { "app_a", "app_b", "folder_x" },
                Folders = new Dictionary<string, List<string>>
                {
                    { "folder_x", new List<string> { "app_c", "app_d" } }
                }
            };

            // Act
            await store.SaveLayoutAsync(config);
            var loaded = await store.LoadLayoutAsync();

            // Assert
            Assert.NotNull(loaded);
            Assert.Equal(5, loaded.Columns);
            Assert.Equal(72, loaded.IconSize);
            Assert.Equal(3, loaded.PageOrder.Count);
            Assert.Equal("folder_x", loaded.PageOrder[2]);
            Assert.True(loaded.Folders.ContainsKey("folder_x"));
            Assert.Equal(2, loaded.Folders["folder_x"].Count);
            Assert.Contains("app_c", loaded.Folders["folder_x"]);
            Assert.Contains("app_d", loaded.Folders["folder_x"]);
        }

        [Fact]
        public void SettingsStore_DefaultConstructor_ResolvesPathToLocalAppData()
        {
            // Act
            var store = new ClassicLaunchpad.Core.SettingsStore();

            // Assert
            var expectedPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClassicLaunchpad", "layout.json");
            Assert.Equal(expectedPath, store.FilePath);
        }

        [Fact]
        public async Task SettingsStore_SaveLayoutAsync_ToNonExistentSubdirectory_CreatesDirectoryAndSaves()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "SettingsStoreTestDir_" + Guid.NewGuid().ToString("N"));
            var filePath = Path.Combine(tempDir, "subdir", "layout.json");
            _tempFiles.Add(filePath);
            var store = new ClassicLaunchpad.Core.SettingsStore(filePath);
            var config = new ClassicLaunchpad.Core.LayoutConfig { Columns = 6 };

            try
            {
                // Act
                await store.SaveLayoutAsync(config);

                // Assert
                Assert.True(File.Exists(filePath));
                Assert.True(Directory.Exists(Path.GetDirectoryName(filePath)));
            }
            finally
            {
                // Clean up directory
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch {}
            }
        }

        [Fact]
        public async Task SettingsStore_LoadLayoutAsync_WhenFileDoesNotExist_ReturnsDefaultLayoutConfig()
        {
            // Arrange
            var filePath = GetTempFilePath();
            var store = new ClassicLaunchpad.Core.SettingsStore(filePath);

            // Act
            var config = await store.LoadLayoutAsync();

            // Assert
            Assert.NotNull(config);
            Assert.Equal(7, config.Columns);
            Assert.Equal(5, config.Rows);
            Assert.Equal(80, config.IconSize);
            Assert.Empty(config.PageOrder);
            Assert.Empty(config.Folders);
        }

        [Fact]
        public async Task SettingsStore_SaveLayoutAsync_CreatesFileWithJsonContent()
        {
            // Arrange
            var filePath = GetTempFilePath();
            var store = new ClassicLaunchpad.Core.SettingsStore(filePath);
            var config = new ClassicLaunchpad.Core.LayoutConfig
            {
                Columns = 8,
                Rows = 6,
                IconSize = 96
            };

            // Act
            await store.SaveLayoutAsync(config);

            // Assert
            Assert.True(File.Exists(filePath));
            var json = await File.ReadAllTextAsync(filePath);
            Assert.Contains("\"Columns\": 8", json);
            Assert.Contains("\"Rows\": 6", json);
            Assert.Contains("\"IconSize\": 96", json);
        }

        [Fact]
        public async Task SettingsStore_LoadLayoutAsync_WhenFileExists_LoadsLayoutConfigCorrectly()
        {
            // Arrange
            var filePath = GetTempFilePath();
            var store = new ClassicLaunchpad.Core.SettingsStore(filePath);
            var expected = new ClassicLaunchpad.Core.LayoutConfig
            {
                Columns = 4,
                Rows = 4,
                IconSize = 64
            };
            await store.SaveLayoutAsync(expected);

            // Act
            var actual = await store.LoadLayoutAsync();

            // Assert
            Assert.NotNull(actual);
            Assert.Equal(expected.Columns, actual.Columns);
            Assert.Equal(expected.Rows, actual.Rows);
            Assert.Equal(expected.IconSize, actual.IconSize);
        }

        [Fact]
        public async Task SettingsStore_LoadLayoutAsync_WhenFileContainsInvalidJson_ReturnsDefaultLayoutConfig()
        {
            // Arrange
            var filePath = GetTempFilePath();
            await File.WriteAllTextAsync(filePath, "{ invalid json }");
            var store = new ClassicLaunchpad.Core.SettingsStore(filePath);

            // Act
            var config = await store.LoadLayoutAsync();

            // Assert
            Assert.NotNull(config);
            Assert.Equal(7, config.Columns); // default value
        }

        [Fact]
        public async Task SettingsStore_LoadLayoutAsync_WhenFileContainsCorruptedJson_ReturnsDefaultLayoutConfig()
        {
            // Arrange
            var filePath = GetTempFilePath();
            // Corrupt file that could trigger various JsonException parsing errors
            await File.WriteAllTextAsync(filePath, "{ \"Columns\": 8, \"Rows\": [invalid array value] }");
            var store = new ClassicLaunchpad.Core.SettingsStore(filePath);

            // Act
            var config = await store.LoadLayoutAsync();

            // Assert
            Assert.NotNull(config);
            Assert.Equal(7, config.Columns); // returns default config
        }

        [Fact]
        public async Task SettingsStore_SaveAndLoad_WithComplexLayout_RoundtripsSuccessfully()
        {
            // Arrange
            var filePath = GetTempFilePath();
            var store = new ClassicLaunchpad.Core.SettingsStore(filePath);
            var config = new ClassicLaunchpad.Core.LayoutConfig
            {
                Columns = 5,
                Rows = 5,
                IconSize = 72,
                PageOrder = new List<string> { "app_a", "app_b", "folder_x" },
                Folders = new Dictionary<string, List<string>>
                {
                    { "folder_x", new List<string> { "app_c", "app_d" } }
                }
            };

            // Act
            await store.SaveLayoutAsync(config);
            var loaded = await store.LoadLayoutAsync();

            // Assert
            Assert.NotNull(loaded);
            Assert.Equal(5, loaded.Columns);
            Assert.Equal(72, loaded.IconSize);
            Assert.Equal(3, loaded.PageOrder.Count);
            Assert.Equal("folder_x", loaded.PageOrder[2]);
            Assert.True(loaded.Folders.ContainsKey("folder_x"));
            Assert.Equal(2, loaded.Folders["folder_x"].Count);
            Assert.Contains("app_c", loaded.Folders["folder_x"]);
            Assert.Contains("app_d", loaded.Folders["folder_x"]);
        }

        // Consolidated from the former SettingsStoreTests.cs
        [Fact]
        public async Task SettingsStore_SaveLayout_ThrowsOnWriteFailure()
        {
            // Arrange: the store's file path is an existing directory, so the
            // final write/rename must fail and the error must propagate.
            var tempDir = Path.Combine(Path.GetTempPath(), "SettingsStoreDir_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var store = new ClassicLaunchpad.Core.SettingsStore(tempDir);
                var config = new ClassicLaunchpad.Core.LayoutConfig { Columns = 5 };

                // Act & Assert
                await Assert.ThrowsAnyAsync<Exception>(() => store.SaveLayoutAsync(config));

                // The atomic-write temp file must not be left behind on failure.
                Assert.False(File.Exists(tempDir + ".tmp"));
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
