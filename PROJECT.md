# Project: Classic Launchpad for Windows

## Architecture
The Classic Launchpad for Windows is ported from the macOS SwiftUI/AppKit version. It is split into three main components to ensure cross-platform testability and separation of logic from UI:
1. **ClassicLaunchpad.Core**: Class Library containing all non-UI logic:
   - Start Menu scanner and shortcut (`.lnk`) parsing.
   - Search filtering and Mathematical evaluation engine.
   - Layout model, configuration state, and JSON persistence.
   - Platform-independent system command parsing.
2. **ClassicLaunchpad.Tests**: xUnit Test suite targeting the Core library. This can compile and run on macOS, providing E2E verification of scanner, search, math, and layout logic.
3. **ClassicLaunchpad**: WinUI 3 Desktop App project handling the UI, Acrylic presentation, input events, taskbar hiding/restoring, and Win32 hotkeys.

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| M1 | Solution Setup & Architecture | Setup `.sln` and core project folders, verify compilation | None | DONE |
| M2 | Core App Scanner & Systems Executor | Implement scanning logic, math parsing, system command executor | M1 | DONE |
| M3 | Core Layout Persistence | Implement layout save/load in JSON format | M2 | DONE |
| M4 | WinUI 3 Shell & Window | Create borderless Acrylic window, taskbar hiding, hotkey registration | M1 | DONE |
| M5 | WinUI 3 Grid, Paging & Keyboard | Paginated grid view, keyboard nav, hover halos | M4 | DONE |
| M6 | WinUI 3 Folders & Drag-to-Remove | Group apps, folder overlay blur, drag-out removal | M3, M5 | DONE |
| M7 | E2E Verification & Hardening | Run E2E test suite, pass 100% test cases, adversarial hardening | M2, M3, M6 | DONE |

## Interface Contracts
### Start Menu App Scanner Contract
- `IAppScanner`
  - `Task<List<AppItem>> ScanApplicationsAsync()`
- `AppItem`
  - `string Id` (unique identifier)
  - `string Name` (display name)
  - `string TargetPath` (executable path)
  - `string IconPath` (extracted icon resource path)
  - `bool IsFolder`
  - `List<AppItem> FolderItems` (nested apps if folder)

### Configuration Persistence Contract
- `ISettingsStore`
  - `Task SaveLayoutAsync(LayoutConfig config)`
  - `Task<LayoutConfig> LoadLayoutAsync()`
- `LayoutConfig`
  - `int Columns`
  - `int Rows`
  - `int IconSize`
  - `List<string> PageOrder` (ordered item IDs)
  - `Dictionary<string, List<string>> Folders` (folder ID to list of item IDs)

### Search & Math Parser Contract
- `ISearchEngine`
  - `SearchResult Query(string input, List<AppItem> pool)`
- `SearchResult`
  - `bool IsMathExpression`
  - `string MathResult`
  - `bool IsSystemAction`
  - `SystemActionType SystemAction`
  - `List<AppItem> FilteredApps`
