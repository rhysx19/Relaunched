# E2E Test Infra: Classic Launchpad for Windows

## Test Philosophy
- Opaque-box, requirement-driven. Tests verify the behavior of the application using simulated user inputs, mock filesystem states, and VM assertion channels, without depending on internal implementation details.
- Methodology: Category-Partition + BVA + Pairwise + Workload Testing.

## Feature Inventory
| # | Feature | Source (requirement) | Tier 1 | Tier 2 | Tier 3 |
|---|---------|---------------------|:------:|:------:|:------:|
| 1 | Fluent Layout & Acrylic Blur | R1 | 5 | 5 | ✓ |
| 2 | Start Menu Scanner & Persistence | R2 | 5 | 5 | ✓ |
| 3 | Glassmorphic Folders & Drag-out | R3 | 5 | 5 | ✓ |
| 4 | Hotkey & Keyboard Navigation | R4 | 5 | 5 | ✓ |
| 5 | Real-Time Search & System Actions | R5 | 5 | 5 | ✓ |

## Test Architecture
- Test runner: `dotnet test` on `ClassicLaunchpad.Tests` project.
- Test case format: xUnit theory/fact tests.
- UI simulation/headless tests will use a UI Controller/ViewModel representation to simulate inputs (key presses, drag actions) and verify UI state responses.
- Directory layout:
  - `ClassicLaunchpad.Tests/`
    - `ScannerTests.cs` (R2 scanner & persistence tests)
    - `LayoutTests.cs` (R1/R3/R5 grid layout, dimensions, paging, search filtering, math/system command execution)
    - `UIHeadlessTests.cs` (R3/R4/R5 simulated user interaction and navigation flow tests)
    - `IntegrationTests.cs` (Tier 3 combinatorial & Tier 4 real-world workloads)

## Real-World Application Scenarios (Tier 4)
| # | Scenario | Features Exercised | Complexity |
|---|----------|--------------------|------------|
| 1 | Standard User Session | Scan apps, scroll pages, open folder, launch app | Medium |
| 2 | Search and Execute | Type search, calculate math, run system command | Medium |
| 3 | Organizer Workflow | Create folder, rename folder, drag-out app, change layout grid settings | High |
| 4 | Hotkey Toggle & Navigation | Open launcher, select app via keyboard, dismiss, toggle via hotkey, select and launch | High |
| 5 | Startup and Recovery | Start empty, scan apps, load existing persistent folder layout, verify correctness | High |

## Coverage Thresholds
- Tier 1: ≥5 per feature (Total: 25)
- Tier 2: ≥5 per feature (Total: 25)
- Tier 3: pairwise coverage of major feature interactions (Total: 5)
- Tier 4: ≥5 realistic application scenarios (Total: 5)
- Total E2E Tests: 60 cases
