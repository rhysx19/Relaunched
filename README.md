# Classic macOS Launchpad

A lightweight, native, and highly customizable alternative to the macOS Launchpad. Built with **SwiftUI** & **AppKit**, it delivers smooth spring animations, deep glassmorphic styling, and power-user features like spotlight calculations and system actions.

<p align="center">
  <img src="AppIcon.icns" width="128" height="128" alt="Launchpad Classic Icon" />
</p>

---

## Key Features

*   🚀 **Global Keyboard Toggle & Navigation**:
    *   Instantly toggle the Launchpad overlay using `Option + Space`.
    *   Full keyboard control: select apps with arrow keys (showing a blue halo glow), navigate pages, and press `Enter` to launch.
*   📱 **Premium Glassmorphic Folders & Drag-to-Remove**:
    *   Group apps into folders with 32pt continuous rounded squircle styling and dark `hudWindow` blurs.
    *   *Depth-of-Field Focus*: Opening a folder automatically dims, scales, and blurs background apps.
    *   *Native Renaming*: Focus-stable AppKit `NSTextField` with inline capsule highlighting. Commits on `Enter` or click-away.
    *   *Drag-to-Remove*: Drag any app out of the folder box onto the background backdrop to remove it.
*   📐 **Fully Animated Grid Reflows**:
    *   Real-time layout recalculations animate with smooth spring physics when adjusting column/row sizes or app icon dimensions.
    *   *Responsive Scaling*: Layout automatically shrinks and scales elements to fit safely within smaller laptop viewports.
*   🔍 **Spotlight Math & System Actions**:
    *   Typing directly on the home screen automatically triggers search.
    *   *Quick Calculations*: Type expressions (e.g. `2 + 2 * 3`) to instantly evaluate and press `Enter` to copy the result.
    *   *System Controls*: Type system actions (e.g. `Lock`, `Sleep`, `Restart`, `Shutdown`) and press `Enter` to execute.
*   ⚙️ **Integrated Settings Card**:
    *   Adjust columns (4-10), rows (3-7), and icon sizes (60pt-120pt) in real-time.
    *   Toggle background daemon running, Menu Bar status items, and Dock icon presence.
*   🔒 **Dock & Hover Interception**:
    *   Hides the macOS Dock and Menu Bar when open to block mouse hover conflicts and window click-throughs.
    *   Asynchronously scans for new applications on launch with a shimmering loading skeleton.

---

## Getting Started

### Prerequisites

*   A Mac running **macOS 10.15 (Catalina)** or later.
*   Command-line tools (installed automatically with Xcode or by running `xcode-select --install`).

### Build and Run

1.  Clone this repository to your local drive.
2.  Open Terminal and navigate to the project directory:
    ```bash
    cd MacOS_Launchpad
    ```
3.  Compile and code-sign the application:
    ```bash
    ./build.sh
    ```
4.  Launch the application:
    ```bash
    open "Launchpad Classic.app"
    ```

---

## Keyboard Controls & Gestures

| Key / Gesture | Action |
| :--- | :--- |
| `Option + Space` | Toggle Launchpad Classic (global hotkey) |
| `Arrow Keys` | Move blue focus border selection |
| `Page Up / Down` | Switch pages |
| `Enter` | Launch selected app or expand folder |
| `Escape` | Reset search query / close expanded folder / close Launchpad |
| `Two-finger Swipe` | Swipe left/right on trackpad to slide pages |
| `Scroll Wheel` | Scroll on mouse wheel to switch pages |

---

## App Sharing & Gatekeeper (Unidentified Developer)

Since this app is ad-hoc signed locally, sharing the raw app bundle directly with other users will trigger macOS Gatekeeper blocks.

### Packaging
To share with friends, package the app folder into a ZIP archive to preserve execution permissions and bundle structures:
```bash
zip -r -y Launchpad_Classic.zip "Launchpad Classic.app"
```

### Bypassing Gatekeeper on Other Macs
When your friends download and extract the app, they can open it by:
1.  **Right-clicking (or Control-clicking)** the extracted `Launchpad Classic.app` in Finder, choosing **Open**, and then clicking **Open** in the confirmation dialog.
2.  Or by opening Terminal and clearing the quarantine quarantine attribute:
    ```bash
    xattr -cr /path/to/Launchpad\ Classic.app
    ```

---

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
