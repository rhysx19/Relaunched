using System.Collections.Generic;

namespace ClassicLaunchpad.Core
{
    public class SearchResult
    {
        public bool IsMathExpression { get; set; }
        public string MathResult { get; set; } = string.Empty;
        public bool IsSystemAction { get; set; }
        public SystemActionType SystemAction { get; set; }
        public List<AppItem> FilteredApps { get; set; } = new List<AppItem>();
    }
}
