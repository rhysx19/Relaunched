using System.Collections.Generic;

namespace ClassicLaunchpad.Core
{
    public interface ISearchEngine
    {
        SearchResult Query(string input, List<AppItem> pool);
    }
}
