import Cocoa
import SwiftUI
import Carbon

class LaunchpadWindow: NSWindow {
    override var canBecomeKey: Bool {
        return true
    }
    override var canBecomeMain: Bool {
        return true
    }
}

class HotkeyManager {
    private var hotKeyRef: EventHotKeyRef?
    private var eventHandlerRef: EventHandlerRef?
    private let onTrigger: () -> Void
    
    init(onTrigger: @escaping () -> Void) {
        self.onTrigger = onTrigger
        installHandler()
        registerFromPreferences()
    }
    
    private func installHandler() {
        var eventType = EventTypeSpec()
        eventType.eventClass = OSType(kEventClassKeyboard)
        eventType.eventKind = OSType(kEventHotKeyPressed)
        
        let handler: EventHandlerUPP = { (nextHandler, theEvent, userData) -> OSStatus in
            if let userData = userData {
                let manager = Unmanaged<HotkeyManager>.fromOpaque(userData).takeUnretainedValue()
                manager.onTrigger()
            }
            return noErr
        }
        
        let selfPointer = Unmanaged.passUnretained(self).toOpaque()
        let status = InstallEventHandler(GetApplicationEventTarget(), handler, 1, &eventType, selfPointer, &eventHandlerRef)
        if status != noErr {
            print("Failed to install application event handler: \(status)")
        }
    }
    
    /// (Re)registers the global hotkey from the user's saved preference
    /// (default: Option + Space). Safe to call repeatedly.
    func registerFromPreferences() {
        if let ref = hotKeyRef {
            UnregisterEventHotKey(ref)
            hotKeyRef = nil
        }
        
        let pref = HotkeyPreference.load()
        let hotKeyID = EventHotKeyID(signature: 0x4C485044, id: 1) // "LHPD"
        
        let registerStatus = RegisterEventHotKey(
            UInt32(pref.keyCode),
            pref.carbonModifiers,
            hotKeyID,
            GetApplicationEventTarget(),
            0,
            &hotKeyRef
        )
        
        if registerStatus != noErr {
            print("Failed to register Event HotKey \(pref.display): \(registerStatus)")
        }
    }
    
    deinit {
        if let ref = hotKeyRef {
            UnregisterEventHotKey(ref)
        }
        if let ref = eventHandlerRef {
            RemoveEventHandler(ref)
        }
    }
}

class AppDelegate: NSObject, NSApplicationDelegate, NSWindowDelegate {
    var window: NSWindow!
    var isSearchActive = false
    var hotkeyManager: HotkeyManager?
    var statusItem: NSStatusItem?
    var appDirectoryWatcher: AppDirectoryWatcher?
    /// True while an in-app modal (e.g. Move to Trash confirmation) is the key
    /// window, so losing key status doesn't auto-hide the launchpad.
    var isModalActive = false
    /// True while the settings hotkey recorder is capturing a shortcut; the
    /// global key monitor passes events through untouched during recording.
    var isHotkeyRecording = false
    
    func applicationDidFinishLaunching(_ notification: Notification) {
        // Retrieve the screen containing the mouse cursor for multi-monitor support
        let mouseLocation = NSEvent.mouseLocation
        let screens = NSScreen.screens
        let targetScreen = screens.first { NSMouseInRect(mouseLocation, $0.frame, false) } ?? NSScreen.main ?? screens.first
        let screenFrame = targetScreen?.frame ?? NSRect(x: 0, y: 0, width: 1920, height: 1080)
        
        // Listen to search status notifications
        NotificationCenter.default.addObserver(forName: NSNotification.Name("LaunchpadSearchActivated"), object: nil, queue: nil) { [weak self] _ in
            self?.isSearchActive = true
        }
        NotificationCenter.default.addObserver(forName: NSNotification.Name("LaunchpadSearchDeactivated"), object: nil, queue: nil) { [weak self] _ in
            self?.isSearchActive = false
        }
        
        // Close request listener from grid app launches
        NotificationCenter.default.addObserver(forName: NSNotification.Name("LaunchpadCloseRequested"), object: nil, queue: nil) { [weak self] _ in
            let persistentMode = UserDefaults.standard.object(forKey: "LaunchpadPersistentMode") as? Bool ?? true
            if persistentMode {
                self?.hideLaunchpad()
            } else {
                self?.dismissAndExit()
            }
        }
        

        
        // Reactive settings observer
        NotificationCenter.default.addObserver(forName: NSNotification.Name("LaunchpadSettingsChanged"), object: nil, queue: nil) { [weak self] _ in
            self?.applySettings()
        }
        
        // Modal suppression (alerts shown by the view layer become key windows)
        NotificationCenter.default.addObserver(forName: NSNotification.Name("LaunchpadModalBegan"), object: nil, queue: nil) { [weak self] _ in
            self?.isModalActive = true
        }
        NotificationCenter.default.addObserver(forName: NSNotification.Name("LaunchpadModalEnded"), object: nil, queue: nil) { [weak self] _ in
            self?.isModalActive = false
        }
        
        // Hotkey recorder capture window
        NotificationCenter.default.addObserver(forName: NSNotification.Name("LaunchpadHotkeyRecordingBegan"), object: nil, queue: nil) { [weak self] _ in
            self?.isHotkeyRecording = true
        }
        NotificationCenter.default.addObserver(forName: NSNotification.Name("LaunchpadHotkeyRecordingEnded"), object: nil, queue: nil) { [weak self] _ in
            self?.isHotkeyRecording = false
        }
        
        // Watch application directories so newly installed/removed apps appear
        // without relaunching the launchpad.
        appDirectoryWatcher = AppDirectoryWatcher()
        appDirectoryWatcher?.start()
        
        // Create a custom borderless overlay window
        window = LaunchpadWindow(
            contentRect: screenFrame,
            styleMask: [.borderless, .fullSizeContentView],
            backing: .buffered,
            defer: false
        )
        window.setFrame(screenFrame, display: true)
        
        window.isOpaque = false
        window.backgroundColor = .clear
        window.hasShadow = false
        window.level = .statusBar
        window.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .ignoresCycle]
        window.ignoresMouseEvents = false
        window.delegate = self
        
        // Host the SwiftUI view
        let contentView = LaunchpadView()
        window.contentView = NSHostingView(rootView: contentView)
        
        // Add local keyboard listener for Escape and keyboard controls
        NSEvent.addLocalMonitorForEvents(matching: .keyDown) { [weak self] event in
            guard let self = self else { return event }
            
            // While the hotkey recorder is capturing, let everything through
            // so its own monitor can consume the shortcut.
            if self.isHotkeyRecording {
                return event
            }
            
            if event.keyCode == 53 { // ESC
                NotificationCenter.default.post(
                    name: NSNotification.Name("LaunchpadEscapePressed"),
                    object: nil
                )
                return nil
            }
            
            if !self.isSearchActive {
                let navKeyCodes: Set<UInt16> = [123, 124, 125, 126, 36, 116, 121]
                if navKeyCodes.contains(event.keyCode) {
                    NotificationCenter.default.post(
                        name: NSNotification.Name("LaunchpadKeyDown"),
                        object: nil,
                        userInfo: ["keyCode": event.keyCode]
                    )
                    return nil
                }
            }
            
            if !self.isSearchActive, let characters = event.characters, !characters.isEmpty {
                let firstChar = characters.unicodeScalars.first!
                if CharacterSet.alphanumerics.contains(firstChar) || firstChar == " " {
                    NotificationCenter.default.post(
                        name: NSNotification.Name("LaunchpadAlphaNumericTyped"),
                        object: nil,
                        userInfo: ["characters": characters]
                    )
                    return nil
                }
            }
            
            return event
        }
        
        // Trackpad pinch gestures: thumb + three fingers pinched together opens,
        // spread apart closes — same as classic macOS Launchpad.
        TrackpadGestureManager.shared.onPinchIn = { [weak self] in
            guard let self = self, !self.window.isVisible else { return }
            self.showLaunchpad()
        }
        TrackpadGestureManager.shared.onSpreadOut = { [weak self] in
            guard let self = self, self.window.isVisible else { return }
            // Route through the view so the zoom-out animation plays first.
            NotificationCenter.default.post(name: NSNotification.Name("LaunchpadDismissRequested"), object: nil)
        }
        
        // Apply settings (daemon status item, global hotkey, activation policy)
        applySettings()
        
        // Make the window active via unified showLaunchpad method
        showLaunchpad()
    }
    
    func applySettings() {
        let persistentMode = UserDefaults.standard.object(forKey: "LaunchpadPersistentMode") as? Bool ?? true
        let showDockIcon = UserDefaults.standard.object(forKey: "LaunchpadShowDockIcon") as? Bool ?? true
        let showMenuBarIcon = UserDefaults.standard.object(forKey: "LaunchpadShowMenuBarIcon") as? Bool ?? true
        let pinchGestures = UserDefaults.standard.object(forKey: "LaunchpadPinchGestures") as? Bool ?? true
        
        DispatchQueue.main.async { [weak self] in
            guard let self = self else { return }
            
            // 1. Activation policy
            if persistentMode {
                if showDockIcon {
                    NSApp.setActivationPolicy(.regular)
                } else {
                    NSApp.setActivationPolicy(.accessory)
                }
            } else {
                NSApp.setActivationPolicy(.regular)
            }
            
            // 2. Hotkey Manager (re-register so hotkey preference changes apply live)
            if persistentMode {
                if self.hotkeyManager == nil {
                    self.hotkeyManager = HotkeyManager(onTrigger: { [weak self] in self?.toggleLaunchpad() })
                } else {
                    self.hotkeyManager?.registerFromPreferences()
                }
            } else {
                self.hotkeyManager = nil
            }
            
            // 3. Status Menu Item
            if persistentMode && showMenuBarIcon {
                if self.statusItem == nil {
                    self.setupStatusItem()
                }
            } else {
                if let item = self.statusItem {
                    NSStatusBar.system.removeStatusItem(item)
                    self.statusItem = nil
                }
            }
            
            // 4. Trackpad pinch gestures (only useful while resident in background)
            if persistentMode && pinchGestures && TrackpadGestureManager.isSupported {
                TrackpadGestureManager.shared.start()
            } else {
                TrackpadGestureManager.shared.stop()
            }
        }
    }
    
    func setupStatusItem() {
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        if let button = statusItem?.button {
            button.image = NSImage(systemSymbolName: "rocket", accessibilityDescription: "Launchpad Classic")
            button.target = self
            button.action = #selector(statusItemClicked)
            button.sendAction(on: [.leftMouseUp, .rightMouseUp])
        }
    }
    
    @objc func statusItemClicked() {
        let event = NSApp.currentEvent
        if event?.type == .rightMouseUp {
            let menu = NSMenu()
            menu.addItem(NSMenuItem(title: "Open Launchpad Classic", action: #selector(openLaunchpadClicked), keyEquivalent: ""))
            menu.addItem(NSMenuItem(title: "Settings...", action: #selector(settingsClicked), keyEquivalent: ","))
            menu.addItem(NSMenuItem.separator())
            menu.addItem(NSMenuItem(title: "Quit Launchpad Classic", action: #selector(quitClicked), keyEquivalent: "q"))
            menu.popUp(positioning: nil, at: NSEvent.mouseLocation, in: nil)
        } else {
            toggleLaunchpad()
        }
    }
    
    @objc func openLaunchpadClicked() {
        showLaunchpad()
    }
    
    @objc func settingsClicked() {
        showLaunchpad()
        // Wait briefly for launchpad transition before triggering open settings
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.15) {
            NotificationCenter.default.post(name: NSNotification.Name("LaunchpadOpenSettingsRequested"), object: nil)
        }
    }
    
    @objc func quitClicked() {
        NSApp.terminate(nil)
    }
    
    func toggleLaunchpad() {
        if window.isVisible {
            // Ask the view to play its zoom-out animation first; it posts
            // LaunchpadCloseRequested when finished, which hides the window.
            NotificationCenter.default.post(name: NSNotification.Name("LaunchpadDismissRequested"), object: nil)
        } else {
            showLaunchpad()
        }
    }
    
    func showLaunchpad() {
        let mouseLocation = NSEvent.mouseLocation
        let screens = NSScreen.screens
        let targetScreen = screens.first { NSMouseInRect(mouseLocation, $0.frame, false) } ?? NSScreen.main ?? screens.first
        let screenFrame = targetScreen?.frame ?? NSRect(x: 0, y: 0, width: 1920, height: 1080)
        window.setFrame(screenFrame, display: true)
        
        NotificationCenter.default.post(name: NSNotification.Name("LaunchpadWillOpen"), object: nil)
        
        // Promote activation policy so system presentation options (hide Dock/Menu Bar) work
        NSApp.setActivationPolicy(.regular)
        
        window.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
        
        // Hide macOS Dock and Menu Bar while active
        NSApp.presentationOptions = [.hideDock, .hideMenuBar]
    }
    
    func hideLaunchpad() {
        window.orderOut(nil)
        NotificationCenter.default.post(name: NSNotification.Name("LaunchpadDidClose"), object: nil)
        
        // Restore standard presentation options
        NSApp.presentationOptions = []
        
        // Restore accessory activation policy if background mode is enabled without Dock icon
        let persistentMode = UserDefaults.standard.object(forKey: "LaunchpadPersistentMode") as? Bool ?? true
        let showDockIcon = UserDefaults.standard.object(forKey: "LaunchpadShowDockIcon") as? Bool ?? true
        if persistentMode && !showDockIcon {
            NSApp.setActivationPolicy(.accessory)
        }
    }
    
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        showLaunchpad()
        return true
    }
    
    func windowDidResignKey(_ notification: Notification) {
        // Don't auto-hide when an in-app modal (confirmation alert) took key status
        guard !isModalActive else { return }
        
        let persistentMode = UserDefaults.standard.object(forKey: "LaunchpadPersistentMode") as? Bool ?? true
        if persistentMode {
            hideLaunchpad()
        } else {
            dismissAndExit()
        }
    }
    
    private func dismissAndExit() {
        NSApp.presentationOptions = []
        NSApp.terminate(nil)
    }
}

let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
_ = NSApplicationMain(CommandLine.argc, CommandLine.unsafeArgv)
