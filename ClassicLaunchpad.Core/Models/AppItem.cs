using System.Collections.Generic;

namespace ClassicLaunchpad.Core
{
    public class AppItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
        public bool IsFolder { get; set; }
        public List<AppItem> FolderItems { get; set; } = new List<AppItem>();
    }
}
