import Cocoa
import SwiftUI
import Carbon

// MARK: - Global Hotkey Preference

struct HotkeyPreference {
    var keyCode: UInt16
    var carbonModifiers: UInt32
    var display: String
    
    static let `default` = HotkeyPreference(keyCode: 49, carbonModifiers: UInt32(optionKey), display: "⌥ Space")
    
    static func load() -> HotkeyPreference {
        let defaults = UserDefaults.standard
        guard defaults.object(forKey: "LaunchpadHotkeyKeyCode") != nil else { return .default }
        return HotkeyPreference(
            keyCode: UInt16(defaults.integer(forKey: "LaunchpadHotkeyKeyCode")),
            carbonModifiers: UInt32(defaults.integer(forKey: "LaunchpadHotkeyModifiers")),
            display: defaults.string(forKey: "LaunchpadHotkeyDisplay") ?? HotkeyPreference.default.display
        )
    }
    
    func save() {
        let defaults = UserDefaults.standard
        defaults.set(Int(keyCode), forKey: "LaunchpadHotkeyKeyCode")
        defaults.set(Int(carbonModifiers), forKey: "LaunchpadHotkeyModifiers")
        defaults.set(display, forKey: "LaunchpadHotkeyDisplay")
    }
    
    static func reset() {
        let defaults = UserDefaults.standard
        defaults.removeObject(forKey: "LaunchpadHotkeyKeyCode")
        defaults.removeObject(forKey: "LaunchpadHotkeyModifiers")
        defaults.removeObject(forKey: "LaunchpadHotkeyDisplay")
    }
    
    /// Builds a preference from a captured key event.
    static func from(event: NSEvent) -> HotkeyPreference {
        let flags = event.modifierFlags.intersection([.command, .option, .control, .shift])
        var carbon: UInt32 = 0
        if flags.contains(.command) { carbon |= UInt32(cmdKey) }
        if flags.contains(.option) { carbon |= UInt32(optionKey) }
        if flags.contains(.control) { carbon |= UInt32(controlKey) }
        if flags.contains(.shift) { carbon |= UInt32(shiftKey) }
        
        var symbols = ""
        if flags.contains(.control) { symbols += "⌃" }
        if flags.contains(.option) { symbols += "⌥" }
        if flags.contains(.shift) { symbols += "⇧" }
        if flags.contains(.command) { symbols += "⌘" }
        
        return HotkeyPreference(
            keyCode: event.keyCode,
            carbonModifiers: carbon,
            display: symbols + " " + keyName(for: event)
        )
    }
    
    private static func keyName(for event: NSEvent) -> String {
        let special: [UInt16: String] = [
            49: "Space", 36: "Return", 48: "Tab", 51: "Delete", 53: "Esc",
            123: "←", 124: "→", 125: "↓", 126: "↑",
            115: "Home", 119: "End", 116: "Page Up", 121: "Page Down",
            122: "F1", 120: "F2", 99: "F3", 118: "F4", 96: "F5", 97: "F6",
            98: "F7", 100: "F8", 101: "F9", 109: "F10", 103: "F11", 111: "F12"
        ]
        if let name = special[event.keyCode] { return name }
        if let chars = event.charactersIgnoringModifiers, !chars.isEmpty {
            return chars.uppercased()
        }
        return "Key \(event.keyCode)"
    }
}

// MARK: - Application Directory Watcher

/// Watches the application folders for installs/removals and posts
/// "LaunchpadAppsChanged" (debounced) so the UI can rescan.
final class AppDirectoryWatcher {
    private var sources: [DispatchSourceFileSystemObject] = []
    private var debounceWorkItem: DispatchWorkItem?
    private let queue = DispatchQueue(label: "launchpad.app-directory-watcher")
    
    func start() {
        stop()
        let paths = [
            "/Applications",
            "/Applications/Utilities",
            NSHomeDirectory() + "/Applications"
        ]
        
        for path in paths {
            let fd = open(path, O_EVTONLY)
            guard fd >= 0 else { continue }
            
            let source = DispatchSource.makeFileSystemObjectSource(fileDescriptor: fd, eventMask: .write, queue: queue)
            source.setEventHandler { [weak self] in
                self?.scheduleNotification()
            }
            source.setCancelHandler {
                close(fd)
            }
            source.resume()
            sources.append(source)
        }
    }
    
    private func scheduleNotification() {
        debounceWorkItem?.cancel()
        let workItem = DispatchWorkItem {
            DispatchQueue.main.async {
                NotificationCenter.default.post(name: NSNotification.Name("LaunchpadAppsChanged"), object: nil)
            }
        }
        debounceWorkItem = workItem
        // Installers copy many files; wait for the dust to settle
        queue.asyncAfter(deadline: .now() + 2.0, execute: workItem)
    }
    
    func stop() {
        sources.forEach { $0.cancel() }
        sources.removeAll()
    }
    
    deinit {
        stop()
    }
}

// MARK: - Core Models

struct AppInfo: Identifiable, Hashable {
    var id: String { path }
    let name: String
    let path: String
    let icon: NSImage
    
    func hash(into hasher: inout Hasher) {
        hasher.combine(path)
    }
    
    static func == (lhs: AppInfo, rhs: AppInfo) -> Bool {
        return lhs.path == rhs.path
    }
}

struct FolderItem: Identifiable, Hashable {
    let id: UUID
    var name: String
    var apps: [AppInfo]
}

struct FolderData: Codable, Hashable {
    var id: UUID
    var name: String
    var appPaths: [String]
}

enum LaunchpadItem: Identifiable, Hashable {
    case app(AppInfo)
    case folder(FolderItem)
    
    var id: String {
        switch self {
        case .app(let app): return "app-\(app.path)"
        case .folder(let folder): return "folder-\(folder.id.uuidString)"
        }
    }
    
    var name: String {
        switch self {
        case .app(let app): return app.name
        case .folder(let folder): return folder.name
        }
    }
    
    var isApp: Bool {
        switch self {
        case .app: return true
        case .folder: return false
        }
    }
}

// MARK: - App Scanner

class AppScanner {
    
    // MARK: - UserDefaults Storage Helpers
    
    static func getHiddenAppPaths() -> Set<String> {
        let list = UserDefaults.standard.stringArray(forKey: "LaunchpadHiddenApps") ?? []
        return Set(list)
    }
    
    static func setHiddenAppPaths(_ paths: Set<String>) {
        UserDefaults.standard.set(Array(paths), forKey: "LaunchpadHiddenApps")
    }
    
    static func getPinnedAppPaths() -> [String] {
        if let list = UserDefaults.standard.stringArray(forKey: "LaunchpadPinnedApps") {
            return list
        }
        
        // Sensible default pinned apps if they exist on the filesystem
        let candidates = [
            "/System/Applications/App Store.app",
            "/System/Applications/Safari.app",
            "/Applications/Safari.app",
            "/System/Applications/System Settings.app",
            "/System/Applications/Utilities/Terminal.app",
            "/Applications/Utilities/Terminal.app",
            "/System/Applications/Photos.app",
            "/System/Applications/Music.app"
        ]
        
        var resolved: [String] = []
        var seen = Set<String>()
        for path in candidates {
            if FileManager.default.fileExists(atPath: path) && !seen.contains(path) {
                resolved.append(path)
                seen.insert(path)
            }
        }
        return resolved
    }
    
    static func setPinnedAppPaths(_ paths: [String]) {
        UserDefaults.standard.set(paths, forKey: "LaunchpadPinnedApps")
    }
    
    static func getFoldersData() -> [FolderData] {
        guard let data = UserDefaults.standard.data(forKey: "LaunchpadFolders"),
              let folders = try? JSONDecoder().decode([FolderData].self, from: data) else {
            return []
        }
        return folders
    }
    
    static func saveFoldersData(_ folders: [FolderData]) {
        if let data = try? JSONEncoder().encode(folders) {
            UserDefaults.standard.set(data, forKey: "LaunchpadFolders")
        }
    }
    
    // MARK: - Main Application Scanning
    
    static func scanForApps() -> [AppInfo] {
        let fileManager = FileManager.default
        let searchPaths = [
            "/Applications",
            "/System/Applications",
            "/Applications/Utilities",
            NSHomeDirectory() + "/Applications"
        ]
        
        var apps: [AppInfo] = []
        var seenNames = Set<String>()
        var seenPaths = Set<String>()
        
        for basePath in searchPaths {
            guard let files = try? fileManager.contentsOfDirectory(atPath: basePath) else { continue }
            for file in files {
                if file.hasSuffix(".app") {
                    let fullPath = (basePath as NSString).appendingPathComponent(file)
                    let name = (file as NSString).deletingPathExtension
                    
                    // Filter duplicates
                    if seenNames.contains(name) || seenPaths.contains(fullPath) {
                        continue
                    }
                    
                    // Skip helper apps or system processes that are not user-facing
                    if name.starts(with: ".") || name.contains("Install macOS") {
                        continue
                    }
                    
                    let icon = NSWorkspace.shared.icon(forFile: fullPath)
                    apps.append(AppInfo(name: name, path: fullPath, icon: icon))
                    seenNames.insert(name)
                    seenPaths.insert(fullPath)
                }
            }
        }
        
        return apps.sorted { $0.name.localizedCompare($1.name) == .orderedAscending }
    }
    
    // MARK: - Layout Composition (Apps & Folders)
    
    static func scanAndResolveLayout() -> [LaunchpadItem] {
        let allApps = scanForApps()
        let hiddenPaths = getHiddenAppPaths()
        
        // Filter out hidden applications
        let visibleApps = allApps.filter { !hiddenPaths.contains($0.path) }
        
        var foldersData = getFoldersData()
        
        // If folders are not initialized, create a default "Utilities" folder
        if foldersData.isEmpty && !UserDefaults.standard.bool(forKey: "LaunchpadFoldersInitialized") {
            var utilityPaths: [String] = []
            for app in visibleApps {
                if app.path.contains("/Utilities/") {
                    utilityPaths.append(app.path)
                }
            }
            if !utilityPaths.isEmpty {
                let defaultFolder = FolderData(id: UUID(), name: "Utilities", appPaths: utilityPaths)
                foldersData = [defaultFolder]
                saveFoldersData(foldersData)
            }
            UserDefaults.standard.set(true, forKey: "LaunchpadFoldersInitialized")
        }
        
        // Build map for quick lookup
        let appsByPath = Dictionary(uniqueKeysWithValues: visibleApps.map { ($0.path, $0) })
        
        var items: [LaunchpadItem] = []
        var appsInFolders = Set<String>()
        
        // 1. Process defined folders
        for folderData in foldersData {
            var folderApps: [AppInfo] = []
            for path in folderData.appPaths {
                if let app = appsByPath[path] {
                    folderApps.append(app)
                    appsInFolders.insert(path)
                }
            }
            // Only add folder if it has visible apps
            if !folderApps.isEmpty {
                // Keep inner apps sorted alphabetically
                let sortedFolderApps = folderApps.sorted { $0.name.localizedCompare($1.name) == .orderedAscending }
                let folderItem = FolderItem(id: folderData.id, name: folderData.name, apps: sortedFolderApps)
                items.append(.folder(folderItem))
            }
        }
        
        // 2. Add remaining standalone apps
        for app in visibleApps {
            if !appsInFolders.contains(app.path) {
                items.append(.app(app))
            }
        }
        
        // 3. Sort items based on saved custom order
        let savedOrder = UserDefaults.standard.stringArray(forKey: "LaunchpadItemOrder") ?? []
        let orderMap = Dictionary(uniqueKeysWithValues: savedOrder.enumerated().map { ($1, $0) })
        
        let sortedItems = items.sorted { (lhs, rhs) -> Bool in
            let idxL = orderMap[lhs.id]
            let idxR = orderMap[rhs.id]
            
            switch (idxL, idxR) {
            case (.some(let l), .some(let r)):
                return l < r
            case (.some, .none):
                return true
            case (.none, .some):
                return false
            case (.none, .none):
                // Fallback to alphabetical: folders first, then apps
                switch (lhs, rhs) {
                case (.folder(let f1), .folder(let f2)):
                    return f1.name.localizedCompare(f2.name) == .orderedAscending
                case (.folder, .app):
                    return true
                case (.app, .folder):
                    return false
                case (.app(let a1), .app(let a2)):
                    return a1.name.localizedCompare(a2.name) == .orderedAscending
                }
            }
        }
        
        // Save initial default order if not yet persisted
        if UserDefaults.standard.stringArray(forKey: "LaunchpadItemOrder") == nil {
            let orderIDs = sortedItems.map { $0.id }
            UserDefaults.standard.set(orderIDs, forKey: "LaunchpadItemOrder")
        }
        
        return sortedItems
    }
    
    // MARK: - Launch Handler
    
    static func launch(app: AppInfo) {
        recordLaunch(path: app.path)
        let url = URL(fileURLWithPath: app.path)
        NSWorkspace.shared.openApplication(at: url, configuration: NSWorkspace.OpenConfiguration()) { _, error in
            if let error = error {
                print("Failed to launch application \(app.name): \(error.localizedDescription)")
            }
            DispatchQueue.main.async {
                NotificationCenter.default.post(name: NSNotification.Name("LaunchpadCloseRequested"), object: nil)
            }
        }
    }
    
    // MARK: - Launch Frequency Tracking (frecency)
    
    static func recordLaunch(path: String) {
        let defaults = UserDefaults.standard
        var counts = (defaults.dictionary(forKey: "LaunchpadLaunchCounts") as? [String: Int]) ?? [:]
        counts[path, default: 0] += 1
        defaults.set(counts, forKey: "LaunchpadLaunchCounts")
        
        var stamps = (defaults.dictionary(forKey: "LaunchpadLastLaunch") as? [String: Double]) ?? [:]
        stamps[path] = Date().timeIntervalSince1970
        defaults.set(stamps, forKey: "LaunchpadLastLaunch")
    }
    
    static func getLaunchCount(path: String) -> Int {
        let counts = (UserDefaults.standard.dictionary(forKey: "LaunchpadLaunchCounts") as? [String: Int]) ?? [:]
        return counts[path] ?? 0
    }
    
    /// Combined frequency + recency score. Recency decays with a ~2 week half-life
    /// so old habits fade but frequent apps stay relevant.
    static func frecencyScore(path: String, counts: [String: Int], stamps: [String: Double]) -> Double {
        let count = Double(counts[path] ?? 0)
        guard count > 0 else { return 0 }
        let last = stamps[path] ?? 0
        let daysAgo = max(0, (Date().timeIntervalSince1970 - last) / 86_400)
        let recency = exp(-daysAgo / 14.0)
        return log2(1 + count) * (0.5 + recency)
    }
    
    /// Most-used apps for the Suggestions row (excludes hidden apps and apps
    /// that were never launched from here).
    static func suggestedApps(from apps: [AppInfo], limit: Int) -> [AppInfo] {
        let defaults = UserDefaults.standard
        let counts = (defaults.dictionary(forKey: "LaunchpadLaunchCounts") as? [String: Int]) ?? [:]
        let stamps = (defaults.dictionary(forKey: "LaunchpadLastLaunch") as? [String: Double]) ?? [:]
        let hidden = getHiddenAppPaths()
        
        return apps
            .filter { !hidden.contains($0.path) }
            .map { ($0, frecencyScore(path: $0.path, counts: counts, stamps: stamps)) }
            .filter { $0.1 > 0 }
            .sorted { $0.1 > $1.1 }
            .prefix(limit)
            .map { $0.0 }
    }
    
    // MARK: - Fuzzy Search
    
    /// Scores how well `query` matches `name`. Higher is better; nil means no match.
    /// Tiers: exact prefix > word prefix > initials ("vsc" → Visual Studio Code)
    /// > substring > in-order subsequence.
    static func searchScore(query: String, name: String) -> Double? {
        let q = query.lowercased().trimmingCharacters(in: .whitespaces)
        let n = name.lowercased()
        guard !q.isEmpty else { return nil }
        
        if n.hasPrefix(q) {
            return 1000 - Double(n.count - q.count) * 0.1
        }
        
        let words = n.split(separator: " ")
        if words.dropFirst().contains(where: { $0.hasPrefix(q) }) {
            return 850
        }
        
        let initials = String(words.compactMap { $0.first })
        if q.count >= 2 && initials.hasPrefix(q) {
            return 800
        }
        
        if let range = n.range(of: q) {
            let position = n.distance(from: n.startIndex, to: range.lowerBound)
            return 600 - Double(position)
        }
        
        // Subsequence: all query chars appear in order, penalize gaps
        var queryIndex = q.startIndex
        var gaps = 0
        var lastMatch = -1
        var position = 0
        for char in n {
            if queryIndex < q.endIndex && char == q[queryIndex] {
                if lastMatch >= 0 && position - lastMatch > 1 {
                    gaps += 1
                }
                lastMatch = position
                queryIndex = q.index(after: queryIndex)
            }
            position += 1
        }
        if queryIndex == q.endIndex {
            return 300 - Double(gaps) * 20 - Double(n.count) * 0.5
        }
        
        return nil
    }
    
    /// Filters and ranks apps for a search query: best fuzzy match first,
    /// with frequently launched apps boosted.
    static func rankedSearch(query: String, apps: [AppInfo]) -> [AppInfo] {
        let trimmed = query.trimmingCharacters(in: .whitespaces)
        guard !trimmed.isEmpty else { return apps }
        
        let defaults = UserDefaults.standard
        let counts = (defaults.dictionary(forKey: "LaunchpadLaunchCounts") as? [String: Int]) ?? [:]
        let stamps = (defaults.dictionary(forKey: "LaunchpadLastLaunch") as? [String: Double]) ?? [:]
        
        return apps
            .compactMap { app -> (AppInfo, Double)? in
                guard let base = searchScore(query: trimmed, name: app.name) else { return nil }
                let boost = frecencyScore(path: app.path, counts: counts, stamps: stamps) * 10
                return (app, base + boost)
            }
            .sorted { lhs, rhs in
                if lhs.1 != rhs.1 { return lhs.1 > rhs.1 }
                return lhs.0.name.localizedCompare(rhs.0.name) == .orderedAscending
            }
            .map { $0.0 }
    }
    
    // MARK: - App Removal Cleanup
    
    /// Removes every stored trace of an app (folders, order, hidden set, launch
    /// stats) after it has been moved to the Trash.
    static func purgeApp(path: String) {
        var folders = getFoldersData()
        for i in folders.indices {
            folders[i].appPaths.removeAll { $0 == path }
        }
        folders.removeAll { $0.appPaths.isEmpty }
        saveFoldersData(folders)
        
        var hidden = getHiddenAppPaths()
        hidden.remove(path)
        setHiddenAppPaths(hidden)
        
        let defaults = UserDefaults.standard
        var order = defaults.stringArray(forKey: "LaunchpadItemOrder") ?? []
        order.removeAll { $0 == "app-\(path)" }
        defaults.set(order, forKey: "LaunchpadItemOrder")
        
        var counts = (defaults.dictionary(forKey: "LaunchpadLaunchCounts") as? [String: Int]) ?? [:]
        counts.removeValue(forKey: path)
        defaults.set(counts, forKey: "LaunchpadLaunchCounts")
        
        var stamps = (defaults.dictionary(forKey: "LaunchpadLastLaunch") as? [String: Double]) ?? [:]
        stamps.removeValue(forKey: path)
        defaults.set(stamps, forKey: "LaunchpadLastLaunch")
    }
    
    static func getAppCategory(at path: String) -> String {
        let plistPath = (path as NSString).appendingPathComponent("Contents/Info.plist")
        guard let dict = NSDictionary(contentsOfFile: plistPath) else { return "Other" }
        if let catType = dict["LSApplicationCategoryType"] as? String {
            let components = catType.components(separatedBy: ".")
            if let last = components.last {
                let clean = last.replacingOccurrences(of: "-tools", with: "")
                                .replacingOccurrences(of: "-apps", with: "")
                                .replacingOccurrences(of: "productivity", with: "Productivity")
                                .replacingOccurrences(of: "utilities", with: "Utilities")
                                .replacingOccurrences(of: "education", with: "Education")
                                .replacingOccurrences(of: "games", with: "Games")
                                .replacingOccurrences(of: "developer", with: "Developer")
                                .replacingOccurrences(of: "graphics-design", with: "Design")
                                .replacingOccurrences(of: "photography", with: "Photography")
                                .replacingOccurrences(of: "video", with: "Video")
                                .replacingOccurrences(of: "music", with: "Music")
                                .replacingOccurrences(of: "social-networking", with: "Social")
                                .replacingOccurrences(of: "entertainment", with: "Entertainment")
                                .capitalized
                return clean
            }
        }
        
        let lowerPath = path.lowercased()
        if lowerPath.contains("xcode") || lowerPath.contains("terminal") || lowerPath.contains("vscode") {
            return "Developer"
        } else if lowerPath.contains("safari") || lowerPath.contains("chrome") || lowerPath.contains("firefox") || lowerPath.contains("mail") {
            return "Productivity"
        } else if lowerPath.contains("music") || lowerPath.contains("tv") || lowerPath.contains("photos") {
            return "Media"
        }
        
        return "Other"
    }
    
    static func autoCategorizeLayout() -> [LaunchpadItem] {
        let allApps = scanForApps()
        let hiddenPaths = getHiddenAppPaths()
        let visibleApps = allApps.filter { !hiddenPaths.contains($0.path) }
        
        var appsByCategory: [String: [AppInfo]] = [:]
        for app in visibleApps {
            let category = getAppCategory(at: app.path)
            appsByCategory[category, default: []].append(app)
        }
        
        var items: [LaunchpadItem] = []
        var folders: [FolderData] = []
        
        for (category, apps) in appsByCategory {
            if apps.count >= 2 && category != "Other" {
                let folderId = UUID()
                let folderData = FolderData(id: folderId, name: category, appPaths: apps.map { $0.path })
                folders.append(folderData)
                
                let sortedFolderApps = apps.sorted { $0.name.localizedCompare($1.name) == .orderedAscending }
                let folderItem = FolderItem(id: folderId, name: category, apps: sortedFolderApps)
                items.append(.folder(folderItem))
            } else {
                for app in apps {
                    items.append(.app(app))
                }
            }
        }
        
        if let otherApps = appsByCategory["Other"] {
            for app in otherApps {
                if !items.contains(where: {
                    if case .app(let a) = $0 { return a.path == app.path }
                    return false
                }) {
                    items.append(.app(app))
                }
            }
        }
        
        let sortedLayout = items.sorted { (lhs, rhs) -> Bool in
            switch (lhs, rhs) {
            case (.folder(let f1), .folder(let f2)):
                return f1.name.localizedCompare(f2.name) == .orderedAscending
            case (.folder, .app):
                return true
            case (.app, .folder):
                return false
            case (.app(let a1), .app(let a2)):
                return a1.name.localizedCompare(a2.name) == .orderedAscending
            }
        }
        
        saveFoldersData(folders)
        UserDefaults.standard.set(true, forKey: "LaunchpadFoldersInitialized")
        
        let orderIDs = sortedLayout.map { $0.id }
        UserDefaults.standard.set(orderIDs, forKey: "LaunchpadItemOrder")
        
        return sortedLayout
    }
}



