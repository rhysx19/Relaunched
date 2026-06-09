# Original User Request

## Initial Request — 2026-06-09T14:26:12+01:00

# Teamwork Project Prompt: Classic Launchpad for Windows (C# / WinUI 3)

Port the macOS Classic Launchpad application to Windows using C# and WinUI 3 (Windows App SDK), creating a lightweight, full-screen transparent application launcher featuring desktop Acrylic vibrancy blurs, paginated grids, drag-and-drop folders, keyboard navigation, and settings configurations.

Working directory: ~/teamwork_projects/classic_launchpad_windows
Integrity mode: development

## Requirements

### R1. WinUI 3 Fluent Layout & Acrylic Blur
- Create a full-screen, borderless WinUI 3 window that behaves as a launcher overlay.
- Configure window attributes to use native Windows 11 **Acrylic** (transparent backdrop tint) background material.
- Implement a paginated horizontal apps grid centered on the screen, matching macOS spacing, that dynamically adjusts column/row spacing and scales icons to fit different display heights.
- Hide the Windows Taskbar temporarily while the launcher is active, restoring it upon dismissal.

### R2. Start Menu Application Scanner
- Scan global (`C:\ProgramData\Microsoft\Windows\Start Menu\Programs`) and user (`C:\Users\<User>\AppData\Roaming\Microsoft\Windows\Start Menu\Programs`) Start Menu shortcut directories (`.lnk` files) to populate the grid.
- Extract high-resolution application icons using Shell / Win32 APIs and launch target executables when cell items are double-clicked or selected via keyboard navigation.
- Persist custom icon layouts, page order, and folder configurations in local application data storage (JSON/XML).

### R3. Premium Glassmorphic Folders & Drag-to-Remove
- Group multiple application icons into folders. Represent folders in the main grid with a mini-grid of nested application icons.
- Opening a folder overlay displays a centered glassmorphic folder container containing the apps. The background grid must scale down, dim, and blur dynamically to focus on the folder.
- Support native title renaming on the folder overlay that commits on pressing Enter or losing focus (clicking away), and rejects empty inputs.
- Enable removing apps from folders via both context menu actions and drag-and-drop: dragging an icon out of the folder box onto the blurred background backdrop removes it and reflows the grid.

### R4. Global Hotkey & Keyboard Navigation
- Register a global hotkey (defaulting to `Ctrl + Space` or `Alt + Space`) using the Win32 `RegisterHotKey` API to toggle launcher visibility.
- Support full keyboard control inside the active grid: arrow keys move a visible selection border, page keys shift pages, and pressing Enter launches the app or expands the folder.

### R5. Real-Time Search & System Actions
- Display a centered search bar at the bottom. Typing alphanumeric keys anywhere on the grid must focus search and filter the grid instantly.
- Parse simple math expressions (e.g. `12 * 8`) in search to show results in a quick action card.
- Parse system keywords (e.g. `Lock`, `Sleep`, `Restart`, `Shutdown`) in search to execute the corresponding system actions.

## Acceptance Criteria

### Grid Layout and Settings
- [ ] Launcher window runs full-screen, borderless, with transparent Acrylic backdrop.
- [ ] Grid dimensions (rows/columns) and icon sizes can be configured via settings, reflowing with a smooth animation.
- [ ] Selecting an app launch target opens the executable and dismisses the launcher.

### Application Scanner & Persistence
- [ ] Start Menu shortcut items are scanned and displayed.
- [ ] Target icons are extracted cleanly and look crisp.
- [ ] Folder configurations and custom page order persist across app relaunches.

### Folders and Drag-to-Remove
- [ ] Dragging an app onto another creates a folder, displaying a mini-grid icon.
- [ ] Expanded folders dim and blur the background app grid.
- [ ] Dragging an app out of the folder box onto the backdrop removes the app from the folder and reflows the main grid.
- [ ] Renaming folder title commits on Enter/focus loss and rejects empty inputs.

### Hotkeys and Keyboard
- [ ] Launcher toggles correctly on the global hotkey command.
- [ ] Arrow keys navigate focus correctly between cells, moving across page boundaries.
- [ ] Escape key clears search, closes folders, or dismisses launcher.
