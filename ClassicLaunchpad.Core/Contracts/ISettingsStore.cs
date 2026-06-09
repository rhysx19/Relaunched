using System.Threading.Tasks;

namespace ClassicLaunchpad.Core
{
    public interface ISettingsStore
    {
        Task SaveLayoutAsync(LayoutConfig config);
        Task<LayoutConfig> LoadLayoutAsync();
    }
}
