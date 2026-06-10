using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ClassicLaunchpad.Core;

namespace ClassicLaunchpad.Tests
{
    /// <summary>
    /// Test double for IAppScanner. Lives in the test project's own namespace;
    /// it must not masquerade as a ClassicLaunchpad.Core type.
    /// </summary>
    public class MockAppScanner : IAppScanner
    {
        private readonly string _mockPath;
        private readonly List<AppItem>? _presetApps;

        public MockAppScanner(string mockPath)
        {
            _mockPath = mockPath;
        }

        public MockAppScanner(List<AppItem> presetApps)
        {
            _mockPath = string.Empty;
            _presetApps = presetApps;
        }

        public Task<List<AppItem>> ScanApplicationsAsync()
        {
            if (_presetApps != null)
            {
                return Task.FromResult(_presetApps);
            }

            var list = new List<AppItem>();
            if (!string.IsNullOrEmpty(_mockPath) && Directory.Exists(_mockPath))
            {
                var files = Directory.GetFiles(_mockPath, "*.lnk", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    list.Add(new AppItem
                    {
                        Id = name.ToLowerInvariant().Replace(" ", "_"),
                        Name = name,
                        TargetPath = file,
                        IconPath = file + ".png",
                        IsFolder = false
                    });
                }
            }
            return Task.FromResult(list);
        }
    }
}
