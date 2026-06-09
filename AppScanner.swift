import Cocoa
import SwiftUI

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



