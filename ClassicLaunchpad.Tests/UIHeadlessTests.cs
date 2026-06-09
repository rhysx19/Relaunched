using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using ClassicLaunchpad.Core;

namespace ClassicLaunchpad.Tests
{
    public class InMemorySettingsStore : ISettingsStore
    {
        public LayoutConfig Config { get; set; } = new LayoutConfig();

        public Task SaveLayoutAsync(LayoutConfig config)
        {
            Config = config;
            return Task.CompletedTask;
        }

        public Task<LayoutConfig> LoadLayoutAsync()
        {
            return Task.FromResult(Config);
        }
    }

    public class UIHeadlessTests
    {
        private List<AppItem> CreatePresetApps(int count)
        {
            var list = new List<AppItem>();
            for (int i = 0; i < count; i++)
            {
                list.Add(new AppItem
                {
                    Id = $"app_{i}",
                    Name = $"App {i}",
                    TargetPath = $"/path/to/app_{i}",
                    IconPath = $"/path/to/app_{i}.png",
                    IsFolder = false
                });
            }
            return list;
        }

        // Test 1: GridSettingsReflow
        [Fact]
        public async Task Test_GridSettingsReflow()
        {
            var apps = CreatePresetApps(40);
            var scanner = new MockAppScanner(apps);
            var settings = new InMemorySettingsStore();
            settings.Config.Columns = 5;
            settings.Config.Rows = 4;
            settings.Config.IconSize = 90;
            var search = new SearchEngine();

            var vm = new LaunchpadViewModel(scanner, settings, search);

            // Assert defaults before load
            Assert.Equal(7, vm.Columns);
            Assert.Equal(5, vm.Rows);
            Assert.Equal(80, vm.IconSize);
            Assert.Equal(35, vm.PageSize);
            Assert.Equal(1, vm.TotalPages);

            // Load from settings
            await vm.InitializeAsync();

            Assert.Equal(5, vm.Columns);
            Assert.Equal(4, vm.Rows);
            Assert.Equal(90, vm.IconSize);
            Assert.Equal(20, vm.PageSize);
            Assert.Equal(2, vm.TotalPages);

            // Reflow at runtime
            vm.Columns = 3;
            vm.Rows = 3;
            vm.IconSize = 100;
            Assert.Equal(9, vm.PageSize);
            Assert.Equal(5, vm.TotalPages); // 40 apps with PageSize 9 -> 5 pages

            await vm.SaveLayoutAsync();
            Assert.Equal(3, settings.Config.Columns);
            Assert.Equal(3, settings.Config.Rows);
            Assert.Equal(100, settings.Config.IconSize);
        }

        // Test 2: KeyboardNavigationBasic
        [Fact]
        public async Task Test_KeyboardNavigationBasic()
        {
            var apps = CreatePresetApps(10);
            var scanner = new MockAppScanner(apps);
            var settings = new InMemorySettingsStore { Config = new LayoutConfig { Columns = 5, Rows = 2 } };
            var vm = new LaunchpadViewModel(scanner, settings, new SearchEngine());
            await vm.InitializeAsync();

            Assert.Equal(0, vm.SelectedItemIndex);
            Assert.Equal(0, vm.CurrentPageIndex);

            vm.MoveFocusRight();
            Assert.Equal(1, vm.SelectedItemIndex);

            vm.MoveFocusDown(); // column 1, row 1 -> index 6
            Assert.Equal(6, vm.SelectedItemIndex);

            vm.MoveFocusLeft(); // column 0, row 1 -> index 5
            Assert.Equal(5, vm.SelectedItemIndex);

            vm.MoveFocusUp(); // column 0, row 0 -> index 0
            Assert.Equal(0, vm.SelectedItemIndex);
        }

        // Test 3: KeyboardNavigationPageWrapping
        [Fact]
        public async Task Test_KeyboardNavigationPageWrapping()
        {
            var apps = CreatePresetApps(15);
            var scanner = new MockAppScanner(apps);
            var settings = new InMemorySettingsStore { Config = new LayoutConfig { Columns = 5, Rows = 2 } }; // Page size 10
            var vm = new LaunchpadViewModel(scanner, settings, new SearchEngine());
            await vm.InitializeAsync();

            // Set to last item of Page 0 (index 9)
            vm.CurrentPageIndex = 0;
            vm.SelectedItemIndex = 9;

            // Move Right -> wraps to Page 1.
            // Page 1 only has 5 items (indices 0..4 on that page). Target index from row 1, col 4 is index 5 (which is 1 * 5 + 0 if going to column 0? No, wait:
            // "targetIndex = row * Columns". row=1, Columns=5 -> targetIndex = 5.
            // Math.Min(5, 5 - 1) = 4. So selected index becomes 4.
            vm.MoveFocusRight();
            Assert.Equal(1, vm.CurrentPageIndex);
            Assert.Equal(4, vm.SelectedItemIndex);

            // Set to index 0 on Page 1 (col 0, row 0)
            vm.CurrentPageIndex = 1;
            vm.SelectedItemIndex = 0;

            // Move Left -> wraps to Page 0.
            // Page 0 has 10 items. targetIndex = row * Columns + (Columns - 1). row=0 -> targetIndex = 4 (col 4).
            // Math.Min(4, 9) = 4.
            vm.MoveFocusLeft();
            Assert.Equal(0, vm.CurrentPageIndex);
            Assert.Equal(4, vm.SelectedItemIndex);
        }

        // Test 4: KeyboardNavigationPageWrapping_FullPage
        [Fact]
        public async Task Test_KeyboardNavigationPageWrapping_FullPage()
        {
            var apps = CreatePresetApps(20);
            var scanner = new MockAppScanner(apps);
            var settings = new InMemorySettingsStore { Config = new LayoutConfig { Columns = 5, Rows = 2 } };
            var vm = new LaunchpadViewModel(scanner, settings, new SearchEngine());
            await vm.InitializeAsync();

            vm.CurrentPageIndex = 0;
            vm.SelectedItemIndex = 9; // Col 4, Row 1

            vm.MoveFocusRight();
            Assert.Equal(1, vm.CurrentPageIndex);
            Assert.Equal(5, vm.SelectedItemIndex); // Col 0, Row 1
        }

        // Test 5: KeyboardNavigationPageKeys
        [Fact]
        public async Task Test_KeyboardNavigationPageKeys()
        {
            var apps = CreatePresetApps(25);
            var scanner = new MockAppScanner(apps);
            var settings = new InMemorySettingsStore { Config = new LayoutConfig { Columns = 5, Rows = 2 } }; // Page size 10
            var vm = new LaunchpadViewModel(scanner, settings, new SearchEngine());
            await vm.InitializeAsync();

            Assert.Equal(0, vm.CurrentPageIndex);

            vm.SelectedItemIndex = 5;
            vm.NextPage();
            Assert.Equal(1, vm.CurrentPageIndex);
            Assert.Equal(0, vm.SelectedItemIndex); // resets SelectedItemIndex to 0

            vm.NextPage();
            Assert.Equal(2, vm.CurrentPageIndex);

            // Verify bounds logic: calling NextPage on last page is no-op
            vm.NextPage();
            Assert.Equal(2, vm.CurrentPageIndex);

            vm.SelectedItemIndex = 5;
            vm.PrevPage();
            Assert.Equal(1, vm.CurrentPageIndex);
            Assert.Equal(0, vm.SelectedItemIndex);

            vm.PrevPage();
            Assert.Equal(0, vm.CurrentPageIndex);

            // calling PrevPage on first page is no-op
            vm.PrevPage();
            Assert.Equal(0, vm.CurrentPageIndex);
        }

        // Test 6: EscapeDismissesLauncher
        [Fact]
        public async Task Test_EscapeDismissesLauncher()
        {
            var vm = new LaunchpadViewModel(new MockAppScanner(new List<AppItem>()), new InMemorySettingsStore(), new SearchEngine());
            await vm.InitializeAsync();

            vm.Show();
            Assert.True(vm.IsVisible);
            Assert.True(vm.TaskbarHidden);

            vm.PressEscape();
            Assert.False(vm.IsVisible);
            Assert.False(vm.TaskbarHidden);
        }

        // Test 7: EscapeClearsSearchFirst
        [Fact]
        public async Task Test_EscapeClearsSearchFirst()
        {
            var apps = CreatePresetApps(5);
            var vm = new LaunchpadViewModel(new MockAppScanner(apps), new InMemorySettingsStore(), new SearchEngine());
            await vm.InitializeAsync();

            vm.Show();
            vm.UpdateSearch("App 1");
            Assert.Equal("App 1", vm.SearchText);
            Assert.True(vm.IsVisible);

            vm.PressEscape();
            Assert.Equal(string.Empty, vm.SearchText);
            Assert.True(vm.IsVisible); // Still visible!

            vm.PressEscape();
            Assert.False(vm.IsVisible); // Dismissed
        }

        // Test 8: EscapeClosesFolderFirst
        [Fact]
        public async Task Test_EscapeClosesFolderFirst()
        {
            var folder = new AppItem { Id = "f1", Name = "Folder 1", IsFolder = true, FolderItems = CreatePresetApps(2) };
            var vm = new LaunchpadViewModel(new MockAppScanner(new List<AppItem>()), new InMemorySettingsStore(), new SearchEngine());
            await vm.InitializeAsync();

            vm.Show();
            vm.UpdateSearch("App");
            vm.OpenFolderOverlay(folder);
            Assert.Equal(folder, vm.OpenFolder);
            Assert.Equal("App", vm.SearchText);
            Assert.True(vm.IsVisible);

            vm.PressEscape();
            Assert.Null(vm.OpenFolder); // Closed folder overlay first
            Assert.Equal("App", vm.SearchText); // Search text remains
            Assert.True(vm.IsVisible); // Launcher remains visible

            vm.PressEscape();
            Assert.Equal(string.Empty, vm.SearchText); // Clears search next

            vm.PressEscape();
            Assert.False(vm.IsVisible); // Finally hides launcher
        }

        // Test 9: FolderCreationByGrouping
        [Fact]
        public async Task Test_FolderCreationByGrouping()
        {
            var apps = CreatePresetApps(5);
            var scanner = new MockAppScanner(apps);
            var vm = new LaunchpadViewModel(scanner, new InMemorySettingsStore(), new SearchEngine());
            await vm.InitializeAsync();

            var app0 = vm.AllItems[0];
            var app1 = vm.AllItems[1];

            vm.CreateFolder(app1, app0); // Drag app1 (source) onto app0 (target)

            Assert.Equal(4, vm.AllItems.Count);
            var folder = vm.AllItems[0];
            Assert.True(folder.IsFolder);
            Assert.Equal("New Folder", folder.Name);
            Assert.StartsWith("folder_", folder.Id);
            Assert.Equal(2, folder.FolderItems.Count);
            Assert.Equal("app_0", folder.FolderItems[0].Id);
            Assert.Equal("app_1", folder.FolderItems[1].Id);

            Assert.DoesNotContain(app1, vm.AllItems);
            Assert.Equal(4, vm.DisplayedItems.Count);
            Assert.Equal(0, vm.SelectedItemIndex);
            Assert.Equal(0, vm.CurrentPageIndex);
        }

        // Test 10: FolderCreationByGrouping_IgnoredIfFolder
        [Fact]
        public async Task Test_FolderCreationByGrouping_IgnoredIfFolder()
        {
            var apps = CreatePresetApps(3);
            var scanner = new MockAppScanner(apps);
            var vm = new LaunchpadViewModel(scanner, new InMemorySettingsStore(), new SearchEngine());
            await vm.InitializeAsync();

            var app0 = vm.AllItems[0];
            var app1 = vm.AllItems[1];
            var app2 = vm.AllItems[2];

            vm.CreateFolder(app1, app0);
            var folder = vm.AllItems[0];
            Assert.True(folder.IsFolder);

            int countBefore = vm.AllItems.Count;

            // Attempt to create a folder with a folder as source (no-op)
            vm.CreateFolder(folder, app2);
            Assert.Equal(countBefore, vm.AllItems.Count);

            // Attempt to create a folder with a folder as target (no-op)
            vm.CreateFolder(app2, folder);
            Assert.Equal(countBefore, vm.AllItems.Count);
        }

        // Test 11: FolderRenameCommitsOnEnter
        [Fact]
        public async Task Test_FolderRenameCommitsOnEnter()
        {
            var folder = new AppItem { Id = "f1", Name = "Original Name", IsFolder = true, FolderItems = CreatePresetApps(2) };
            var vm = new LaunchpadViewModel(new MockAppScanner(new List<AppItem> { folder }), new InMemorySettingsStore(), new SearchEngine());
            await vm.InitializeAsync();

            vm.OpenFolderOverlay(folder);
            Assert.Equal(folder, vm.OpenFolder);
            Assert.Equal("Original Name", vm.FolderRenameText);

            vm.FolderRenameText = "New Folder Name";
            vm.PressEnter();

            Assert.Equal("New Folder Name", folder.Name);
            Assert.Equal(folder, vm.OpenFolder); // Overlay remains open
        }

        // Test 12: FolderRenameViaHelper
        [Fact]
        public async Task Test_FolderRenameViaHelper()
        {
            var folder = new AppItem { Id = "f1", Name = "Original Name", IsFolder = true, FolderItems = CreatePresetApps(2) };
            var vm = new LaunchpadViewModel(new MockAppScanner(new List<AppItem> { folder }), new InMemorySettingsStore(), new SearchEngine());
            await vm.InitializeAsync();

            vm.OpenFolderOverlay(folder);
            vm.RenameFolder("Helper Renamed");

            Assert.Equal("Helper Renamed", folder.Name);
            Assert.Equal("Helper Renamed", vm.FolderRenameText);
        }

        // Test 13: FolderDragOutAppReflows
        [Fact]
        public async Task Test_FolderDragOutAppReflows()
        {
            var app1 = new AppItem { Id = "app1", Name = "App 1" };
            var app2 = new AppItem { Id = "app2", Name = "App 2" };
            var app3 = new AppItem { Id = "app3", Name = "App 3" };
            var folder = new AppItem
            {
                Id = "f1",
                Name = "My Folder",
                IsFolder = true,
                FolderItems = new List<AppItem> { app1, app2, app3 }
            };

            var vm = new LaunchpadViewModel(new MockAppScanner(new List<AppItem> { folder }), new InMemorySettingsStore(), new SearchEngine());
            await vm.InitializeAsync();

            vm.OpenFolderOverlay(folder);
            vm.DragAppOutOfFolder(app1);

            Assert.Equal(2, folder.FolderItems.Count);
            Assert.Equal(2, vm.AllItems.Count);
            Assert.Contains(folder, vm.AllItems);
            Assert.Contains(app1, vm.AllItems);
            Assert.Equal(folder, vm.OpenFolder); // Folder still has 2 items, overlay remains open
        }

        // Test 14: FolderDissolvesWhenEmpty
        [Fact]
        public async Task Test_FolderDissolvesWhenEmpty()
        {
            var app1 = new AppItem { Id = "app1", Name = "App 1" };
            var app2 = new AppItem { Id = "app2", Name = "App 2" };
            var folder = new AppItem
            {
                Id = "f1",
                Name = "My Folder",
                IsFolder = true,
                FolderItems = new List<AppItem> { app1, app2 }
            };

            var vm = new LaunchpadViewModel(new MockAppScanner(new List<AppItem> { folder }), new InMemorySettingsStore(), new SearchEngine());
            await vm.InitializeAsync();

            vm.OpenFolderOverlay(folder);
            vm.DragAppOutOfFolder(app1);

            Assert.Null(vm.OpenFolder); // Closed
            Assert.Equal(2, vm.AllItems.Count);
            Assert.Contains(app1, vm.AllItems);
            Assert.Contains(app2, vm.AllItems);
            Assert.DoesNotContain(folder, vm.AllItems); // dissolved
        }

        // Test 15: SearchFiltersAppsInstantly
        [Fact]
        public async Task Test_SearchFiltersAppsInstantly()
        {
            var apps = new List<AppItem>
            {
                new AppItem { Id = "chrome", Name = "Google Chrome" },
                new AppItem { Id = "safari", Name = "Safari" },
                new AppItem { Id = "firefox", Name = "Firefox" }
            };
            var vm = new LaunchpadViewModel(new MockAppScanner(apps), new InMemorySettingsStore(), new SearchEngine());
            await vm.InitializeAsync();

            Assert.Equal(3, vm.DisplayedItems.Count);

            vm.UpdateSearch("fire");
            Assert.Equal("fire", vm.SearchText);
            Assert.Single(vm.DisplayedItems);
            Assert.Equal("firefox", vm.DisplayedItems[0].Id);

            vm.UpdateSearch(string.Empty);
            Assert.Equal(3, vm.DisplayedItems.Count);
        }

        // Test 16: SearchMathParserQuickCard
        [Fact]
        public async Task Test_SearchMathParserQuickCard()
        {
            var apps = CreatePresetApps(5);
            var vm = new LaunchpadViewModel(new MockAppScanner(apps), new InMemorySettingsStore(), new SearchEngine());
            await vm.InitializeAsync();

            vm.UpdateSearch("2 + 3 * 4");
            Assert.True(vm.IsMathVisible);
            Assert.Equal("14", vm.MathResult);
            Assert.Empty(vm.DisplayedItems);

            vm.UpdateSearch("2 / 0"); // invalid math (division by zero throws, falls back to search)
            Assert.False(vm.IsMathVisible);
            Assert.Empty(vm.MathResult);
            Assert.Empty(vm.DisplayedItems); // no match found
        }

        // Test 17: SearchSystemActionExecution
        [Fact]
        public async Task Test_SearchSystemActionExecution()
        {
            var apps = CreatePresetApps(5);
            var vm = new LaunchpadViewModel(new MockAppScanner(apps), new InMemorySettingsStore(), new SearchEngine());
            await vm.InitializeAsync();

            vm.Show();
            Assert.True(vm.IsVisible);

            vm.UpdateSearch("sleep");
            Assert.Equal(SystemActionType.Sleep, vm.PendingSystemAction);
            Assert.Equal(SystemActionType.Sleep, vm.ExecutedSystemAction);
            Assert.False(vm.IsVisible);
        }

        // Test 18: TaskbarHidingToggle
        [Fact]
        public async Task Test_TaskbarHidingToggle()
        {
            var vm = new LaunchpadViewModel(new MockAppScanner(new List<AppItem>()), new InMemorySettingsStore(), new SearchEngine());
            await vm.InitializeAsync();

            Assert.False(vm.IsVisible);
            Assert.False(vm.TaskbarHidden);

            vm.Show();
            Assert.True(vm.IsVisible);
            Assert.True(vm.TaskbarHidden);

            vm.Hide();
            Assert.False(vm.IsVisible);
            Assert.False(vm.TaskbarHidden);
        }

        // Test 19: PressEnterLaunchesApp
        [Fact]
        public async Task Test_PressEnterLaunchesApp()
        {
            var apps = CreatePresetApps(5);
            var vm = new LaunchpadViewModel(new MockAppScanner(apps), new InMemorySettingsStore(), new SearchEngine());
            await vm.InitializeAsync();

            vm.Show();
            vm.SelectedItemIndex = 2; // app_2

            vm.PressEnter();

            Assert.Equal("app_2", vm.LaunchedApp?.Id);
            Assert.False(vm.IsVisible);
        }

        // Test 20: PressEnterOpensFolderOverlay
        [Fact]
        public async Task Test_PressEnterOpensFolderOverlay()
        {
            var folder = new AppItem { Id = "f1", Name = "Folder 1", IsFolder = true, FolderItems = CreatePresetApps(2) };
            var vm = new LaunchpadViewModel(new MockAppScanner(new List<AppItem> { folder }), new InMemorySettingsStore(), new SearchEngine());
            await vm.InitializeAsync();

            vm.Show();
            vm.SelectedItemIndex = 0; // The folder

            vm.PressEnter();

            Assert.Equal(folder, vm.OpenFolder);
            Assert.True(vm.IsVisible);
        }

        // Test 21: DragAppOutOfFolder_AppNotInFolder_DoesNotDuplicateApp
        [Fact]
        public async Task Test_DragAppOutOfFolder_AppNotInFolder_DoesNotDuplicateApp()
        {
            var app1 = new AppItem { Id = "app1", Name = "App 1" };
            var app2 = new AppItem { Id = "app2", Name = "App 2" };
            var app3 = new AppItem { Id = "app3", Name = "App 3" };
            var folder = new AppItem
            {
                Id = "f1",
                Name = "My Folder",
                IsFolder = true,
                FolderItems = new List<AppItem> { app1, app2 }
            };

            var vm = new LaunchpadViewModel(new MockAppScanner(new List<AppItem> { folder, app3 }), new InMemorySettingsStore(), new SearchEngine());
            await vm.InitializeAsync();

            vm.OpenFolderOverlay(folder);
            
            // Drag app3 (which is outside the folder) out of the folder
            vm.DragAppOutOfFolder(app3);

            // Assert: app3 is not duplicated in AllItems
            var app3Count = vm.AllItems.Count(x => x.Id == "app3");
            Assert.Equal(1, app3Count);
        }

        // Test 22: ViewModel_ColumnsZero_ClampsToOneAndDoesNotThrow
        [Fact]
        public async Task Test_ViewModel_ColumnsZero_ClampsToOneAndDoesNotThrow()
        {
            var vm = new LaunchpadViewModel(new MockAppScanner(new List<AppItem>()), new InMemorySettingsStore(), new SearchEngine());
            await vm.InitializeAsync();
            vm.Columns = 0;
            
            // Assert that columns clamps to 1
            Assert.Equal(1, vm.Columns);
            
            // Accessing TotalPages should not throw divide-by-zero since columns is clamped to 1
            var totalPages = vm.TotalPages;
            Assert.Equal(1, totalPages);
        }

        // Test 23: CreateFolder_SourceEqualsTarget_DoesNotCreateFolder
        [Fact]
        public async Task Test_CreateFolder_SourceEqualsTarget_DoesNotCreateFolder()
        {
            var app = new AppItem { Id = "app1", Name = "App 1" };
            var vm = new LaunchpadViewModel(new MockAppScanner(new List<AppItem> { app }), new InMemorySettingsStore(), new SearchEngine());
            await vm.InitializeAsync();

            // Act: Try to create a folder with source == target
            vm.CreateFolder(app, app);

            // Assert: no folder is created, the item remains in AllItems as is
            Assert.Single(vm.AllItems);
            Assert.False(vm.AllItems[0].IsFolder);
            Assert.Equal("app1", vm.AllItems[0].Id);
        }

        // Test 24: ViewModel_NegativeLayoutSettings_ClampsToOneAndReturnsApps
        [Fact]
        public async Task Test_ViewModel_NegativeLayoutSettings_ClampsToOneAndReturnsApps()
        {
            var apps = CreatePresetApps(5);
            var vm = new LaunchpadViewModel(new MockAppScanner(apps), new InMemorySettingsStore(), new SearchEngine());
            await vm.InitializeAsync();

            vm.Columns = -1;
            vm.Rows = 1;

            // Clamped to 1, so PageSize is Columns * Rows = 1 * 1 = 1
            Assert.Equal(1, vm.PageSize);
            Assert.Equal(5, vm.TotalPages);

            // Assert: Returns first item on page 0 since page is not empty (contains the apps)
            var items = vm.GetItemsOnPage(0);
            Assert.Single(items);
            Assert.Equal("app_0", items[0].Id);
        }

        // Test 25: InitializeAsync_ScannerReturnsNull_HandlesGracefully
        [Fact]
        public async Task Test_InitializeAsync_ScannerReturnsNull_HandlesGracefully()
        {
            var scanner = new NullMockAppScanner();
            var vm = new LaunchpadViewModel(scanner, new InMemorySettingsStore(), new SearchEngine());

            // Act & Assert: Should not throw, resulting in an empty list
            await vm.InitializeAsync();
            Assert.Empty(vm.AllItems);
        }

        // Test 26: InitializeAsync_ScannerReturnsNullItems_HandlesGracefully
        [Fact]
        public async Task Test_InitializeAsync_ScannerReturnsNullItems_HandlesGracefully()
        {
            var apps = new List<AppItem> { null! };
            var scanner = new MockAppScanner(apps);
            var vm = new LaunchpadViewModel(scanner, new InMemorySettingsStore(), new SearchEngine());

            // Act & Assert: Should not throw, filtering out the null item (resulting in empty list)
            await vm.InitializeAsync();
            Assert.Empty(vm.AllItems);
        }

        // Test 27: KeyboardNavigation_MoveFocus_EmptyGrid_NoOp
        [Fact]
        public async Task Test_KeyboardNavigation_MoveFocus_EmptyGrid_NoOp()
        {
            var vm = new LaunchpadViewModel(new MockAppScanner(new List<AppItem>()), new InMemorySettingsStore(), new SearchEngine());
            await vm.InitializeAsync();

            vm.UpdateSearch("NonExistentApp");
            Assert.Empty(vm.DisplayedItems);

            vm.MoveFocusUp();
            Assert.Equal(0, vm.SelectedItemIndex);

            vm.MoveFocusDown();
            Assert.Equal(0, vm.SelectedItemIndex);
        }
    }

    public class NullMockAppScanner : IAppScanner
    {
        public Task<List<AppItem>> ScanApplicationsAsync()
        {
            return Task.FromResult<List<AppItem>>(null!);
        }
    }
}
