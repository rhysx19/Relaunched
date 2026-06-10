using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassicLaunchpad.Core
{
    public class LaunchpadViewModel
    {
        private readonly IAppScanner _scanner;
        private readonly ISettingsStore _settingsStore;
        private readonly ISearchEngine _searchEngine;

        public List<AppItem> AllItems { get; private set; } = new List<AppItem>();
        public List<AppItem> DisplayedItems { get; private set; } = new List<AppItem>();

        public bool IsVisible { get; set; }

        private int _columns = 7;
        private int _rows = 5;

        public int Columns
        {
            get => _columns;
            set => _columns = Math.Max(1, value);
        }

        public int Rows
        {
            get => _rows;
            set => _rows = Math.Max(1, value);
        }

        public int IconSize { get; set; } = 80;
        public int CurrentPageIndex { get; set; }
        public int SelectedItemIndex { get; set; } // 0-indexed index on the current page
        public string SearchText { get; set; } = string.Empty;
        
        public bool IsMathVisible { get; private set; }
        public string MathResult { get; private set; } = string.Empty;
        public SystemActionType PendingSystemAction { get; private set; } = SystemActionType.None;

        public AppItem? OpenFolder { get; private set; }
        public string FolderRenameText { get; set; } = string.Empty;

        public AppItem? LaunchedApp { get; private set; }
        public SystemActionType ExecutedSystemAction { get; private set; } = SystemActionType.None;
        public bool TaskbarHidden { get; private set; }

        public LaunchpadViewModel(IAppScanner scanner, ISettingsStore settingsStore, ISearchEngine searchEngine)
        {
            _scanner = scanner;
            _settingsStore = settingsStore;
            _searchEngine = searchEngine;
        }

        public int PageSize => Columns * Rows;
        public int TotalPages => Math.Max(1, (DisplayedItems.Count + PageSize - 1) / PageSize);

        public async System.Threading.Tasks.Task InitializeAsync()
        {
            var config = await _settingsStore.LoadLayoutAsync();
            Columns = config.Columns > 0 ? config.Columns : 7;
            Rows = config.Rows > 0 ? config.Rows : 5;
            IconSize = config.IconSize > 0 ? config.IconSize : 80;

            var apps = await _scanner.ScanApplicationsAsync();
            if (apps == null)
            {
                apps = new List<AppItem>();
            }
            
            var folderDict = new Dictionary<string, AppItem>();
            var topItems = new List<AppItem>();
            var processedAppIds = new HashSet<string>();

            if (config.Folders != null)
            {
                foreach (var kvp in config.Folders)
                {
                    string displayName = kvp.Key;
                    if (config.FolderNames != null &&
                        config.FolderNames.TryGetValue(kvp.Key, out var savedName) &&
                        !string.IsNullOrWhiteSpace(savedName))
                    {
                        displayName = savedName;
                    }

                    var folder = new AppItem
                    {
                        Id = kvp.Key,
                        Name = displayName,
                        IsFolder = true,
                        FolderItems = new List<AppItem>()
                    };
                    folderDict[kvp.Key] = folder;
                }
            }

            foreach (var app in apps)
            {
                if (app == null) continue;
                string? parentFolderId = null;
                if (config.Folders != null)
                {
                    foreach (var kvp in config.Folders)
                    {
                        if (kvp.Value.Contains(app.Id))
                        {
                            parentFolderId = kvp.Key;
                            break;
                        }
                    }
                }

                if (parentFolderId != null && folderDict.ContainsKey(parentFolderId))
                {
                    folderDict[parentFolderId].FolderItems.Add(app);
                    processedAppIds.Add(app.Id);
                }
            }

            foreach (var folder in folderDict.Values)
            {
                if (folder.FolderItems.Count > 0)
                {
                    topItems.Add(folder);
                }
            }

            foreach (var app in apps)
            {
                if (app == null) continue;
                if (!processedAppIds.Contains(app.Id))
                {
                    topItems.Add(app);
                }
            }

            if (config.PageOrder != null && config.PageOrder.Count > 0)
            {
                var sorted = new List<AppItem>();
                foreach (var id in config.PageOrder)
                {
                    var found = topItems.FirstOrDefault(x => x.Id == id);
                    if (found != null)
                    {
                        sorted.Add(found);
                        topItems.Remove(found);
                    }
                }
                sorted.AddRange(topItems);
                AllItems = sorted;
            }
            else
            {
                AllItems = topItems;
            }

            DisplayedItems = new List<AppItem>(AllItems);
            CurrentPageIndex = 0;
            SelectedItemIndex = 0;
        }

        public async System.Threading.Tasks.Task SaveLayoutAsync()
        {
            var config = new LayoutConfig
            {
                Columns = Columns,
                Rows = Rows,
                IconSize = IconSize,
                PageOrder = AllItems.Select(x => x.Id).ToList(),
                Folders = AllItems
                    .Where(x => x.IsFolder)
                    .ToDictionary(x => x.Id, x => x.FolderItems.Select(y => y.Id).ToList()),
                FolderNames = AllItems
                    .Where(x => x.IsFolder)
                    .ToDictionary(x => x.Id, x => x.Name)
            };
            try
            {
                await _settingsStore.SaveLayoutAsync(config);
            }
            catch (System.Exception)
            {
                // Ignore settings write errors to prevent application crashes
            }
        }

        public void Show()
        {
            IsVisible = true;
            TaskbarHidden = true;
        }

        public void Hide()
        {
            IsVisible = false;
            TaskbarHidden = false;
        }

        public void MoveFocusLeft()
        {
            var curPageItemsCount = GetItemsOnPage(CurrentPageIndex).Count;
            if (curPageItemsCount == 0) return;

            int col = SelectedItemIndex % Columns;
            int row = SelectedItemIndex / Columns;

            if (col > 0)
            {
                SelectedItemIndex--;
            }
            else
            {
                if (CurrentPageIndex > 0)
                {
                    CurrentPageIndex--;
                    var newPageItemsCount = GetItemsOnPage(CurrentPageIndex).Count;
                    int targetIndex = row * Columns + (Columns - 1);
                    SelectedItemIndex = Math.Min(targetIndex, newPageItemsCount - 1);
                }
            }
        }

        public void MoveFocusRight()
        {
            var curPageItemsCount = GetItemsOnPage(CurrentPageIndex).Count;
            if (curPageItemsCount == 0) return;

            int col = SelectedItemIndex % Columns;
            int row = SelectedItemIndex / Columns;

            if (col < Columns - 1 && SelectedItemIndex + 1 < curPageItemsCount)
            {
                SelectedItemIndex++;
            }
            else
            {
                if (CurrentPageIndex < TotalPages - 1)
                {
                    CurrentPageIndex++;
                    int targetIndex = row * Columns;
                    var newPageItemsCount = GetItemsOnPage(CurrentPageIndex).Count;
                    SelectedItemIndex = Math.Min(targetIndex, newPageItemsCount - 1);
                }
            }
        }

        public void MoveFocusUp()
        {
            if (SelectedItemIndex >= Columns)
            {
                SelectedItemIndex -= Columns;
            }
        }

        public void MoveFocusDown()
        {
            var curPageItemsCount = GetItemsOnPage(CurrentPageIndex).Count;
            if (SelectedItemIndex + Columns < curPageItemsCount)
            {
                SelectedItemIndex += Columns;
            }
        }

        public void NextPage()
        {
            if (CurrentPageIndex < TotalPages - 1)
            {
                CurrentPageIndex++;
                SelectedItemIndex = 0;
            }
        }

        public void PrevPage()
        {
            if (CurrentPageIndex > 0)
            {
                CurrentPageIndex--;
                SelectedItemIndex = 0;
            }
        }

        public void PressEnter()
        {
            if (OpenFolder != null)
            {
                if (!string.IsNullOrWhiteSpace(FolderRenameText) && FolderRenameText != OpenFolder.Name)
                {
                    CommitFolderRename();
                }
                return;
            }

            if (PendingSystemAction != SystemActionType.None)
            {
                ExecuteSystemAction(PendingSystemAction);
                return;
            }

            var itemsOnPage = GetItemsOnPage(CurrentPageIndex);
            if (SelectedItemIndex < 0 || SelectedItemIndex >= itemsOnPage.Count) return;

            var selected = itemsOnPage[SelectedItemIndex];
            if (selected.IsFolder)
            {
                OpenFolderOverlay(selected);
            }
            else
            {
                LaunchApp(selected);
            }
        }

        public void PressEscape()
        {
            if (OpenFolder != null)
            {
                CloseFolderOverlay();
            }
            else if (!string.IsNullOrEmpty(SearchText))
            {
                UpdateSearch(string.Empty);
            }
            else
            {
                Hide();
            }
        }

        public void UpdateSearch(string text)
        {
            SearchText = text;

            // Re-evaluate the pending action on every keystroke so a stale action
            // can never be executed by a later Enter press.
            PendingSystemAction = SystemActionType.None;

            var searchResult = _searchEngine.Query(text, AllItems.Where(x => !x.IsFolder).ToList());

            if (searchResult.IsSystemAction)
            {
                // Arm the action only; it is executed exclusively by an explicit
                // Enter press (PressEnter), never as a side effect of typing.
                PendingSystemAction = searchResult.SystemAction;
                IsMathVisible = false;
                MathResult = string.Empty;
                DisplayedItems = new List<AppItem>();
                CurrentPageIndex = 0;
                SelectedItemIndex = 0;
                return;
            }

            if (searchResult.IsMathExpression)
            {
                IsMathVisible = true;
                MathResult = searchResult.MathResult;
                DisplayedItems = new List<AppItem>();
            }
            else
            {
                IsMathVisible = false;
                MathResult = string.Empty;
                if (string.IsNullOrEmpty(text))
                {
                    DisplayedItems = new List<AppItem>(AllItems);
                }
                else
                {
                    DisplayedItems = searchResult.FilteredApps;
                }
            }

            CurrentPageIndex = 0;
            SelectedItemIndex = 0;
        }

        public void CreateFolder(AppItem source, AppItem target)
        {
            if (source == target) return;
            if (source.IsFolder || target.IsFolder) return;

            var folderId = "folder_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var folder = new AppItem
            {
                Id = folderId,
                Name = "New Folder",
                IsFolder = true,
                FolderItems = new List<AppItem> { target, source }
            };

            AllItems.Remove(source);
            int idx = AllItems.IndexOf(target);
            if (idx >= 0)
            {
                AllItems[idx] = folder;
            }
            else
            {
                AllItems.Add(folder);
            }

            DisplayedItems = new List<AppItem>(AllItems);
            SelectedItemIndex = 0;
            CurrentPageIndex = 0;
        }

        public void OpenFolderOverlay(AppItem folder)
        {
            OpenFolder = folder;
            FolderRenameText = folder.Name;
        }

        public void CloseFolderOverlay()
        {
            OpenFolder = null;
            FolderRenameText = string.Empty;
        }

        public void RenameFolder(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return;
            FolderRenameText = newName;
            CommitFolderRename();
        }

        private void CommitFolderRename()
        {
            if (OpenFolder != null && !string.IsNullOrWhiteSpace(FolderRenameText))
            {
                OpenFolder.Name = FolderRenameText;
            }
        }

        public void DragAppOutOfFolder(AppItem app)
        {
            if (OpenFolder == null || !OpenFolder.FolderItems.Contains(app)) return;

            OpenFolder.FolderItems.Remove(app);
            AllItems.Add(app);

            if (OpenFolder.FolderItems.Count <= 1)
            {
                foreach (var remaining in OpenFolder.FolderItems)
                {
                    AllItems.Add(remaining);
                }
                AllItems.Remove(OpenFolder);
                CloseFolderOverlay();
            }

            DisplayedItems = new List<AppItem>(AllItems);
            SelectedItemIndex = 0;
            CurrentPageIndex = 0;
        }

        public List<AppItem> GetItemsOnPage(int pageIndex)
        {
            if (pageIndex < 0 || pageIndex >= TotalPages) return new List<AppItem>();
            return DisplayedItems.Skip(pageIndex * PageSize).Take(PageSize).ToList();
        }

        private void LaunchApp(AppItem app)
        {
            LaunchedApp = app;
            Hide();
        }

        private void ExecuteSystemAction(SystemActionType action)
        {
            ExecutedSystemAction = action;
            Hide();
        }

        /// <summary>
        /// Clears one-shot action results (launched app / executed system action)
        /// after the UI has handled them, so they cannot fire again on a later
        /// Enter or Escape press.
        /// </summary>
        public void ClearActionResults()
        {
            LaunchedApp = null;
            ExecutedSystemAction = SystemActionType.None;
        }
    }
}
