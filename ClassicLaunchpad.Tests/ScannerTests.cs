using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace ClassicLaunchpad.Tests
{
    public class ScannerTests : IDisposable
    {
        private readonly List<string> _tempDirectories = new List<string>();

        private string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "ScannerTests_" + Guid.NewGuid().ToString("N"));
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
        public async Task ScanApplicationsAsync_WithPresetApps_ReturnsPresetApps()
        {
            // Arrange
            var preset = new List<ClassicLaunchpad.Core.AppItem>
            {
                new ClassicLaunchpad.Core.AppItem { Id = "app1", Name = "App One" },
                new ClassicLaunchpad.Core.AppItem { Id = "app2", Name = "App Two" }
            };
            var scanner = new ClassicLaunchpad.Core.MockAppScanner(preset);

            // Act
            var result = await scanner.ScanApplicationsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal("app1", result[0].Id);
            Assert.Equal("App Two", result[1].Name);
        }

        [Fact]
        public async Task ScanApplicationsAsync_WithNonExistentDirectory_ReturnsEmptyList()
        {
            // Arrange
            var nonExistentPath = Path.Combine(Path.GetTempPath(), "NonExistent_" + Guid.NewGuid().ToString("N"));
            var scanner = new ClassicLaunchpad.Core.MockAppScanner(nonExistentPath);

            // Act
            var result = await scanner.ScanApplicationsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task ScanApplicationsAsync_WithEmptyDirectory_ReturnsEmptyList()
        {
            // Arrange
            var tempDir = CreateTempDirectory();
            var scanner = new ClassicLaunchpad.Core.MockAppScanner(tempDir);

            // Act
            var result = await scanner.ScanApplicationsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task ScanApplicationsAsync_WithLnkFiles_ReturnsCorrectAppItems()
        {
            // Arrange
            var tempDir = CreateTempDirectory();
            var lnkFile1 = Path.Combine(tempDir, "Visual Studio Code.lnk");
            var lnkFile2 = Path.Combine(tempDir, "Google Chrome.lnk");

            await File.WriteAllTextAsync(lnkFile1, "dummy shortcut content");
            await File.WriteAllTextAsync(lnkFile2, "dummy shortcut content");

            var scanner = new ClassicLaunchpad.Core.MockAppScanner(tempDir);

            // Act
            var result = await scanner.ScanApplicationsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);

            var vsCode = result.Find(a => a.Name == "Visual Studio Code");
            Assert.NotNull(vsCode);
            Assert.Equal("visual_studio_code", vsCode.Id);
            Assert.Equal(lnkFile1, vsCode.TargetPath);
            Assert.Equal(lnkFile1 + ".png", vsCode.IconPath);
            Assert.False(vsCode.IsFolder);

            var chrome = result.Find(a => a.Name == "Google Chrome");
            Assert.NotNull(chrome);
            Assert.Equal("google_chrome", chrome.Id);
        }

        [Fact]
        public async Task ScanApplicationsAsync_WithLnkFilesInSubdirectories_ReturnsAllAppItems()
        {
            // Arrange
            var tempDir = CreateTempDirectory();
            var subDir = Path.Combine(tempDir, "SubFolder");
            Directory.CreateDirectory(subDir);

            var lnkFile1 = Path.Combine(tempDir, "RootApp.lnk");
            var lnkFile2 = Path.Combine(subDir, "SubApp.lnk");

            await File.WriteAllTextAsync(lnkFile1, "dummy shortcut content");
            await File.WriteAllTextAsync(lnkFile2, "dummy shortcut content");

            var scanner = new ClassicLaunchpad.Core.MockAppScanner(tempDir);

            // Act
            var result = await scanner.ScanApplicationsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);

            Assert.Contains(result, a => a.Name == "RootApp" && a.Id == "rootapp");
            Assert.Contains(result, a => a.Name == "SubApp" && a.Id == "subapp");
        }
    }
}
