# Classic Launchpad

A lightweight, native, and highly customizable application launcher overlay for **macOS**. It brings back the clean, full-screen classic Launchpad experience, complete with elegant desktop backdrop blurs, animated paging grids, glassmorphic folder overlays, power-user math calculations, and full keyboard navigation.

<p align="center">
  <img src="AppIcon.icns" width="128" height="128" alt="Classic Launchpad Icon" />
</p>

Built natively with **SwiftUI** & **AppKit** (targeting macOS 10.15+).

---

## ✨ Features

- 🚀 **Global Toggle & Keyboard Navigation**:
  - Global hotkey `Option + Space` (configurable in Settings).
  - Full keyboard controls: navigate cells using arrow keys (with selection halos), change pages using `Page Up / Down`, and press `Enter` to open apps/folders.
- 🤏 **Trackpad Pinch Gestures**: pinch in with thumb + three fingers anywhere to open Launchpad, spread them apart to close — exactly like classic macOS. Reads raw trackpad touches (no Accessibility permission needed), so it never misfires on regular two-finger zooming. Toggleable in Settings.
- 🧠 **Smart Search & Suggestions**:
  - Fuzzy matching with initials support — type `vsc` to find Visual Studio Code.
  - Results ranked by how often and how recently you launch each app.
  - An optional Suggestions row surfaces your most-used apps above the grid.
- 🔄 **Live App List**: watches `/Applications` (and friends) so newly installed or deleted apps appear without relaunching.
- 📱 **Premium Glassmorphic Folders & Drag-to-Remove**:
  - Drag apps onto each other to group them into folders with mini-grid previews.
  - Opening a folder blurs, dims, and scales down the background apps grid.
  - Change folder titles inline—commits automatically on `Enter` or clicking away.
  - Drag an app out of the folder box onto the blurred background backdrop to remove and reflow the layout.
  - Drag an icon to the screen edge to flip between pages, just like native Launchpad.
- 🛠 **App Management**: right-click any app for Get Info (version, bundle ID, location, usage), Show in Finder, Hide, or Move to Trash.
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
  - Control Launch at Login, daemon status bar controls, Dock presence, the global hotkey, trackpad pinch gestures, and check for updates in-app.
- 🔒 **Dock & Menu Bar Hiding**:
  - Hides the macOS Dock and Menu Bar while the launcher is active to prevent mouse hover conflicts.
  - Employs fail-safe process hooks to restore system bars on focus loss or app crashes.

---

## 📂 Project Structure

- **`main.swift`**: Bootstrapper handling AppKit window states, Dock promotions, the global hotkey, and gesture wiring.
- **`AppScanner.swift`**: Scans application directories, resolves bundle icon assets, fuzzy/frecency search, and file-system watching.
- **`TrackpadGestures.swift`**: Raw multitouch reader powering the classic pinch-to-open / spread-to-close gestures.
- **`LaunchpadView.swift`**: SwiftUI view representing pages, drag-reordering, folders, search, and the settings card.
- **`VisualEffectView.swift`**: NSVisualEffectView bridge for the blurred backdrop.
- **`build.sh`**: Codesigning wrapper to build a clean `.app` bundle from the Swift source.

---

## 🍎 Getting Started

### Install a Release Build
Download `Launchpad-Classic-macOS.zip` from the [Releases page](https://github.com/rhysx19/Classic-Launchpad/releases), unzip, and drag `Launchpad Classic.app` into `/Applications`. Since release builds are ad-hoc signed, clear Gatekeeper once with:
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

| Action | Shortcut |
| :--- | :--- |
| **Toggle Launcher** | `Option + Space` (configurable) |
| **Pinch Open / Close** | Thumb + 3 finger pinch / spread (trackpad) |
| **Selection Highlight** | `Arrow Keys` |
| **Paging** | `Page Up / Down` |
| **Select / Launch** | `Enter` |
| **Dismiss / Clear** | `Escape` |
| **Swipe Navigation** | Trackpad swiping |
| **Scroll Navigation** | Scroll wheel ticking |

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
