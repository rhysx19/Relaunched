using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClassicLaunchpad.Core
{
    public class SettingsStore : ISettingsStore
    {
        public string FilePath { get; }

        public SettingsStore()
        {
            FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClassicLaunchpad", "layout.json");
        }

        public SettingsStore(string filePath)
        {
            FilePath = filePath;
        }

        public async Task SaveLayoutAsync(LayoutConfig config)
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(config, options);

            // Write to a temp file then rename so a crash mid-write can never
            // leave a truncated/corrupt layout.json behind.
            var tempPath = FilePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json);
            try
            {
                File.Move(tempPath, FilePath, overwrite: true);
            }
            catch
            {
                try { File.Delete(tempPath); } catch { /* best effort */ }
                throw;
            }
        }

        public async Task<LayoutConfig> LoadLayoutAsync()
        {
            if (!File.Exists(FilePath))
            {
                return new LayoutConfig();
            }

            try
            {
                var json = await File.ReadAllTextAsync(FilePath);
                return JsonSerializer.Deserialize<LayoutConfig>(json) ?? new LayoutConfig();
            }
            catch (Exception ex)
            {
                // Fall back to defaults, but leave a trace instead of silently
                // wiping the user's layout.
                System.Diagnostics.Debug.WriteLine($"ClassicLaunchpad: failed to load layout from '{FilePath}': {ex.Message}");
                return new LayoutConfig();
            }
        }
    }
}
