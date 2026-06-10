using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using ClassicLaunchpad.Core;

namespace ClassicLaunchpad.Tests
{
    public class IntegrationTests
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

        // Tier 3: Cross-Feature Combinatorial Tests

        [Fact]
        public async Task Test_HotkeyToggleInsideFolder()
        {
            var apps = CreatePresetApps(3);
            var folder = new AppItem { Id = "f1", Name = "Folder 1", IsFolder = true, FolderItems = apps };
            var vm = new LaunchpadViewModel(new MockAppScanner(new List<AppItem> { folder }), new InMemorySettingsStore(), new SearchEngine());
            await vm.InitializeAsync();

            // 1. Escape key sequence
            vm.Show();
            vm.OpenFolderOverlay(folder);
            Assert.True(vm.IsVisible);
            Assert.Equal(folder, vm.OpenFolder);

            // First escape closes folder overlay
            vm.PressEscape();
            Assert.Null(vm.OpenFolder);
            Assert.True(vm.IsVisible);

            // Second escape hides the launcher
            vm.PressEscape();
            Assert.False(vm.IsVisible);

            // 2. Hotkey toggle sequence
            vm.Show();
            vm.OpenFolderOverlay(folder);
            Assert.True(vm.IsVisible);
            Assert.Equal(folder, vm.OpenFolder);

            // Simulate hotkey toggle-off while folder open: closes folder overlay first
            if (vm.OpenFolder != null)
            {
                vm.CloseFolderOverlay();
            }
            else if (vm.IsVisible)
            {
                vm.Hide();
            }
            else
            {
                vm.Show();
            }
            Assert.Null(vm.OpenFolder);
            Assert.True(vm.IsVisible);

            // Simulate hotkey toggle-off again: hides launcher
            if (vm.OpenFolder != null)
            {
                vm.CloseFolderOverlay();
            }
            else if (vm.IsVisible)
            {
                vm.Hide();
            }
            else
            {
                vm.Show();
            }
            Assert.False(vm.IsVisible);
        }

        [Fact]
        public async Task Test_KeyboardNavInSearchResults()
        {
            var apps = new List<AppItem>
            {
                new AppItem { Id = "chrome", Name = "Google Chrome" },
                new AppItem { Id = "safari", Name = "Safari" },
                new AppItem { Id = "firefox", Name = "Firefox" },
                new AppItem { Id = "slack", Name = "Slack" },
                new AppItem { Id = "spotify", Name = "Spotify" }
            };
            var vm = new LaunchpadViewModel(new MockAppScanner(apps), new InMemorySettingsStore(), new SearchEngine());
            await vm.InitializeAsync();

            vm.Show();

            // Type "S" to filter results (Safari, Slack, Spotify should match)
            vm.UpdateSearch("S");
            Assert.Equal(3, vm.DisplayedItems.Count);
            Assert.Equal("Safari", vm.DisplayedItems[0].Name);
            Assert.Equal("Slack", vm.DisplayedItems[1].Name);
            Assert.Equal("Spotify", vm.DisplayedItems[2].Name);

            // Arrow right navigates within filtered results
            Assert.Equal(0, vm.SelectedItemIndex); // Points to Safari
            vm.MoveFocusRight();
            Assert.Equal(1, vm.SelectedItemIndex); // Points to Slack
            vm.MoveFocusRight();
            Assert.Equal(2, vm.SelectedItemIndex); // Points to Spotify

            // Escape clears search
            vm.PressEscape();
            Assert.Equal(string.Empty, vm.SearchText);
            Assert.True(vm.IsVisible);
            Assert.Equal(5, vm.DisplayedItems.Count);

            // Re-type and navigate again to launch
            vm.UpdateSearch("S");
            vm.MoveFocusRight(); // Slack
            vm.MoveFocusRight(); // Spotify
            
            // Enter launches selected filtered app
            vm.PressEnter();
            Assert.Equal("spotify", vm.LaunchedApp?.Id);
            Assert.False(vm.IsVisible);
        }

        [Fact]
        public async Task Test_CreateFolderFromSearchResults()
        {
            var apps = new List<AppItem>
            {
                new AppItem { Id = "apple", Name = "Apple Mail" },
                new AppItem { Id = "apricot", Name = "Apricot" },
                new AppItem { Id = "banana", Name = "Banana" }
            };
            var vm = new LaunchpadViewModel(new MockAppScanner(apps), new InMemorySettingsStore(), new SearchEngine());
            await vm.InitializeAsync();

            vm.Show();
            vm.UpdateSearch("Ap");
            Assert.Equal(2, vm.DisplayedItems.Count); // Apple Mail, Apricot

            var target = vm.DisplayedItems[0];
            var source = vm.DisplayedItems[1];

            // Create folder by grouping filtered apps
            vm.CreateFolder(source, target);

            // Search is cleared and main grid reflowed
            vm.UpdateSearch(string.Empty);

            // We expect 2 items in main grid now: a folder and Banana
            Assert.Equal(2, vm.AllItems.Count);
            var folder = vm.AllItems.FirstOrDefault(x => x.IsFolder);
            Assert.NotNull(folder);
            Assert.Equal("New Folder", folder.Name);
            Assert.Equal(2, folder.FolderItems.Count);
            Assert.Contains(folder.FolderItems, x => x.Id == "apple");
            Assert.Contains(folder.FolderItems, x => x.Id == "apricot");
            Assert.Contains(vm.AllItems, x => x.Id == "banana");
        }

        [Fact]
        public async Task Test_SettingsReloadOnToggle()
        {
            var tempSettingsFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            var store = new MockSettingsStore(tempSettingsFile);

            // Initial settings
            var config = new LayoutConfig { Columns = 5, Rows = 4, IconSize = 90 };
            await store.SaveLayoutAsync(config);

            var apps = CreatePresetApps(10);
            var vm = new LaunchpadViewModel(new MockAppScanner(apps), store, new SearchEngine());
            await vm.InitializeAsync();

            vm.Show();
            Assert.Equal(5, vm.Columns);
            Assert.Equal(4, vm.Rows);
            Assert.Equal(90, vm.IconSize);

            // Dismiss launcher
            vm.Hide();

            // Modify configuration on disk
            config.Columns = 3;
            config.Rows = 3;
            config.IconSize = 110;
            await store.SaveLayoutAsync(config);

            // Toggle back open: initialize again to simulate reload on open
            await vm.InitializeAsync();
            vm.Show();

            // Verify settings are applied dynamically
            Assert.Equal(3, vm.Columns);
            Assert.Equal(3, vm.Rows);
            Assert.Equal(110, vm.IconSize);

            // Clean up
            if (File.Exists(tempSettingsFile))
            {
                File.Delete(tempSettingsFile);
            }
        }

        [Fact]
        public async Task Test_MathSearchInsideFolder()
        {
            var apps = CreatePresetApps(3);
            var folder = new AppItem { Id = "f1", Name = "Folder 1", IsFolder = true, FolderItems = apps };
            var vm = new LaunchpadViewModel(new MockAppScanner(new List<AppItem> { folder }), new InMemorySettingsStore(), new SearchEngine());
            await vm.InitializeAsync();

            vm.Show();
            vm.OpenFolderOverlay(folder);
            Assert.Equal(folder, vm.OpenFolder);

            // Typing a math expression hides the folder overlay in the UI flow and updates search
            vm.CloseFolderOverlay();
            vm.UpdateSearch("2 * (5 + 5)");

            Assert.Null(vm.OpenFolder);
            Assert.True(vm.IsMathVisible);
            Assert.Equal("20", vm.MathResult);

            // Pressing Escape clears search
            vm.PressEscape();
            Assert.False(vm.IsMathVisible);
            Assert.Equal(string.Empty, vm.SearchText);
        }

        // Tier 4: Real-World Application Workloads Tests

        [Fact]
        public async Task Test_StandardUserSession()
        {
            // 25 apps, columns=5, rows=2 -> Page size=10. Total 3 pages.
            var apps = CreatePresetApps(25);
            var scanner = new MockAppScanner(apps);
            var settings = new InMemorySettingsStore { Config = new LayoutConfig { Columns = 5, Rows = 2 } };
            var vm = new LaunchpadViewModel(scanner, settings, new SearchEngine());

            // Initialize / scan start menu
            await vm.InitializeAsync();
            Assert.Equal(25, vm.AllItems.Count);

            vm.Show();
            Assert.True(vm.IsVisible);

            // Navigate pages
            Assert.Equal(0, vm.CurrentPageIndex);
            vm.NextPage();
            Assert.Equal(1, vm.CurrentPageIndex);
            vm.NextPage();
            Assert.Equal(2, vm.CurrentPageIndex);

            // Select an app on page 2 (index 2 on page 2 -> 23rd app)
            vm.SelectedItemIndex = 2;
            var expectedApp = vm.GetItemsOnPage(2)[2];

            // Press enter to launch
            vm.PressEnter();

            Assert.Equal(expectedApp.Id, vm.LaunchedApp?.Id);
            Assert.False(vm.IsVisible);
        }

        [Fact]
        public async Task Test_OrganizerWorkflow()
        {
            var app1 = new AppItem { Id = "app1", Name = "App 1" };
            var app2 = new AppItem { Id = "app2", Name = "App 2" };
            var app3 = new AppItem { Id = "app3", Name = "App 3" };
            var app4 = new AppItem { Id = "app4", Name = "App 4" };

            var vm = new LaunchpadViewModel(new MockAppScanner(new List<AppItem> { app1, app2, app3, app4 }), new InMemorySettingsStore(), new SearchEngine());
            await vm.InitializeAsync();

            // 1. Group two apps to create folder
            vm.CreateFolder(app2, app1);
            Assert.Equal(3, vm.AllItems.Count); // folder, app3, app4
            var folder = vm.AllItems.FirstOrDefault(x => x.IsFolder);
            Assert.NotNull(folder);
            Assert.Equal(2, folder.FolderItems.Count);

            // 2. Open folder
            vm.OpenFolderOverlay(folder);
            Assert.Equal(folder, vm.OpenFolder);

            // 3. Rename folder
            vm.RenameFolder("Office Tools");
            Assert.Equal("Office Tools", folder.Name);

            // 4. Drag third app into folder
            vm.AllItems.Remove(app3);
            folder.FolderItems.Add(app3);
            vm.UpdateSearch(string.Empty); // refresh layout
            Assert.Equal(3, folder.FolderItems.Count);
            Assert.Equal(2, vm.AllItems.Count); // folder, app4

            // 5. Drag an app out
            vm.DragAppOutOfFolder(app1);
            Assert.Equal(2, folder.FolderItems.Count);
            Assert.Equal(3, vm.AllItems.Count); // folder, app4, app1
            Assert.Equal(folder, vm.OpenFolder); // overlay still open

            // 6. Drag another app out (only 1 remaining, dissolves folder)
            vm.DragAppOutOfFolder(app2);
            Assert.Null(vm.OpenFolder); // Closed
            Assert.DoesNotContain(folder, vm.AllItems); // dissolved
            Assert.Equal(4, vm.AllItems.Count); // all apps restored individually
            Assert.Equal(4, vm.DisplayedItems.Count);
        }

        [Fact]
        public async Task Test_FolderRenameEmptyOrWhitespaceIgnored()
        {
            var folder = new AppItem { Id = "f1", Name = "Original Name", IsFolder = true, FolderItems = CreatePresetApps(2) };
            var vm = new LaunchpadViewModel(new MockAppScanner(new List<AppItem> { folder }), new InMemorySettingsStore(), new SearchEngine());
            await vm.InitializeAsync();

            vm.OpenFolderOverlay(folder);

            // Attempt rename to null
            vm.RenameFolder(null!);
            Assert.Equal("Original Name", folder.Name);

            // Attempt rename to empty
            vm.RenameFolder("");
            Assert.Equal("Original Name", folder.Name);

            // Attempt rename to whitespace
            vm.RenameFolder("   ");
            Assert.Equal("Original Name", folder.Name);
        }

        [Fact]
        public async Task Test_KeyboardNavAndSearchFlow()
        {
            var apps = CreatePresetApps(5);
            var vm = new LaunchpadViewModel(new MockAppScanner(apps), new InMemorySettingsStore(), new SearchEngine());
            await vm.InitializeAsync();

            // Navigate using keyboard
            vm.Show();
            Assert.Equal(0, vm.SelectedItemIndex);
            vm.MoveFocusRight();
            Assert.Equal(1, vm.SelectedItemIndex);

            // Type "Lock" system action keyword: the action is armed, not executed
            vm.UpdateSearch("Lock");
            Assert.Equal(SystemActionType.Lock, vm.PendingSystemAction);
            Assert.Equal(SystemActionType.None, vm.ExecutedSystemAction);
            Assert.True(vm.IsVisible);

            // Explicit Enter executes it
            vm.PressEnter();
            Assert.Equal(SystemActionType.Lock, vm.ExecutedSystemAction);
            Assert.False(vm.IsVisible);
            vm.ClearActionResults();

            // Toggle launcher back
            vm.Show();
            Assert.True(vm.IsVisible);
            vm.UpdateSearch(string.Empty); // clear action search

            // Type search query
            vm.UpdateSearch("App 3");
            Assert.Single(vm.DisplayedItems);

            // Navigate filtered results and launch
            Assert.Equal(0, vm.SelectedItemIndex);
            vm.PressEnter();
            Assert.Equal("app_3", vm.LaunchedApp?.Id);
            Assert.False(vm.IsVisible);
        }

        [Fact]
        public async Task Test_ConfigurationLoadSaveWorkflow()
        {
            var tempSettingsFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            var store = new MockSettingsStore(tempSettingsFile);

            var apps = CreatePresetApps(5); // app_0, app_1, app_2, app_3, app_4

            // Pre-existing layout
            var config = new LayoutConfig
            {
                Columns = 6,
                Rows = 4,
                IconSize = 75,
                Folders = new Dictionary<string, List<string>>
                {
                    { "FolderA", new List<string> { "app_0", "app_1" } },
                    { "FolderB", new List<string> { "app_2", "app_4" } }
                },
                PageOrder = new List<string> { "FolderA", "FolderB", "app_3" }
            };
            await store.SaveLayoutAsync(config);

            // Launch first VM
            var vm1 = new LaunchpadViewModel(new MockAppScanner(apps), store, new SearchEngine());
            await vm1.InitializeAsync();

            // Verify loaded locations and folders
            Assert.Equal(6, vm1.Columns);
            Assert.Equal(4, vm1.Rows);
            Assert.Equal(75, vm1.IconSize);
            Assert.Equal(3, vm1.AllItems.Count);

            var folderA = vm1.AllItems[0];
            var folderB = vm1.AllItems[1];
            var app3 = vm1.AllItems[2];

            Assert.True(folderA.IsFolder);
            Assert.Equal("FolderA", folderA.Name);
            Assert.Equal(2, folderA.FolderItems.Count);

            Assert.True(folderB.IsFolder);
            Assert.Equal("FolderB", folderB.Name);
            Assert.Equal(2, folderB.FolderItems.Count);

            Assert.False(app3.IsFolder);
            Assert.Equal("app_3", app3.Id);

            // Modify rows/columns layouts and save
            vm1.Columns = 8;
            vm1.Rows = 6;
            vm1.IconSize = 85;
            await vm1.SaveLayoutAsync();

            // Launch a fresh VM
            var vm2 = new LaunchpadViewModel(new MockAppScanner(apps), store, new SearchEngine());
            await vm2.InitializeAsync();

            // Verify identical config loaded
            Assert.Equal(8, vm2.Columns);
            Assert.Equal(6, vm2.Rows);
            Assert.Equal(85, vm2.IconSize);
            Assert.Equal(3, vm2.AllItems.Count);
            Assert.Equal("FolderA", vm2.AllItems[0].Id);
            Assert.Equal("FolderB", vm2.AllItems[1].Id);
            Assert.Equal("app_3", vm2.AllItems[2].Id);

            // Clean up
            if (File.Exists(tempSettingsFile))
            {
                File.Delete(tempSettingsFile);
            }
        }

        [Fact]
        public async Task Test_FolderNamePersistsAcrossSessions()
        {
            var tempSettingsFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            var store = new MockSettingsStore(tempSettingsFile);
            var apps = CreatePresetApps(3);

            var vm1 = new LaunchpadViewModel(new MockAppScanner(apps), store, new SearchEngine());
            await vm1.InitializeAsync();

            // Group two apps, rename the folder, persist
            vm1.CreateFolder(vm1.AllItems[0], vm1.AllItems[1]);
            var folder = vm1.AllItems.First(x => x.IsFolder);
            vm1.OpenFolderOverlay(folder);
            vm1.RenameFolder("Productivity");
            await vm1.SaveLayoutAsync();

            // A fresh session must restore the display name, not the folder id
            var vm2 = new LaunchpadViewModel(new MockAppScanner(apps), store, new SearchEngine());
            await vm2.InitializeAsync();
            var restored = vm2.AllItems.First(x => x.IsFolder);
            Assert.Equal(folder.Id, restored.Id);
            Assert.Equal("Productivity", restored.Name);

            if (File.Exists(tempSettingsFile))
            {
                File.Delete(tempSettingsFile);
            }
        }

        [Fact]
        public async Task Test_EdgeCaseWorkflowSequence()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var tempSettingsFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            
            var store = new MockSettingsStore(tempSettingsFile);
            var scanner = new MockAppScanner(tempDir);

            // 1. Empty scan directory, save default settings
            var vm = new LaunchpadViewModel(scanner, store, new SearchEngine());
            await vm.InitializeAsync();
            Assert.Empty(vm.AllItems);
            
            vm.Columns = 5;
            vm.Rows = 2; // Page size = 10
            await vm.SaveLayoutAsync();

            // 2. Dynamically add shortcut (.lnk) files
            for (int i = 1; i <= 25; i++)
            {
                File.WriteAllText(Path.Combine(tempDir, $"Shortcut_{i}.lnk"), "");
            }

            // 3. Trigger rescan
            await vm.InitializeAsync();
            Assert.Equal(25, vm.AllItems.Count);

            // 4. Drag one shortcut to page 3 (index 20+ overall)
            var movingItem = vm.AllItems[0];
            vm.AllItems.RemoveAt(0);
            vm.AllItems.Add(movingItem);
            vm.UpdateSearch(string.Empty);

            // Verify it's on page 3 (pageIndex = 2)
            var page3Items = vm.GetItemsOnPage(2);
            Assert.Contains(movingItem, page3Items);

            // 5. Save layout
            await vm.SaveLayoutAsync();

            // 6. Query math expression
            vm.UpdateSearch("((2 + 2) * 5) / 2");
            Assert.True(vm.IsMathVisible);
            Assert.Equal("10", vm.MathResult);

            // Clean up
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
            if (File.Exists(tempSettingsFile))
            {
                File.Delete(tempSettingsFile);
            }
        }
    }
}
