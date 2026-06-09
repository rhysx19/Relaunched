using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using ClassicLaunchpad.Core;

namespace ClassicLaunchpad.Tests
{
    public class AppScannerTests
    {
        [Fact]
        public async Task TestMockAppScanner_ReturnsPresetApps()
        {
            // Arrange
            var preset = new List<AppItem>
            {
                new AppItem { Id = "app1", Name = "App One" }
            };
            var scanner = new MockAppScanner(preset);

            // Act
            var result = await scanner.ScanApplicationsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("app1", result[0].Id);
        }

        [Fact]
        public void TestAppItem_PropertiesSetAndGetCorrectly()
        {
            // Arrange & Act
            var app = new AppItem
            {
                Id = "id123",
                Name = "TestApp",
                TargetPath = "path/to/exe",
                IconPath = "path/to/icon",
                IsFolder = true,
                FolderItems = new List<AppItem>()
            };

            // Assert
            Assert.Equal("id123", app.Id);
            Assert.Equal("TestApp", app.Name);
            Assert.Equal("path/to/exe", app.TargetPath);
            Assert.Equal("path/to/icon", app.IconPath);
            Assert.True(app.IsFolder);
            Assert.Empty(app.FolderItems);
        }
    }

    public class RealAppScannerTests : IDisposable
    {
        private readonly List<string> _tempDirectories = new List<string>();

        private string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "RealAppScannerTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            _tempDirectories.Add(path);
            return path;
        }

        public void Dispose()
        {
            foreach (var dir in _tempDirectories)
            {
                try
                {
                    if (Directory.Exists(dir))
                    {
                        Directory.Delete(dir, true);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [Fact]
        public async Task TestAppScanner_SimulatedScan_FiltersHelperAndUninstallers()
        {
            // Arrange
            var tempDir = CreateTempDirectory();
            
            // Valid apps
            await File.WriteAllTextAsync(Path.Combine(tempDir, "Visual Studio Code.lnk"), "dummy");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "Google Chrome.lnk"), "dummy");
            
            // Helper/uninstaller apps
            await File.WriteAllTextAsync(Path.Combine(tempDir, "Uninstall Chrome.lnk"), "dummy");
            await File.WriteAllTextAsync(Path.Combine(tempDir, ".ignored.lnk"), "dummy");

            var scanner = new AppScanner(tempDir);

            // Act
            var result = await scanner.ScanApplicationsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            
            // Verify names are sorted alphabetically
            Assert.Equal("Google Chrome", result[0].Name);
            Assert.Equal("Visual Studio Code", result[1].Name);
            
            // Verify they are not filtered
            Assert.DoesNotContain(result, a => a.Name.Contains("Uninstall"));
            Assert.DoesNotContain(result, a => a.Name.StartsWith("."));
        }

        [Fact]
        public async Task TestAppScanner_Deduplication()
        {
            // Arrange
            var tempDir = CreateTempDirectory();
            var subDir = Path.Combine(tempDir, "Sub");
            Directory.CreateDirectory(subDir);

            // Same name, different paths
            await File.WriteAllTextAsync(Path.Combine(tempDir, "DuplicateApp.lnk"), "dummy");
            await File.WriteAllTextAsync(Path.Combine(subDir, "DuplicateApp.lnk"), "dummy");

            var scanner = new AppScanner(tempDir);

            // Act
            var result = await scanner.ScanApplicationsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("DuplicateApp", result[0].Name);
        }
    }
}
