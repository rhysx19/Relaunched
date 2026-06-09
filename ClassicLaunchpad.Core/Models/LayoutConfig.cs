using System.Collections.Generic;

namespace ClassicLaunchpad.Core
{
    public class LayoutConfig
    {
        public int Columns { get; set; } = 7;
        public int Rows { get; set; } = 5;
        public int IconSize { get; set; } = 80;
        public List<string> PageOrder { get; set; } = new List<string>();
        public Dictionary<string, List<string>> Folders { get; set; } = new Dictionary<string, List<string>>();
    }
}
