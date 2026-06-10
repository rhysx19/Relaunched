# Classic Launchpad

A lightweight, native, and highly customizable application launcher overlay for **macOS** and **Windows**. It brings the clean, full-screen macOS Launchpad experience to both operating systems, complete with elegant desktop backdrop blurs, animated paging grids, glassmorphic folder overlays, power-user math calculations, and full keyboard navigation.

<p align="center">
  <img src="AppIcon.icns" width="128" height="128" alt="Classic Launchpad Icon" />
</p>

---

## 💻 Platforms Supported

1. **macOS**: Built natively using **SwiftUI** & **AppKit** (targeting macOS 10.15+).
2. **Windows**: Built natively using **C#**, **WinUI 3**, and the **Windows App SDK** (targeting Windows 10 & 11).

---

## ✨ Features

- 🚀 **Global Toggle & Keyboard Navigation**:
  - **macOS**: Global hotkey `Option + Space` (configurable).
  - **Windows**: Global hotkey `Ctrl + Alt + Space` (registered via Win32 subclasses; plain `Alt + Space` is reserved by Windows for the system window menu).
  - Full keyboard controls: navigate cells using arrow keys (with selection halos), change pages using `Page Up / Down`, and press `Enter` to open apps/folders.
- 🧠 **Smart Search & Suggestions** (macOS):
  - Fuzzy matching with initials support — type `vsc` to find Visual Studio Code.
  - Results ranked by how often and how recently you launch each app.
  - An optional Suggestions row surfaces your most-used apps above the grid.
- 🔄 **Live App List** (macOS): watches `/Applications` (and friends) so newly installed or deleted apps appear without relaunching.
- 📱 **Premium Glassmorphic Folders & Drag-to-Remove**:
  - Drag apps onto each other to group them into folders with mini-grid previews.
  - Opening a folder blurs, dims, and scales down the background apps grid.
  - Change folder titles inline—commits automatically on `Enter` or clicking away.
  - Drag an app out of the folder box onto the blurred background backdrop to remove and reflow the layout.
  - Drag an icon to the screen edge to flip between pages, just like native Launchpad (macOS).
- 🛠 **App Management** (macOS): right-click any app for Get Info (version, bundle ID, location, usage), Show in Finder, Hide, or Move to Trash.
- 📐 **Responsive Paginated Grids**:
  - Configure columns, rows, and icon dimensions in real-time.
  - Apps animate smoothly using spring physics during layout shifts.
  - Safe-scaling rules shrink layout dimensions on smaller viewports to prevent screen overflows.
- 🔍 **Real-Time Search, Math Parser & System Actions**:
  - Start typing anywhere on the screen to immediately search and filter applications.
  - **Calculator**: Type simple mathematical expressions (e.g. `12 * (4 + 6)`) to solve instantly. Click the action card to copy the result.
  - **System Control**: Execute commands like `Lock`, `Sleep`, `Restart`, or `Shutdown` directly from the search bar.
- ⚙️ **Integrated Settings**:
  - Modify rows, columns, icon sizes, background dim, and icon-label visibility instantly with changes updating in real-time.
  - Control Launch at Login, daemon status bar controls, taskbar/dock presence, the global hotkey, and check for updates in-app (macOS).
- 🔒 **Dock & Taskbar Hiding**:
  - Hides the macOS Dock/MenuBar or Windows Taskbar (on all active screens) during visibility to prevent mouse hover conflicts.
  - Employs fail-safe process hooks to restore system bars on focus loss or app crashes.

---

## 📂 Project Structure

- **`ClassicLaunchpad.sln`**: The central .NET solution for Windows.
  - **`ClassicLaunchpad`**: WinUI 3 Desktop Application handling presentation, Acrylic backdrop blurs, and global hooks.
  - **`ClassicLaunchpad.Core`**: Shared C# library managing layout state machines, COM shortcut scanners, search engines, system commands, and local settings stores.
  - **`ClassicLaunchpad.Tests`**: xUnit tests containing 90 headless E2E verification test cases.
- **macOS SwiftUI Source**:
  - **`main.swift`**: Bootstrapper handling AppKit window states, Dock promotions, and global hooks.
  - **`AppScanner.swift`**: Scans application directories and resolves bundle icon assets.
  - **`LaunchpadView.swift`**: SwiftUI view representing pages, drag-reordering, folders, search, and the settings card.
  - **`build.sh`**: Codesigning wrapper to build a clean `.app` bundle from the Swift source.

---

## 🛠️ Getting Started (Windows - WinUI 3)

### Prerequisites
- Windows 10 Version 1809 (Build 17763) or later.
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
- Windows App SDK runtime dependencies (handled automatically by MSBuild).

### Build & Publish
To compile a standalone, self-contained publish bundle:
```powershell
dotnet publish ClassicLaunchpad/ClassicLaunchpad.csproj -c Release -r win-x64 --self-contained
```
The compiled output is located at:
`ClassicLaunchpad/bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/ClassicLaunchpad.exe`

### Running the Test Suite
The C# test suite runs headlessly and compiles on **Windows, macOS, and Linux**:
```bash
./run_tests.sh
```

---

## 🍎 Getting Started (macOS - SwiftUI)

### Install a Release Build
Download `Launchpad-Classic-macOS.zip` from the [Releases page](https://github.com/rhysx1/Classic-Launchpad/releases), unzip, and drag `Launchpad Classic.app` into `/Applications`. Since release builds are ad-hoc signed, clear Gatekeeper once with:
```bash
xattr -cr "/Applications/Launchpad Classic.app"
```
A Homebrew cask template lives at `packaging/homebrew/launchpad-classic.rb` for tap-based installs.

### Prerequisites (building from source)
- macOS 10.15 (Catalina) or later (macOS 13+ for the Launch at Login toggle).
- Command-line build tools (install via `xcode-select --install`).

### Build & Run
1. Open terminal and navigate to the project directory:
   ```bash
   cd MacOS_Launchpad
   ```
2. Build and sign the application bundle:
   ```bash
   ./build.sh
   ```
3. Run the application:
   ```bash
   open "Launchpad Classic.app"
   ```

### Gatekeeper Workaround for Shared Bundles
To share the built bundle with friends, compress it first to maintain attributes:
```bash
zip -r -y Launchpad_Classic.zip "Launchpad Classic.app"
```
On their machine, they can clear Gatekeeper alerts using terminal:
```bash
xattr -cr /path/to/Launchpad\ Classic.app
```

---

## ⌨️ Controls & Shortcuts

| Action | macOS Shortcut | Windows Shortcut |
| :--- | :--- | :--- |
| **Toggle Launcher** | `Option + Space` (configurable) | `Ctrl + Alt + Space` (global hook) |
| **Selection Highlight** | `Arrow Keys` | `Arrow Keys` |
| **Paging** | `Page Up / Down` | `Page Up / Down` |
| **Select / Launch** | `Enter` | `Enter` |
| **Dismiss / Clear** | `Escape` | `Escape` |
| **Swipe Navigation** | Trackpad swiping | Trackpad swiping |
| **Scroll Navigation** | Scroll wheel ticking | Scroll wheel ticking |

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
