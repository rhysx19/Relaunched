using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClassicLaunchpad.Core
{
    public interface IAppScanner
    {
        Task<List<AppItem>> ScanApplicationsAsync();
    }
}
