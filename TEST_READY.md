# E2E Test Suite Ready

## Test Runner
- Command: `bash ./run_tests.sh` (or `dotnet test ClassicLaunchpad.Tests/ClassicLaunchpad.Tests.csproj`)
- Expected: all tests pass with exit code 0
- Runs cross-platform (Windows, macOS, Linux); CI runs the suite on `windows-latest` and `ubuntu-latest`.

## Coverage Summary
| File | Count | Description |
|------|------:|-------------|
| `UIHeadlessTests.cs` | 29 | Headless ViewModel simulation: paging, keyboard nav, folders, search, system-action arming/cancel |
| `SearchEngineTests.cs` | 23 | Search filtering, math evaluator (precision, parentheses, depth cap), system-action keywords |
| `SettingsTests.cs` | 14 | Layout persistence: roundtrips, corrupt JSON recovery, atomic-write failure propagation |
| `IntegrationTests.cs` | 12 | Full user journeys: session flows, organizer workflow, config load/save, folder-name persistence |
| `AppScannerTests.cs` | 8 | Real scanner in simulated mode: filtering, dedup, subdirectories, id/path mapping |
| `SystemCommandExecutorTests.cs` | 4 | Per-OS command mappings via the process-start hook (no real side effects) |
| **Total** | **90** | |

## Feature Checklist
| Feature | Covered by |
|---------|------------|
| Fluent Layout & Grid Scaling | UIHeadless, Integration |
| Start Menu Scanner & Persistence | AppScanner, Settings, Integration |
| Glassmorphic Folders & Rename/Drag-out | UIHeadless, Integration |
| Hotkeys & Keyboard Navigation | UIHeadless, Integration |
| Real-Time Search, Math & System Actions | SearchEngine, UIHeadless, SystemCommandExecutor |
