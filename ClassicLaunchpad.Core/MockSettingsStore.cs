using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClassicLaunchpad.Core
{
    public class MockSettingsStore : ISettingsStore
    {
        private readonly string _filePath;

        public MockSettingsStore(string filePath)
        {
            _filePath = filePath;
        }

        public async Task SaveLayoutAsync(LayoutConfig config)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(config, options);
            await File.WriteAllTextAsync(_filePath, json);
        }

        public async Task<LayoutConfig> LoadLayoutAsync()
        {
            if (!File.Exists(_filePath))
            {
                return new LayoutConfig();
            }

            try
            {
                var json = await File.ReadAllTextAsync(_filePath);
                return JsonSerializer.Deserialize<LayoutConfig>(json) ?? new LayoutConfig();
            }
            catch
            {
                return new LayoutConfig();
            }
        }
    }
}
