import SwiftUI
import Cocoa
import UniformTypeIdentifiers
import ServiceManagement

struct LaunchpadView: View {
    // MARK: - State Variables
    @State private var items: [LaunchpadItem] = []
    @State private var allApps: [AppInfo] = []
    @State private var searchQuery: String = ""
    @State private var currentPage: Int = 0
    @State private var dragOffset: CGFloat = 0
    @State private var isAnimatingIn = false
    @State private var isAnimatingOut = false
    @State private var isLoading = true
    @State private var scrollMonitor: Any? = nil
    @State private var accumDeltaX: CGFloat = 0
    
    // Selection and Navigation State
    @State private var focusedIndex: Int? = nil
    @State private var expandedFolder: FolderItem? = nil
    @State private var folderFocusedIndex: Int? = nil
    @State private var draggedItem: LaunchpadItem? = nil
    
    // Daemon preferences state
    @AppStorage("LaunchpadPersistentMode") private var persistentMode: Bool = true
    @AppStorage("LaunchpadShowDockIcon") private var showDockIcon: Bool = true
    @AppStorage("LaunchpadShowMenuBarIcon") private var showMenuBarIcon: Bool = true
    @AppStorage("LaunchpadLaunchAtLogin") private var launchAtLogin: Bool = false
    @AppStorage("LaunchpadPinchGestures") private var pinchGestures: Bool = true
    
    @Namespace private var searchNamespace
    
    // Page grid configuration
    @AppStorage("LaunchpadColumns") private var columnsCount: Int = 7
    @AppStorage("LaunchpadRows") private var rowsCount: Int = 5
    @AppStorage("LaunchpadIconSize") private var iconSizeSelection: Double = 80.0
    private var appsPerPage: Int { columnsCount * rowsCount }
    @AppStorage("LaunchpadBgTintOpacity") private var bgTintOpacity: Double = 0.15
    @AppStorage("LaunchpadShowLabels") private var showLabels: Bool = true
    @AppStorage("LaunchpadShowSuggestions") private var showSuggestions: Bool = true
    @State private var isShowingSettings = false
    @State private var selectedSettingsTab = 0
    
    @State private var isSearchActive = false
    @State private var hoveredMergeTarget: LaunchpadItem? = nil
    
    // Get Info panel + drag-to-edge page flipping
    @State private var infoApp: AppInfo? = nil
    @State private var edgeFlipTimer: Timer? = nil
    @State private var lastEdgeFlipTime: Date = .distantPast
    
    var body: some View {
        GeometryReader { geometry in
            let screenWidth = geometry.size.width
            let screenHeight = geometry.size.height
            
            let isSmallScreen = screenHeight < 950
            let rawIconSize = CGFloat(iconSizeSelection)
            let rawRowSpacing: CGFloat = isSmallScreen ? 22 : 32
            let rawColumnSpacing: CGFloat = isSmallScreen ? 44 : 52
            
            // Suggestions row (most-used apps), shown above the grid when idle
            let suggestedApps = (showSuggestions && searchQuery.isEmpty && !isLoading)
                ? AppScanner.suggestedApps(from: allApps, limit: min(6, columnsCount))
                : []
            let suggestionsVisible = !suggestedApps.isEmpty
            
            // Limit grid area to fit screen height and avoid overlapping search capsule
            let maxGridHeight = screenHeight - 240 - (suggestionsVisible ? 96 : 0)
            let numerator = max(100.0, maxGridHeight - CGFloat(rowsCount) * 40)
            let denominator = max(1.0, CGFloat(rowsCount) * rawIconSize + CGFloat(rowsCount - 1) * rawRowSpacing)
            let scaleFactor = min(1.0, max(0.35, numerator / denominator))
            
            let iconSize = rawIconSize * scaleFactor
            let rowSpacing = rawRowSpacing * scaleFactor
            let columnSpacing = rawColumnSpacing * scaleFactor
            let gridHeight = CGFloat(rowsCount) * (iconSize + 40) + CGFloat(rowsCount - 1) * rowSpacing
            
            ZStack {
                // Background styling stack
                ZStack {
                    // Blurred Desktop Background
                    VisualEffectView()
                        .edgesIgnoringSafeArea(.all)
                    
                    // Customizable Dark Tint Overlay
                    Color.black.opacity(bgTintOpacity)
                        .edgesIgnoringSafeArea(.all)
                }
                
                // Main Content Container
                VStack(spacing: 0) {
                    Spacer()
                        .frame(height: 60)
                    
                    Spacer()
                    
                    if suggestionsVisible {
                        VStack(spacing: 8) {
                            Text("SUGGESTIONS")
                                .font(.system(size: 10, weight: .semibold))
                                .tracking(1.4)
                                .foregroundColor(.white.opacity(0.4))
                            
                            HStack(spacing: 30) {
                                ForEach(suggestedApps) { app in
                                    SuggestionCell(app: app, onLaunch: {
                                        launchAppAction(app)
                                    })
                                }
                            }
                        }
                        .padding(.bottom, 22)
                        .transition(.opacity.combined(with: .move(edge: .top)))
                    }
                    
                    Group {
                        if isLoading {
                        // Skeleton screen loading state
                        SkeletonGridView(columns: columnsCount)
                            .frame(height: gridHeight)
                            .transition(.opacity)
                    } else if searchQuery.isEmpty {
                        // Horizontal Paged Grid
                        let pages = chunkedItems(items, size: appsPerPage)
                        let pageCount = max(1, pages.count)
                        
                        ZStack(alignment: .bottomLeading) {
                            HStack(alignment: .top, spacing: 0) {
                                ForEach(0..<pageCount, id: \.self) { pageIndex in
                                    if pageIndex < pages.count {
                                        ItemGridView(
                                            items: pages[pageIndex],
                                            columns: columnsCount,
                                            rows: rowsCount,
                                            focusedIndex: focusedIndex,
                                            pageIndex: pageIndex,
                                            currentPage: currentPage,
                                            iconSize: iconSize,
                                            rowSpacing: rowSpacing,
                                            columnSpacing: columnSpacing,
                                            draggedItem: $draggedItem,
                                            listData: $items,
                                            hoveredMergeTarget: $hoveredMergeTarget,
                                            onLaunch: launchItemAction,
                                            onHide: hideAppAction,
                                            onDisbandFolder: disbandFolderAction,
                                            onMoveToFolder: moveAppToFolderAction,
                                            onRemoveFromFolder: removeAppFromFolderAction,
                                            onCreateFolder: createFolderWithAppAction,
                                            onMerge: mergeItemsAction,
                                            onShowInfo: showAppInfoAction,
                                            onMoveToTrash: moveAppToTrashAction
                                        )
                                        .frame(width: screenWidth)
                                    } else {
                                        VStack {
                                            Text("No applications found.")
                                                .foregroundColor(.white.opacity(0.8))
                                                .font(.title)
                                        }
                                        .frame(width: screenWidth, height: gridHeight)
                                    }
                                }
                            }
                            .frame(width: screenWidth * CGFloat(pageCount), alignment: .leading)
                            .offset(x: -CGFloat(currentPage) * screenWidth + dragOffset)
                            .frame(width: screenWidth, alignment: .leading)
                            .background(Color.black.opacity(0.01))
                            .gesture(
                                DragGesture()
                                    .onChanged { value in
                                        if expandedFolder != nil { return } // Disable swipe if folder is open
                                        let translation = value.translation.width
                                        let isFirstPageRightDrag = (currentPage == 0 && translation > 0)
                                        let isLastPageLeftDrag = (currentPage == pageCount - 1 && translation < 0)
                                        if isFirstPageRightDrag || isLastPageLeftDrag {
                                            dragOffset = translation * 0.3
                                        } else {
                                            dragOffset = translation
                                        }
                                    }
                                    .onEnded { value in
                                        if expandedFolder != nil { return }
                                        let threshold: CGFloat = 80
                                        let velocity = value.predictedEndTranslation.width
                                        
                                        withAnimation(.spring(response: 0.35, dampingFraction: 0.82)) {
                                            if velocity < -threshold && currentPage < pageCount - 1 {
                                                currentPage += 1
                                                focusedIndex = nil
                                            } else if velocity > threshold && currentPage > 0 {
                                                currentPage -= 1
                                                focusedIndex = nil
                                            }
                                            dragOffset = 0
                                        }
                                    }
                            )
                        }
                        .frame(width: screenWidth, height: gridHeight + 10)
                        .transition(.opacity)
                        
                    } else {
                        // Filtered Search Results Grid (fuzzy match, frecency-ranked)
                        let filteredApps = AppScanner.rankedSearch(query: searchQuery, apps: allApps)
                        
                        let math = mathResult
                        let cmd = matchedSystemCommand
                        let hasQuickAction = (math != nil || cmd != nil)
                        
                        VStack(spacing: 20) {
                            if let result = math {
                                QuickActionCard(
                                    title: result,
                                    subtitle: "Calculation Result (Press Enter to Copy)",
                                    icon: "equal.circle.fill",
                                    action: {
                                        copyToClipboard(result)
                                    }
                                )
                                .frame(width: 500)
                                .transition(.move(edge: .top).combined(with: .opacity))
                            } else if let command = cmd {
                                QuickActionCard(
                                    title: command.name,
                                    subtitle: command.description + " (Press Enter to Execute)",
                                    icon: command.icon,
                                    action: {
                                        runShellCommand(command.shellCommand)
                                        NotificationCenter.default.post(name: NSNotification.Name("LaunchpadCloseRequested"), object: nil)
                                    }
                                )
                                .frame(width: 500)
                                .transition(.move(edge: .top).combined(with: .opacity))
                            }
                            
                            if filteredApps.isEmpty {
                                if !hasQuickAction {
                                    VStack(spacing: 15) {
                                        Image(systemName: "magnifyingglass")
                                            .font(.system(size: 48, weight: .thin))
                                            .foregroundColor(.white.opacity(0.4))
                                        Text("No Results for \"\(searchQuery)\"")
                                            .font(.title2)
                                            .fontWeight(.medium)
                                            .foregroundColor(.white.opacity(0.6))
                                    }
                                    .frame(width: screenWidth, height: gridHeight + 10)
                                    .transition(.opacity)
                                } else {
                                    Spacer()
                                }
                            } else {
                                ScrollView(.vertical, showsIndicators: true) {
                                    ItemGridView(
                                        items: filteredApps.map { .app($0) },
                                        columns: columnsCount,
                                        rows: rowsCount,
                                        focusedIndex: focusedIndex,
                                        pageIndex: 0,
                                        currentPage: 0,
                                        iconSize: iconSize,
                                        rowSpacing: rowSpacing,
                                        columnSpacing: columnSpacing,
                                        draggedItem: $draggedItem,
                                        listData: $items,
                                        hoveredMergeTarget: $hoveredMergeTarget,
                                        onLaunch: launchItemAction,
                                        onHide: hideAppAction,
                                        onDisbandFolder: disbandFolderAction,
                                        onMoveToFolder: moveAppToFolderAction,
                                        onRemoveFromFolder: removeAppFromFolderAction,
                                        onCreateFolder: createFolderWithAppAction,
                                        onMerge: mergeItemsAction,
                                        onShowInfo: showAppInfoAction,
                                        onMoveToTrash: moveAppToTrashAction
                                    )
                                }
                                .frame(width: screenWidth, height: gridHeight + 10 - (hasQuickAction ? 100 : 0))
                                .transition(.opacity)
                                .animation(.spring(response: 0.35, dampingFraction: 0.78), value: filteredApps)
                            }
                        }
                        .frame(width: screenWidth, height: gridHeight + 10)
                    }
                    }
                    .animation(.spring(response: 0.4, dampingFraction: 0.82), value: searchQuery.isEmpty)
                    .animation(.spring(response: 0.45, dampingFraction: 0.8), value: columnsCount)
                    .animation(.spring(response: 0.45, dampingFraction: 0.8), value: rowsCount)
                    .animation(.spring(response: 0.35, dampingFraction: 0.85), value: iconSizeSelection)
                    
                    Spacer()
                    
                    // Unified Page Indicator & Search Capsule (Always at the bottom)
                    let pages = chunkedItems(items, size: appsPerPage)
                    let pageCount = max(1, pages.count)
                    let isSearching = isSearchActive || !searchQuery.isEmpty
                    
                    ZStack {
                        // Normal page dots + search icon state (only interactive when not searching)
                        HStack(spacing: 8) {
                            Image(systemName: "magnifyingglass")
                                .font(.system(size: 13, weight: .bold))
                                .foregroundColor(.white.opacity(0.7))
                                .padding(.leading, 14)
                                .onTapGesture {
                                    withAnimation(.spring(response: 0.35, dampingFraction: 0.78)) {
                                        isSearchActive = true
                                    }
                                }
                            
                            if pageCount > 1 {
                                PageControlView(numberOfPages: pageCount, currentPage: $currentPage, dragOffset: dragOffset, screenWidth: screenWidth)
                            }
                            
                            Button(action: {
                                withAnimation(.spring(response: 0.35, dampingFraction: 0.82)) {
                                    isShowingSettings.toggle()
                                }
                            }) {
                                Image(systemName: "gearshape")
                                    .font(.system(size: 13, weight: .bold))
                                    .foregroundColor(isShowingSettings ? .blue : .white.opacity(0.7))
                            }
                            .buttonStyle(.plain)
                            .padding(.trailing, 14)
                        }
                        .opacity(isSearching ? 0.0 : 1.0)
                        .allowsHitTesting(!isSearching)
                        
                        // Search input state (permanently in view tree for focus stability)
                        HStack(spacing: 8) {
                            Image(systemName: "magnifyingglass")
                                .foregroundColor(.white.opacity(0.6))
                                .font(.system(size: 13, weight: .medium))
                                .padding(.leading, 12)
                            
                            SearchTextField(text: $searchQuery, isFocused: $isSearchActive, placeholder: "Search", onCommit: {
                                launchFirstSearchResult()
                            })
                            .frame(height: 22)
                            
                            HStack(spacing: 6) {
                                if !searchQuery.isEmpty {
                                    Button(action: {
                                        withAnimation(.spring(response: 0.35, dampingFraction: 0.82)) {
                                            searchQuery = ""
                                        }
                                    }) {
                                        Image(systemName: "xmark.circle.fill")
                                            .foregroundColor(.white.opacity(0.6))
                                            .font(.system(size: 13))
                                    }
                                    .buttonStyle(.plain)
                                }
                                
                                Button(action: {
                                    withAnimation(.spring(response: 0.35, dampingFraction: 0.82)) {
                                        isShowingSettings.toggle()
                                    }
                                }) {
                                    Image(systemName: "gearshape")
                                        .foregroundColor(isShowingSettings ? .blue : .white.opacity(0.7))
                                        .font(.system(size: 13, weight: .medium))
                                }
                                .buttonStyle(.plain)
                            }
                            .padding(.trailing, 12)
                        }
                        .frame(width: isSearching ? 320 : 0)
                        .clipped()
                        .opacity(isSearching ? 1.0 : 0.0)
                        .allowsHitTesting(isSearching)
                    }
                    .frame(height: 38)
                    .frame(width: isSearching ? 320 : nil)
                    .padding(.horizontal, isSearching ? 0 : 6)
                    .background(
                        Capsule()
                            .fill(Color.black.opacity(0.4))
                            .overlay(
                                Capsule()
                                    .stroke(Color.white.opacity(0.12), lineWidth: 1)
                            )
                    )
                    .onTapGesture {
                        if !isSearching {
                            withAnimation(.spring(response: 0.35, dampingFraction: 0.78)) {
                                isSearchActive = true
                            }
                        }
                    }
                    .padding(.bottom, 24)
                }
                .frame(width: screenWidth)
                .animation(.spring(response: 0.45, dampingFraction: 0.82), value: suggestionsVisible)
                .opacity(isAnimatingOut ? 0 : (isAnimatingIn ? (expandedFolder != nil ? 0.35 : 1.0) : 0.0))
                .scaleEffect(isAnimatingOut ? 0.92 : (isAnimatingIn ? (expandedFolder != nil ? 0.96 : 1.0) : 0.95))
                .blur(radius: expandedFolder != nil ? 10 : 0)
                
                // Folder Expanded Overlay
                if let folder = expandedFolder {
                    FolderOverlayView(
                        folder: folder,
                        iconSize: CGFloat(iconSizeSelection),
                        focusedIndex: folderFocusedIndex,
                        onClose: {
                            withAnimation(.spring(response: 0.3, dampingFraction: 0.8)) {
                                expandedFolder = nil
                                folderFocusedIndex = nil
                            }
                        },
                        onRename: { newName in
                            renameFolderAction(folder, newName: newName)
                            if var updated = expandedFolder {
                                updated.name = newName
                                expandedFolder = updated
                            }
                        },
                        onLaunchApp: { app in
                            launchAppAction(app)
                        },
                        onRemoveApp: { app in
                            removeAppFromFolderAction(app)
                            if let updatedFolder = AppScanner.scanAndResolveLayout().compactMap({ item -> FolderItem? in
                                if case .folder(let f) = item, f.id == folder.id { return f }
                                return nil
                            }).first {
                                expandedFolder = updatedFolder
                            } else {
                                expandedFolder = nil
                            }
                        }
                    )
                    .transition(.opacity.combined(with: .scale(scale: 0.8)))
                }
                
                if isShowingSettings {
                    SettingsCardOverlayView(
                        columnsCount: $columnsCount,
                        rowsCount: $rowsCount,
                        iconSizeSelection: $iconSizeSelection,
                        bgTintOpacity: $bgTintOpacity,
                        showLabels: $showLabels,
                        showSuggestions: $showSuggestions,
                        persistentMode: $persistentMode,
                        showDockIcon: $showDockIcon,
                        showMenuBarIcon: $showMenuBarIcon,
                        launchAtLogin: $launchAtLogin,
                        pinchGestures: $pinchGestures,
                        selectedTab: $selectedSettingsTab,
                        onAutoCategorize: autoCategorizeAction,
                        onResetAll: resetLayoutAction,
                        onClose: {
                            withAnimation(.spring(response: 0.35, dampingFraction: 0.82)) {
                                isShowingSettings = false
                            }
                        }
                    )
                    .transition(.opacity.combined(with: .scale(scale: 0.92)))
                }
                
                // App "Get Info" panel
                if let app = infoApp {
                    Color.black.opacity(0.35)
                        .edgesIgnoringSafeArea(.all)
                        .onTapGesture {
                            withAnimation(.spring(response: 0.3, dampingFraction: 0.8)) {
                                infoApp = nil
                            }
                        }
                    
                    AppInfoCardView(app: app, onClose: {
                        withAnimation(.spring(response: 0.3, dampingFraction: 0.8)) {
                            infoApp = nil
                        }
                    })
                    .transition(.opacity.combined(with: .scale(scale: 0.95)))
                }
            }
            .frame(width: screenWidth, height: screenHeight)
            .onTapGesture {
                if expandedFolder != nil {
                    withAnimation(.spring(response: 0.3)) {
                        expandedFolder = nil
                        folderFocusedIndex = nil
                    }
                } else if isShowingSettings {
                    withAnimation(.spring(response: 0.35, dampingFraction: 0.82)) {
                        isShowingSettings = false
                    }
                } else if isSearchActive {
                    withAnimation(.spring(response: 0.35, dampingFraction: 0.82)) {
                        searchQuery = ""
                        isSearchActive = false
                    }
                } else {
                    dismissLaunchpad()
                }
            }
            .contextMenu {
                if expandedFolder == nil {
                    Button("Settings...") {
                        withAnimation(.spring(response: 0.35, dampingFraction: 0.82)) {
                            isShowingSettings = true
                        }
                    }
                    Divider()
                    Button("Reset Layout") {
                        resetLayoutAction()
                    }
                }
            }
            .onDrop(of: [UTType.text.identifier], isTargeted: nil) { providers in
                DispatchQueue.main.asyncAfter(deadline: .now() + 0.2) {
                    withAnimation(.spring(response: 0.35, dampingFraction: 0.8)) {
                        self.draggedItem = nil
                    }
                }
                return true
            }
            .onAppear {
                loadLaunchpadData()
                
                // Reflect the actual login-item state (it can be changed from
                // System Settings behind our back)
                if #available(macOS 13.0, *) {
                    let actuallyEnabled = SMAppService.mainApp.status == .enabled
                    if launchAtLogin != actuallyEnabled {
                        launchAtLogin = actuallyEnabled
                    }
                }
                
                // Animate zoom-in entry
                withAnimation(.spring(response: 0.45, dampingFraction: 0.82)) {
                    isAnimatingIn = true
                }
                
                // Local monitor for trackpad swipes and mouse scrolls
                scrollMonitor = NSEvent.addLocalMonitorForEvents(matching: .scrollWheel) { event in
                    handleScrollWheel(with: event)
                    return event
                }
            }
            .onDisappear {
                if let monitor = scrollMonitor {
                    NSEvent.removeMonitor(monitor)
                    scrollMonitor = nil
                }
                stopEdgeMonitor()
            }
            // Keyboard event hooks
            .onReceive(NotificationCenter.default.publisher(for: NSNotification.Name("LaunchpadKeyDown"))) { notification in
                guard let keyCode = notification.userInfo?["keyCode"] as? UInt16 else { return }
                handleKeyPress(keyCode: keyCode)
            }
            .onReceive(NotificationCenter.default.publisher(for: NSNotification.Name("LaunchpadAlphaNumericTyped"))) { notification in
                withAnimation(.spring(response: 0.4, dampingFraction: 0.76)) {
                    if let chars = notification.userInfo?["characters"] as? String {
                        searchQuery = chars
                    }
                    if !isSearchActive {
                        isSearchActive = true
                    }
                }
            }
            .onReceive(NotificationCenter.default.publisher(for: NSNotification.Name("LaunchpadEscapePressed"))) { _ in
                if infoApp != nil {
                    withAnimation(.spring(response: 0.3, dampingFraction: 0.8)) {
                        infoApp = nil
                    }
                } else if !searchQuery.isEmpty || isSearchActive {
                    withAnimation(.spring(response: 0.35, dampingFraction: 0.82)) {
                        searchQuery = ""
                        isSearchActive = false
                    }
                } else {
                    dismissLaunchpad()
                }
            }
            .onReceive(NotificationCenter.default.publisher(for: NSNotification.Name("LaunchpadAppsChanged"))) { _ in
                // App installed/removed while running: rescan in the background
                loadLaunchpadData()
            }
            .onReceive(NotificationCenter.default.publisher(for: NSNotification.Name("LaunchpadDismissRequested"))) { _ in
                // Hotkey/menu-bar toggle: animate out before the window hides
                dismissLaunchpad()
            }
            .onChange(of: launchAtLogin) { _, enabled in
                applyLaunchAtLogin(enabled)
            }
            .onChange(of: draggedItem) { _, newValue in
                // While an icon is being dragged, watch the mouse so hovering at
                // a screen edge flips pages (like native Launchpad)
                if newValue != nil && expandedFolder == nil && searchQuery.isEmpty {
                    startEdgeMonitor()
                } else {
                    stopEdgeMonitor()
                }
            }
            .onChange(of: isSearchActive) { oldActive, newActive in
                if newActive {
                    NotificationCenter.default.post(name: NSNotification.Name("LaunchpadSearchActivated"), object: nil)
                } else {
                    NotificationCenter.default.post(name: NSNotification.Name("LaunchpadSearchDeactivated"), object: nil)
                }
            }
            .onChange(of: persistentMode) { _, newVal in
                NotificationCenter.default.post(name: NSNotification.Name("LaunchpadSettingsChanged"), object: nil)
            }
            .onChange(of: showDockIcon) { _, newVal in
                NotificationCenter.default.post(name: NSNotification.Name("LaunchpadSettingsChanged"), object: nil)
            }
            .onChange(of: showMenuBarIcon) { _, newVal in
                NotificationCenter.default.post(name: NSNotification.Name("LaunchpadSettingsChanged"), object: nil)
            }
            .onChange(of: pinchGestures) { _, _ in
                NotificationCenter.default.post(name: NSNotification.Name("LaunchpadSettingsChanged"), object: nil)
            }
            .onChange(of: columnsCount) { _, _ in
                clampCurrentPage()
            }
            .onChange(of: rowsCount) { _, _ in
                clampCurrentPage()
            }
            .onReceive(NotificationCenter.default.publisher(for: NSNotification.Name("LaunchpadWillOpen"))) { _ in
                isAnimatingIn = false
                isAnimatingOut = false
                withAnimation(.spring(response: 0.45, dampingFraction: 0.82)) {
                    isAnimatingIn = true
                }
                loadLaunchpadData()
            }
            .onReceive(NotificationCenter.default.publisher(for: NSNotification.Name("LaunchpadResetLayout"))) { _ in
                resetLayoutAction()
            }
            .onReceive(NotificationCenter.default.publisher(for: NSNotification.Name("LaunchpadAutoCategorize"))) { _ in
                autoCategorizeAction()
            }
            .onReceive(NotificationCenter.default.publisher(for: NSNotification.Name("LaunchpadOpenSettingsRequested"))) { _ in
                withAnimation(.spring(response: 0.35, dampingFraction: 0.82)) {
                    isShowingSettings = true
                }
            }
        }
    }
    
    // MARK: - Data Management & Refresh
    
    private func loadLaunchpadData() {
        if items.isEmpty {
            isLoading = true
        }
        DispatchQueue.global(qos: .userInitiated).async {
            let layout = AppScanner.scanAndResolveLayout()
            let apps = AppScanner.scanForApps()
            
            DispatchQueue.main.async {
                self.items = layout
                self.allApps = apps
                withAnimation(.easeOut(duration: 0.25)) {
                    self.isLoading = false
                }
            }
        }
    }
    
    private func handleScrollWheel(with event: NSEvent) {
        guard expandedFolder == nil else { return }
        
        let deltaX = event.scrollingDeltaX
        let hasPrecise = event.hasPreciseScrollingDeltas
        let multiplier: CGFloat = hasPrecise ? 1.0 : 15.0
        let actualDeltaX = deltaX * multiplier

        let pages = chunkedItems(items, size: appsPerPage)
        let pageCount = max(1, pages.count)

        if !hasPrecise {
            // Discrete mouse scroll wheel: page immediately
            let threshold: CGFloat = 3.0
            if actualDeltaX < -threshold && currentPage < pageCount - 1 {
                withAnimation(.spring(response: 0.35, dampingFraction: 0.82)) {
                    currentPage += 1
                    focusedIndex = nil
                }
            } else if actualDeltaX > threshold && currentPage > 0 {
                withAnimation(.spring(response: 0.35, dampingFraction: 0.82)) {
                    currentPage -= 1
                    focusedIndex = nil
                }
            }
            return
        }

        // Trackpad precise swipe gesture
        // Ignore momentum events to prevent double-paging
        guard event.momentumPhase.isEmpty else { return }

        if event.phase == .began {
            accumDeltaX = 0
            dragOffset = 0
        }

        accumDeltaX += actualDeltaX
        
        // Apply rubber banding at boundaries
        let translation = accumDeltaX
        if (currentPage == 0 && translation > 0) || (currentPage == pageCount - 1 && translation < 0) {
            dragOffset = translation * 0.3
        } else {
            dragOffset = translation
        }

        if event.phase == .ended || event.phase == .cancelled {
            let threshold: CGFloat = 80
            withAnimation(.spring(response: 0.35, dampingFraction: 0.82)) {
                if dragOffset < -threshold && currentPage < pageCount - 1 {
                    currentPage += 1
                    focusedIndex = nil
                } else if dragOffset > threshold && currentPage > 0 {
                    currentPage -= 1
                    focusedIndex = nil
                }
                dragOffset = 0
            }
            accumDeltaX = 0
        }
    }
    
    private func refreshLayout() {
        let layout = AppScanner.scanAndResolveLayout()
        let apps = AppScanner.scanForApps()
        
        self.items = layout
        self.allApps = apps
    }
    
    private func saveOrder() {
        let orderIDs = items.map { $0.id }
        UserDefaults.standard.set(orderIDs, forKey: "LaunchpadItemOrder")
    }
    
    private func clampCurrentPage() {
        let pages = chunkedItems(items, size: appsPerPage)
        let pageCount = max(1, pages.count)
        if currentPage >= pageCount {
            withAnimation(.spring(response: 0.35, dampingFraction: 0.8)) {
                currentPage = max(0, pageCount - 1)
            }
        }
    }
    
    private func resetLayoutAction() {
        withAnimation(.spring(response: 0.35, dampingFraction: 0.8)) {
            columnsCount = 7
            rowsCount = 5
            let isSmallScreen = (NSScreen.main?.frame.height ?? 1080) < 950
            iconSizeSelection = isSmallScreen ? 72 : 80
            bgTintOpacity = 0.15
            currentPage = 0
        }
        
        UserDefaults.standard.set(7, forKey: "LaunchpadColumns")
        UserDefaults.standard.set(5, forKey: "LaunchpadRows")
        let isSmallScreen = (NSScreen.main?.frame.height ?? 1080) < 950
        UserDefaults.standard.set(Double(isSmallScreen ? 72 : 80), forKey: "LaunchpadIconSize")
        UserDefaults.standard.set(0.15, forKey: "LaunchpadBgTintOpacity")
        
        UserDefaults.standard.removeObject(forKey: "LaunchpadItemOrder")
        UserDefaults.standard.removeObject(forKey: "LaunchpadHiddenApps")
        UserDefaults.standard.removeObject(forKey: "LaunchpadFolders")
        UserDefaults.standard.removeObject(forKey: "LaunchpadFoldersInitialized")
        
        withAnimation(.spring(response: 0.35, dampingFraction: 0.8)) {
            refreshLayout()
        }
    }
    
    // MARK: - App & Folder Actions
    
    private func launchItemAction(_ item: LaunchpadItem) {
        switch item {
        case .app(let app):
            launchAppAction(app)
        case .folder(let folder):
            withAnimation(.spring(response: 0.38, dampingFraction: 0.8)) {
                expandedFolder = folder
                folderFocusedIndex = nil
            }
        }
    }
    
    private func launchAppAction(_ app: AppInfo) {
        withAnimation(.easeOut(duration: 0.2)) {
            isAnimatingOut = true
        }
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.2) {
            AppScanner.launch(app: app)
        }
    }
    
    private func launchFirstSearchResult() {
        if let result = mathResult {
            copyToClipboard(result)
            return
        }
        if let command = matchedSystemCommand {
            runShellCommand(command.shellCommand)
            NotificationCenter.default.post(name: NSNotification.Name("LaunchpadCloseRequested"), object: nil)
            return
        }
        
        let filteredApps = AppScanner.rankedSearch(query: searchQuery, apps: allApps)
        if let firstApp = filteredApps.first {
            launchAppAction(firstApp)
        }
    }
    
    private func hideAppAction(_ app: AppInfo) {
        var hidden = AppScanner.getHiddenAppPaths()
        hidden.insert(app.path)
        AppScanner.setHiddenAppPaths(hidden)
        
        withAnimation(.easeInOut(duration: 0.2)) {
            refreshLayout()
        }
    }
    
    private func disbandFolderAction(_ folder: FolderItem) {
        var folders = AppScanner.getFoldersData()
        folders.removeAll(where: { $0.id == folder.id })
        AppScanner.saveFoldersData(folders)
        
        withAnimation(.easeInOut(duration: 0.2)) {
            refreshLayout()
        }
    }
    
    private func moveAppToFolderAction(_ app: AppInfo, _ folderData: FolderData) {
        var folders = AppScanner.getFoldersData()
        
        // Strip app from any folders first
        for i in 0..<folders.count {
            folders[i].appPaths.removeAll(where: { $0 == app.path })
        }
        
        // Add to targeted folder
        if let idx = folders.firstIndex(where: { $0.id == folderData.id }) {
            folders[idx].appPaths.append(app.path)
        }
        
        // Sweep empty folders
        folders.removeAll(where: { $0.appPaths.isEmpty })
        
        AppScanner.saveFoldersData(folders)
        
        withAnimation(.easeInOut(duration: 0.2)) {
            refreshLayout()
        }
    }
    
    private func removeAppFromFolderAction(_ app: AppInfo) {
        var folders = AppScanner.getFoldersData()
        for i in 0..<folders.count {
            folders[i].appPaths.removeAll(where: { $0 == app.path })
        }
        folders.removeAll(where: { $0.appPaths.isEmpty })
        AppScanner.saveFoldersData(folders)
        
        withAnimation(.easeInOut(duration: 0.2)) {
            refreshLayout()
        }
    }
    
    private func createFolderWithAppAction(_ app: AppInfo) {
        var folders = AppScanner.getFoldersData()
        
        // Remove app from other folders
        for i in 0..<folders.count {
            folders[i].appPaths.removeAll(where: { $0 == app.path })
        }
        folders.removeAll(where: { $0.appPaths.isEmpty })
        
        // Create new folder
        let folder = FolderData(id: UUID(), name: "New Folder", appPaths: [app.path])
        folders.append(folder)
        
        AppScanner.saveFoldersData(folders)
        
        withAnimation(.easeInOut(duration: 0.2)) {
            refreshLayout()
        }
    }
    
    private func renameFolderAction(_ folder: FolderItem, newName: String) {
        var folders = AppScanner.getFoldersData()
        if let idx = folders.firstIndex(where: { $0.id == folder.id }) {
            folders[idx].name = newName
            AppScanner.saveFoldersData(folders)
            
            withAnimation(.easeInOut(duration: 0.15)) {
                refreshLayout()
            }
        }
    }
    
    
    private func mergeItemsAction(_ dragged: LaunchpadItem, _ target: LaunchpadItem) {
        var folders = AppScanner.getFoldersData()
        var savedOrder = UserDefaults.standard.stringArray(forKey: "LaunchpadItemOrder") ?? []
        
        let draggedId = dragged.id
        let targetId = target.id
        
        switch (dragged, target) {
        case (.app(let dragApp), .app(let targetApp)):
            let folderId = UUID()
            let cat1 = AppScanner.getAppCategory(at: dragApp.path)
            let cat2 = AppScanner.getAppCategory(at: targetApp.path)
            let folderName = (cat1 == cat2 && cat1 != "Other") ? cat1 : "New Folder"
            
            let newFolder = FolderData(id: folderId, name: folderName, appPaths: [targetApp.path, dragApp.path])
            folders.append(newFolder)
            
            let newFolderIdString = "folder-\(folderId.uuidString)"
            // Insert the new folder in place of the target app
            if let idx = savedOrder.firstIndex(of: targetId) {
                savedOrder.insert(newFolderIdString, at: idx)
            } else {
                savedOrder.append(newFolderIdString)
            }
            savedOrder.removeAll { $0 == draggedId || $0 == targetId }
            
        case (.app(let dragApp), .folder(let targetFolder)):
            if let idx = folders.firstIndex(where: { $0.id == targetFolder.id }) {
                if !folders[idx].appPaths.contains(dragApp.path) {
                    folders[idx].appPaths.append(dragApp.path)
                }
            }
            // Target folder keeps its position, dragged app is removed from main grid
            savedOrder.removeAll { $0 == draggedId }
            
        case (.folder(let dragFolder), .folder(let targetFolder)):
            if let dragIdx = folders.firstIndex(where: { $0.id == dragFolder.id }),
               let targetIdx = folders.firstIndex(where: { $0.id == targetFolder.id }) {
                for path in folders[dragIdx].appPaths {
                    if !folders[targetIdx].appPaths.contains(path) {
                        folders[targetIdx].appPaths.append(path)
                    }
                }
                folders.remove(at: dragIdx)
            }
            // Target folder keeps its position, dragged folder is removed from main grid
            savedOrder.removeAll { $0 == draggedId }
            
        case (.folder(let dragFolder), .app(let targetApp)):
            if let idx = folders.firstIndex(where: { $0.id == dragFolder.id }) {
                if !folders[idx].appPaths.contains(targetApp.path) {
                    folders[idx].appPaths.append(targetApp.path)
                }
            }
            // Dragged folder moves to the location of the target app, target app is removed
            if let targetIdx = savedOrder.firstIndex(of: targetId) {
                savedOrder.removeAll { $0 == draggedId } // remove from old position first
                if let newTargetIdx = savedOrder.firstIndex(of: targetId) {
                    savedOrder.insert(draggedId, at: newTargetIdx)
                } else {
                    savedOrder.insert(draggedId, at: targetIdx)
                }
            }
            savedOrder.removeAll { $0 == targetId }
        }
        
        folders.removeAll(where: { $0.appPaths.isEmpty })
        AppScanner.saveFoldersData(folders)
        
        // Save the updated custom order list to UserDefaults
        UserDefaults.standard.set(savedOrder, forKey: "LaunchpadItemOrder")
        
        withAnimation(.spring(response: 0.35, dampingFraction: 0.8)) {
            refreshLayout()
        }
    }
    
    private func autoCategorizeAction() {
        withAnimation(.spring(response: 0.45, dampingFraction: 0.8)) {
            let categorizedItems = AppScanner.autoCategorizeLayout()
            self.items = categorizedItems
            
            let pages = chunkedItems(categorizedItems, size: appsPerPage)
            let pageCount = max(1, pages.count)
            if currentPage >= pageCount {
                currentPage = pageCount - 1
            }
        }
    }
    
    private func dismissLaunchpad() {
        withAnimation(.easeOut(duration: 0.15)) {
            isAnimatingOut = true
        }
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.15) {
            if persistentMode {
                NotificationCenter.default.post(name: NSNotification.Name("LaunchpadCloseRequested"), object: nil)
                isAnimatingOut = false
            } else {
                NSApp.terminate(nil)
            }
        }
    }
    
    // MARK: - Launch at Login
    
    private func applyLaunchAtLogin(_ enabled: Bool) {
        guard #available(macOS 13.0, *) else { return }
        do {
            if enabled {
                if SMAppService.mainApp.status != .enabled {
                    try SMAppService.mainApp.register()
                }
            } else {
                if SMAppService.mainApp.status == .enabled {
                    try SMAppService.mainApp.unregister()
                }
            }
        } catch {
            print("Failed to update launch-at-login: \(error.localizedDescription)")
        }
    }
    
    // MARK: - Get Info & Move to Trash
    
    private func showAppInfoAction(_ app: AppInfo) {
        withAnimation(.spring(response: 0.3, dampingFraction: 0.8)) {
            infoApp = app
        }
    }
    
    /// Presents an alert as a sheet attached to the launchpad window. The
    /// overlay window sits at status-bar level, so a free-floating
    /// `runModal()` alert would open underneath it; a sheet is always above
    /// its parent. Also pauses the auto-hide-on-resign-key behavior.
    private func presentAlertSheet(_ alert: NSAlert, completion: @escaping (NSApplication.ModalResponse) -> Void) {
        NotificationCenter.default.post(name: NSNotification.Name("LaunchpadModalBegan"), object: nil)
        let finish: (NSApplication.ModalResponse) -> Void = { response in
            NotificationCenter.default.post(name: NSNotification.Name("LaunchpadModalEnded"), object: nil)
            completion(response)
        }
        
        guard let window = NSApp.windows.first(where: { $0 is LaunchpadWindow && $0.isVisible }) else {
            finish(alert.runModal())
            return
        }
        alert.beginSheetModal(for: window) { response in
            finish(response)
        }
    }
    
    private func moveAppToTrashAction(_ app: AppInfo) {
        guard !app.path.hasPrefix("/System/") else {
            let alert = NSAlert()
            alert.messageText = "\"\(app.name)\" can't be moved to the Trash"
            alert.informativeText = "Built-in system applications are protected by macOS and cannot be deleted."
            alert.alertStyle = .informational
            presentAlertSheet(alert) { _ in }
            return
        }
        
        let alert = NSAlert()
        alert.messageText = "Move \"\(app.name)\" to the Trash?"
        alert.informativeText = "The application at \(app.path) will be moved to the Trash."
        alert.alertStyle = .warning
        alert.addButton(withTitle: "Move to Trash")
        alert.addButton(withTitle: "Cancel")
        
        presentAlertSheet(alert) { response in
            guard response == .alertFirstButtonReturn else { return }
            
            NSWorkspace.shared.recycle([URL(fileURLWithPath: app.path)]) { _, error in
                DispatchQueue.main.async {
                    if let error = error {
                        let failAlert = NSAlert()
                        failAlert.messageText = "Couldn't move \"\(app.name)\" to the Trash"
                        failAlert.informativeText = error.localizedDescription
                        failAlert.alertStyle = .warning
                        presentAlertSheet(failAlert) { _ in }
                    } else {
                        AppScanner.purgeApp(path: app.path)
                        withAnimation(.easeInOut(duration: 0.2)) {
                            refreshLayout()
                        }
                    }
                }
            }
        }
    }
    
    // MARK: - Drag-to-Edge Page Flipping
    
    /// Polls the mouse position during an active icon drag. Drop-target
    /// tracking can't be used here: views inserted mid-drag aren't registered
    /// with the session, and default-mode timers pause while AppKit runs its
    /// drag-tracking run loop, so the timer must run in `.common` modes.
    private func startEdgeMonitor() {
        stopEdgeMonitor()
        lastEdgeFlipTime = .distantPast
        let timer = Timer(timeInterval: 0.1, repeats: true) { _ in
            checkEdgeHover()
        }
        RunLoop.main.add(timer, forMode: .common)
        edgeFlipTimer = timer
    }
    
    private func stopEdgeMonitor() {
        edgeFlipTimer?.invalidate()
        edgeFlipTimer = nil
    }
    
    private func checkEdgeHover() {
        guard draggedItem != nil, expandedFolder == nil, searchQuery.isEmpty else {
            stopEdgeMonitor()
            return
        }
        guard let window = NSApp.windows.first(where: { $0 is LaunchpadWindow && $0.isVisible }) else { return }
        
        let mouseX = NSEvent.mouseLocation.x
        let frame = window.frame
        let edgeZone: CGFloat = 44
        
        // Throttle so hovering at the edge flips one page every 0.8s
        guard Date().timeIntervalSince(lastEdgeFlipTime) >= 0.8 else { return }
        
        if mouseX <= frame.minX + edgeZone {
            lastEdgeFlipTime = Date()
            flipPage(-1)
        } else if mouseX >= frame.maxX - edgeZone {
            lastEdgeFlipTime = Date()
            flipPage(1)
        }
    }
    
    private func flipPage(_ direction: Int) {
        let pageCount = max(1, chunkedItems(items, size: appsPerPage).count)
        let target = currentPage + direction
        guard target >= 0 && target < pageCount else { return }
        withAnimation(.spring(response: 0.35, dampingFraction: 0.82)) {
            currentPage = target
            focusedIndex = nil
        }
    }
    
    // MARK: - Keyboard Navigation
    
    private func handleKeyPress(keyCode: UInt16) {
        if expandedFolder != nil {
            handleFolderKeyPress(keyCode: keyCode)
            return
        }
        
        let isSearching = !searchQuery.isEmpty
        let pageItems: [LaunchpadItem]
        
        if isSearching {
            let filtered = AppScanner.rankedSearch(query: searchQuery, apps: allApps)
            pageItems = filtered.map { .app($0) }
        } else {
            let pages = chunkedItems(items, size: appsPerPage)
            guard currentPage < pages.count else { return }
            pageItems = pages[currentPage]
        }
        
        guard !pageItems.isEmpty else { return }
        
        var currentIdx = focusedIndex ?? -1
        
        switch keyCode {
        case 123: // Left Arrow
            if currentIdx == -1 {
                currentIdx = pageItems.count - 1
            } else if currentIdx == 0 && !isSearching && currentPage > 0 {
                currentPage -= 1
                let prevPageCount = chunkedItems(items, size: appsPerPage)[currentPage].count
                currentIdx = prevPageCount - 1
            } else {
                currentIdx = max(0, currentIdx - 1)
            }
        case 124: // Right Arrow
            if currentIdx == -1 {
                currentIdx = 0
            } else if currentIdx == pageItems.count - 1 && !isSearching && currentPage < chunkedItems(items, size: appsPerPage).count - 1 {
                currentPage += 1
                currentIdx = 0
            } else {
                currentIdx = min(pageItems.count - 1, currentIdx + 1)
            }
        case 126: // Up Arrow
            if currentIdx == -1 {
                currentIdx = pageItems.count - 1
            } else {
                let target = currentIdx - columnsCount
                if target >= 0 {
                    currentIdx = target
                }
            }
        case 125: // Down Arrow
            if currentIdx == -1 {
                currentIdx = 0
            } else {
                let target = currentIdx + columnsCount
                if target < pageItems.count {
                    currentIdx = target
                }
            }
        case 36: // Return/Enter
            if currentIdx >= 0 && currentIdx < pageItems.count {
                let selectedItem = pageItems[currentIdx]
                launchOrOpenItem(selectedItem)
            }
        case 116: // Page Up
            if !isSearching && currentPage > 0 {
                currentPage -= 1
                currentIdx = 0
            }
        case 121: // Page Down
            if !isSearching && currentPage < chunkedItems(items, size: appsPerPage).count - 1 {
                currentPage += 1
                currentIdx = 0
            }
        default:
            break
        }
        
        focusedIndex = currentIdx
    }
    
    private func handleFolderKeyPress(keyCode: UInt16) {
        guard let folder = expandedFolder else { return }
        let apps = folder.apps
        guard !apps.isEmpty else { return }
        
        var currentIdx = folderFocusedIndex ?? -1
        // Mirrors FolderOverlayView's adaptive column count
        let folderColumns = min(max(apps.count, 1), 5)
        
        switch keyCode {
        case 123: // Left
            if currentIdx == -1 { currentIdx = apps.count - 1 }
            else { currentIdx = max(0, currentIdx - 1) }
        case 124: // Right
            if currentIdx == -1 { currentIdx = 0 }
            else { currentIdx = min(apps.count - 1, currentIdx + 1) }
        case 126: // Up
            if currentIdx != -1 {
                let target = currentIdx - folderColumns
                if target >= 0 { currentIdx = target }
            }
        case 125: // Down
            if currentIdx == -1 { currentIdx = 0 }
            else {
                let target = currentIdx + folderColumns
                if target < apps.count { currentIdx = target }
            }
        case 36: // Enter
            if currentIdx >= 0 && currentIdx < apps.count {
                launchAppAction(apps[currentIdx])
            }
        default:
            break
        }
        
        folderFocusedIndex = currentIdx
    }
    
    private func launchOrOpenItem(_ item: LaunchpadItem) {
        switch item {
        case .app(let app):
            launchAppAction(app)
        case .folder(let folder):
            withAnimation(.spring(response: 0.38, dampingFraction: 0.8)) {
                expandedFolder = folder
                folderFocusedIndex = nil
            }
        }
    }
    
    // kVK_Space is 49 (0x31)
    
    // MARK: - Layout Helpers
    
    private func chunkedItems(_ list: [LaunchpadItem], size: Int) -> [[LaunchpadItem]] {
        guard !list.isEmpty else { return [] }
        return stride(from: 0, to: list.count, by: size).map {
            Array(list[$0..<min($0 + size, list.count)])
        }
    }
    
    // MARK: - Spotlight Search Actions Helpers
    
    private func convertIntegersToDoubles(in expression: String) -> String {
        let pattern = "(?<!\\.)\\b(\\d+)\\b(?!\\.)"
        guard let regex = try? NSRegularExpression(pattern: pattern, options: []) else {
            return expression
        }
        let range = NSRange(expression.startIndex..<expression.endIndex, in: expression)
        return regex.stringByReplacingMatches(
            in: expression,
            options: [],
            range: range,
            withTemplate: "$1.0"
        )
    }
    
    private func isValidMathExpression(_ expr: String) -> Bool {
        let trimmed = expr.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return false }
        
        // 1. Cannot end with an operator or a decimal point
        let lastChar = trimmed.last!
        guard !"+-*/.".contains(lastChar) else { return false }
        
        // 2. Cannot start with a multiplicative operator
        let firstChar = trimmed.first!
        guard !"*/".contains(firstChar) else { return false }
        
        // 3. Parentheses balancing check
        var parenCount = 0
        var prevChar: Character? = nil
        
        // Remove spaces
        let noSpaces = trimmed.replacingOccurrences(of: " ", with: "")
        
        for char in noSpaces {
            if char == "(" {
                parenCount += 1
                if let prev = prevChar, prev == ")" || prev == "." { return false }
            } else if char == ")" {
                parenCount -= 1
                if parenCount < 0 { return false }
                if let prev = prevChar, "+-*/.(".contains(prev) { return false }
            } else if "*/".contains(char) {
                if let prev = prevChar, "+-*/(".contains(prev) { return false }
            } else if "+-".contains(char) {
                if let prev = prevChar, "+-*/".contains(prev) { return false }
            } else if char == "." {
                if let prev = prevChar, !prev.isNumber { return false }
            } else if char.isNumber {
                if let prev = prevChar, prev == ")" { return false }
            }
            prevChar = char
        }
        
        return parenCount == 0
    }
    
    private var mathResult: String? {
        let trimmed = searchQuery.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return nil }
        
        let allowed = CharacterSet(charactersIn: "0123456789+-*/(). ")
        guard trimmed.unicodeScalars.allSatisfy({ allowed.contains($0) }) else { return nil }
        
        let hasDigit = trimmed.contains(where: { $0.isNumber })
        let hasOperator = trimmed.contains(where: { "+-*/".contains($0) })
        guard hasDigit && hasOperator else { return nil }
        
        // Safety check to prevent NSExpression parse exceptions/crashes!
        guard isValidMathExpression(trimmed) else { return nil }
        
        let formatted = convertIntegersToDoubles(in: trimmed)
        let expr = NSExpression(format: formatted)
        if let result = expr.expressionValue(with: nil, context: nil) {
            if let doubleVal = result as? Double {
                if doubleVal.truncatingRemainder(dividingBy: 1) == 0 {
                    return "\(Int(doubleVal))"
                } else {
                    return String(format: "%.4f", doubleVal)
                }
            }
            return "\(result)"
        }
        return nil
    }
    
    private struct SystemCommand {
        let name: String
        let description: String
        let icon: String
        let shellCommand: String
    }
    
    private var matchedSystemCommand: SystemCommand? {
        let trimmed = searchQuery.lowercased().trimmingCharacters(in: .whitespacesAndNewlines)
        switch trimmed {
        case "lock":
            return SystemCommand(
                name: "Lock Screen",
                description: "Lock your Mac instantly",
                icon: "lock.fill",
                shellCommand: "osascript -e 'tell application \"System Events\" to keystroke \"q\" using {control down, command down}'"
            )
        case "sleep":
            return SystemCommand(
                name: "Sleep",
                description: "Put your Mac to sleep",
                icon: "moon.fill",
                shellCommand: "osascript -e 'tell application \"System Events\" to sleep'"
            )
        case "screensaver":
            return SystemCommand(
                name: "Screen Saver",
                description: "Start the screen saver",
                icon: "sparkles",
                shellCommand: "open -a ScreenSaverEngine"
            )
        case "empty trash", "trash":
            return SystemCommand(
                name: "Empty Trash",
                description: "Permanently delete items in Trash",
                icon: "trash.fill",
                shellCommand: "osascript -e 'tell application \"Finder\" to empty trash'"
            )
        case "restart":
            return SystemCommand(
                name: "Restart",
                description: "Restart your Mac",
                icon: "arrow.clockwise",
                shellCommand: "osascript -e 'tell application \"System Events\" to restart'"
            )
        case "shutdown":
            return SystemCommand(
                name: "Shut Down",
                description: "Shut down your Mac",
                icon: "power",
                shellCommand: "osascript -e 'tell application \"System Events\" to shut down'"
            )
        default:
            return nil
        }
    }
    
    private func copyToClipboard(_ text: String) {
        let pasteboard = NSPasteboard.general
        pasteboard.declareTypes([.string], owner: nil)
        pasteboard.setString(text, forType: .string)
        NSSound.beep()
    }
    
    private func runShellCommand(_ command: String) {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/bin/zsh")
        process.arguments = ["-c", command]
        try? process.run()
    }
}

struct QuickActionCard: View {
    let title: String
    let subtitle: String
    let icon: String
    let action: () -> Void
    
    @State private var isHovered = false
    
    var body: some View {
        HStack(spacing: 16) {
            Image(systemName: icon)
                .font(.system(size: 24))
                .foregroundColor(.white.opacity(0.9))
                .frame(width: 50, height: 50)
                .background(
                    RoundedRectangle(cornerRadius: 12)
                        .fill(Color.blue.opacity(0.3))
                )
            
            VStack(alignment: .leading, spacing: 4) {
                Text(title)
                    .font(.system(size: 18, weight: .semibold))
                    .foregroundColor(.white)
                Text(subtitle)
                    .font(.system(size: 13))
                    .foregroundColor(.white.opacity(0.6))
            }
            
            Spacer()
            
            Image(systemName: "chevron.right")
                .font(.system(size: 14, weight: .bold))
                .foregroundColor(.white.opacity(0.4))
        }
        .padding(16)
        .background(
            RoundedRectangle(cornerRadius: 16, style: .continuous)
                .fill(Color.white.opacity(isHovered ? 0.15 : 0.08))
                .overlay(
                    RoundedRectangle(cornerRadius: 16, style: .continuous)
                        .stroke(Color.white.opacity(0.12), lineWidth: 1)
                )
        )
        .contentShape(Rectangle())
        .onHover { hover in
            withAnimation(.easeOut(duration: 0.15)) {
                isHovered = hover
            }
        }
        .onTapGesture {
            action()
        }
    }
}

// MARK: - Search Bar View

struct SearchBarView: View {
    @Binding var text: String
    @FocusState var isFocused: Bool
    
    var body: some View {
        HStack(spacing: 6) {
            if !isFocused && text.isEmpty {
                Spacer()
            }
            
            Image(systemName: "magnifyingglass")
                .foregroundColor(.white.opacity(0.6))
                .font(.system(size: 13, weight: .medium))
            
            TextField("Search", text: $text)
                .focused($isFocused)
                .textFieldStyle(.plain)
                .foregroundColor(.white)
                .font(.system(size: 13, weight: .medium))
                .frame(width: isFocused || !text.isEmpty ? 180 : 60)
                .placeholder(when: text.isEmpty) {
                    Text("Search")
                        .foregroundColor(.white.opacity(0.5))
                        .font(.system(size: 13, weight: .medium))
                }
            
            if !text.isEmpty {
                Button(action: { text = "" }) {
                    Image(systemName: "xmark.circle.fill")
                        .foregroundColor(.white.opacity(0.6))
                        .font(.system(size: 13))
                }
                .buttonStyle(.plain)
            }
            
            if !isFocused && text.isEmpty {
                Spacer()
            }
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 6)
        .background(
            RoundedRectangle(cornerRadius: 8, style: .continuous)
                .fill(Color.white.opacity(0.12))
                .overlay(
                    RoundedRectangle(cornerRadius: 8, style: .continuous)
                        .stroke(Color.white.opacity(0.08), lineWidth: 1)
                )
        )
        .animation(.spring(response: 0.25, dampingFraction: 0.75), value: isFocused)
    }
}

// Placeholder Utility Extension
extension View {
    func placeholder<Content: View>(
        when shouldShow: Bool,
        alignment: Alignment = .leading,
        @ViewBuilder placeholder: () -> Content
    ) -> some View {
        ZStack(alignment: alignment) {
            placeholder().opacity(shouldShow ? 1 : 0)
            self
        }
    }
    
    @ViewBuilder
    func `if`<Content: View>(_ conditional: Bool, transform: (Self) -> Content) -> some View {
        if conditional {
            transform(self)
        } else {
            self
        }
    }
}

// MARK: - Grid View Component

struct DragRelocateDelegate: DropDelegate {
    let item: LaunchpadItem
    @Binding var listData: [LaunchpadItem]
    @Binding var draggedItem: LaunchpadItem?
    @Binding var hoveredMergeTarget: LaunchpadItem?
    let iconSize: CGFloat
    let onOrderChanged: () -> Void
    let onMerge: (LaunchpadItem, LaunchpadItem) -> Void
    
    func dropEntered(info: DropInfo) {
        checkMergeOrSwap(info: info)
    }
    
    func dropUpdated(info: DropInfo) -> DropProposal? {
        checkMergeOrSwap(info: info)
        return DropProposal(operation: .move)
    }
    
    func dropExited(info: DropInfo) {
        if hoveredMergeTarget == item {
            withAnimation(.spring(response: 0.25, dampingFraction: 0.75)) {
                hoveredMergeTarget = nil
            }
        }
    }
    
    private func checkMergeOrSwap(info: DropInfo) {
        guard let dragged = draggedItem, dragged != item else { return }
        
        let cellW = iconSize + 20
        let cellH = iconSize + 40
        let loc = info.location
        
        // Inner 60% of target cell acts as the merge hover trigger zone
        let marginX = cellW * 0.2
        let marginY = cellH * 0.2
        let isInCenter = loc.x > marginX && loc.x < (cellW - marginX) &&
                         loc.y > marginY && loc.y < (cellH - marginY)
        
        if isInCenter {
            if hoveredMergeTarget != item {
                withAnimation(.spring(response: 0.25, dampingFraction: 0.75)) {
                    hoveredMergeTarget = item
                }
            }
        } else {
            if hoveredMergeTarget == item {
                withAnimation(.spring(response: 0.25, dampingFraction: 0.75)) {
                    hoveredMergeTarget = nil
                }
            }
            
            let from = listData.firstIndex(of: dragged)
            let to = listData.firstIndex(of: item)
            if let from = from, let to = to {
                if from != to {
                    withAnimation(.spring(response: 0.4, dampingFraction: 0.62, blendDuration: 0.15)) {
                        listData.move(fromOffsets: IndexSet(integer: from), toOffset: to > from ? to + 1 : to)
                    }
                }
            }
        }
    }
    
    func performDrop(info: DropInfo) -> Bool {
        if let dragged = draggedItem, let targetHovered = hoveredMergeTarget, targetHovered == item {
            onMerge(dragged, item)
            withAnimation(.spring(response: 0.3, dampingFraction: 0.8)) {
                hoveredMergeTarget = nil
                draggedItem = nil
            }
            onOrderChanged()
            return true
        }
        
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.2) {
            withAnimation(.spring(response: 0.35, dampingFraction: 0.8)) {
                self.draggedItem = nil
                self.hoveredMergeTarget = nil
            }
        }
        onOrderChanged()
        return true
    }
}

// MARK: - Grid View Component

struct ItemGridView: View {
    let items: [LaunchpadItem]
    let columns: Int
    let rows: Int
    let focusedIndex: Int?
    let pageIndex: Int
    let currentPage: Int
    
    let iconSize: CGFloat
    let rowSpacing: CGFloat
    let columnSpacing: CGFloat
    
    @Binding var draggedItem: LaunchpadItem?
    @Binding var listData: [LaunchpadItem]
    @Binding var hoveredMergeTarget: LaunchpadItem?
    
    let onLaunch: (LaunchpadItem) -> Void
    let onHide: (AppInfo) -> Void
    let onDisbandFolder: (FolderItem) -> Void
    let onMoveToFolder: (AppInfo, FolderData) -> Void
    let onRemoveFromFolder: (AppInfo) -> Void
    let onCreateFolder: (AppInfo) -> Void
    let onMerge: (LaunchpadItem, LaunchpadItem) -> Void
    let onShowInfo: (AppInfo) -> Void
    let onMoveToTrash: (AppInfo) -> Void
    
    var body: some View {
        // Use fixed column widths to match native macOS layout precisely
        let gridItems = Array(repeating: GridItem(.fixed(iconSize + 20), spacing: columnSpacing), count: columns)
        
        LazyVGrid(columns: gridItems, spacing: rowSpacing) {
            ForEach(items) { item in
                let index = items.firstIndex(of: item) ?? 0
                let isFocused = (currentPage == pageIndex && focusedIndex == index)
                let isMergeTarget = (hoveredMergeTarget == item)
                
                Group {
                    switch item {
                    case .app(let app):
                        AppIconCell(
                            app: app,
                            isFocused: isFocused,
                            isMergeTarget: isMergeTarget,
                            iconSize: iconSize,
                            onLaunch: { onLaunch(.app(app)) },
                            onHide: { onHide(app) },
                            onRemoveFromFolder: { onRemoveFromFolder(app) },
                            onCreateFolder: { onCreateFolder(app) },
                            onMoveToFolder: { folder in onMoveToFolder(app, folder) },
                            onShowInfo: { onShowInfo(app) },
                            onMoveToTrash: { onMoveToTrash(app) }
                        )
                    case .folder(let folder):
                        FolderIconCell(
                            folder: folder,
                            isFocused: isFocused,
                            isMergeTarget: isMergeTarget,
                            iconSize: iconSize,
                            onOpen: { onLaunch(.folder(folder)) },
                            onDisband: { onDisbandFolder(folder) },
                            onRename: { newName in }
                        )
                    }
                }
                .opacity(draggedItem == item ? 0.01 : 1.0)
                .onDrag {
                    self.draggedItem = item
                    return NSItemProvider(object: item.id as NSString)
                }
                .onDrop(of: [UTType.text.identifier], delegate: DragRelocateDelegate(
                    item: item,
                    listData: $listData,
                    draggedItem: $draggedItem,
                    hoveredMergeTarget: $hoveredMergeTarget,
                    iconSize: iconSize,
                    onOrderChanged: {
                        let orderIDs = listData.map { $0.id }
                        UserDefaults.standard.set(orderIDs, forKey: "LaunchpadItemOrder")
                    },
                    onMerge: onMerge
                ))
            }
        }
        .frame(width: CGFloat(columns) * (iconSize + 20) + CGFloat(columns - 1) * columnSpacing,
               height: CGFloat(rows) * (iconSize + 40) + CGFloat(rows - 1) * rowSpacing,
               alignment: .top)
        .background(Color.black.opacity(0.001))
        .onTapGesture {
            // Absorb tap to prevent closing Launchpad when clicking between icons in the grid
        }
        .padding(.vertical, 10)
    }
}

// MARK: - App Cell Renderer

struct AppIconCell: View {
    let app: AppInfo
    let isFocused: Bool
    let isMergeTarget: Bool
    let iconSize: CGFloat
    let onLaunch: () -> Void
    let onHide: () -> Void
    let onRemoveFromFolder: () -> Void
    let onCreateFolder: () -> Void
    let onMoveToFolder: (FolderData) -> Void
    let onShowInfo: () -> Void
    let onMoveToTrash: () -> Void
    
    @AppStorage("LaunchpadShowLabels") private var showLabels: Bool = true
    @State private var isHovered = false
    
    var body: some View {
        VStack(spacing: 8) {
            ZStack(alignment: .topLeading) {
                // Application Icon with Hover Glow
                Image(nsImage: app.icon)
                    .resizable()
                    .frame(width: iconSize, height: iconSize)
                    .scaleEffect(isMergeTarget ? 1.18 : (isHovered ? 1.08 : 1.0))
                    .shadow(color: isFocused ? Color.blue.opacity(0.5) : (isMergeTarget ? Color.green.opacity(0.5) : (isHovered ? .white.opacity(0.18) : .black.opacity(0.15))),
                            radius: isFocused ? 14 : (isMergeTarget ? 16 : (isHovered ? 12 : 5)),
                            x: 0,
                            y: isHovered ? 6 : 3)
                    .overlay(
                        RoundedRectangle(cornerRadius: 18)
                            .stroke(isMergeTarget ? Color.green.opacity(0.85) : Color.blue.opacity(0.85), lineWidth: 3.5)
                            .scaleEffect(1.1)
                            .blur(radius: 0.5)
                            .opacity(isFocused || isMergeTarget ? 1.0 : 0.0)
                    )
                    .animation(.spring(response: 0.22, dampingFraction: 0.65), value: isHovered)
                    .animation(.spring(response: 0.25, dampingFraction: 0.7), value: isFocused)
                    .animation(.spring(response: 0.25, dampingFraction: 0.7), value: isMergeTarget)
            }
            
            // Application Label (kept in layout when hidden so grid math is stable)
            Text(app.name)
                .foregroundColor(.white)
                .font(.system(size: 12, weight: .medium))
                .multilineTextAlignment(.center)
                .lineLimit(2)
                .frame(height: 32, alignment: .top)
                .shadow(color: .black.opacity(0.65), radius: 3, x: 0, y: 1.5)
                .opacity(showLabels ? 1.0 : 0.0)
        }
        .frame(width: iconSize + 20)
        .contentShape(Rectangle())
        .onHover { hover in
            isHovered = hover
        }
        .onTapGesture {
            onLaunch()
        }
        // Right-Click Context Menu
        .contextMenu {
            Button("Open App") {
                onLaunch()
            }
            Divider()
            Menu("Folder Actions") {
                Button("Move to New Folder") {
                    onCreateFolder()
                }
                Button("Remove from Folder") {
                    onRemoveFromFolder()
                }
                
                let folders = AppScanner.getFoldersData()
                if !folders.isEmpty {
                    Divider()
                    ForEach(folders, id: \.id) { folder in
                        Button("Add to '\(folder.name)'") {
                            onMoveToFolder(folder)
                        }
                    }
                }
            }
            Divider()
            Button("Get Info") {
                onShowInfo()
            }
            Button("Show in Finder") {
                NSWorkspace.shared.selectFile(app.path, inFileViewerRootedAtPath: "")
            }
            Divider()
            Button("Hide App") {
                onHide()
            }
            Button("Move to Trash") {
                onMoveToTrash()
            }
        }
    }
}

// MARK: - Folder Cell Renderer

struct FolderIconCell: View {
    let folder: FolderItem
    let isFocused: Bool
    let isMergeTarget: Bool
    let iconSize: CGFloat
    let onOpen: () -> Void
    let onDisband: () -> Void
    let onRename: (String) -> Void
    
    @AppStorage("LaunchpadShowLabels") private var showLabels: Bool = true
    @State private var isHovered = false
    
    var body: some View {
        VStack(spacing: 8) {
            ZStack(alignment: .topLeading) {
                // Folder Icon container
                FolderMiniGrid(apps: folder.apps, iconSize: iconSize)
                    .scaleEffect(isMergeTarget ? 1.18 : (isHovered ? 1.08 : 1.0))
                    .shadow(color: isFocused ? Color.blue.opacity(0.5) : (isMergeTarget ? Color.green.opacity(0.5) : (isHovered ? .white.opacity(0.18) : .black.opacity(0.15))),
                            radius: isFocused ? 14 : (isMergeTarget ? 16 : (isHovered ? 12 : 5)),
                            x: 0,
                            y: isHovered ? 6 : 3)
                    .overlay(
                        RoundedRectangle(cornerRadius: 18)
                            .stroke(isMergeTarget ? Color.green.opacity(0.85) : Color.blue.opacity(0.85), lineWidth: 3.5)
                            .scaleEffect(1.1)
                            .blur(radius: 0.5)
                            .opacity(isFocused || isMergeTarget ? 1.0 : 0.0)
                    )
                    .animation(.spring(response: 0.22, dampingFraction: 0.65), value: isHovered)
                    .animation(.spring(response: 0.25, dampingFraction: 0.7), value: isFocused)
                    .animation(.spring(response: 0.25, dampingFraction: 0.7), value: isMergeTarget)
            }
            
            // Folder label
            Text(folder.name)
                .foregroundColor(.white)
                .font(.system(size: 12, weight: .semibold))
                .multilineTextAlignment(.center)
                .lineLimit(2)
                .frame(height: 32, alignment: .top)
                .shadow(color: .black.opacity(0.65), radius: 3, x: 0, y: 1.5)
                .opacity(showLabels ? 1.0 : 0.0)
        }
        .frame(width: iconSize + 20)
        .contentShape(Rectangle())
        .onHover { hover in
            isHovered = hover
        }
        .onTapGesture {
            onOpen()
        }
        .contextMenu {
            Button("Open Folder") {
                onOpen()
            }
            Divider()
            Button("Disband Folder") {
                onDisband()
            }
        }
    }
}

// MARK: - Mini App grid inside Folder Icon

struct FolderMiniGrid: View {
    let apps: [AppInfo]
    let iconSize: CGFloat
    
    var body: some View {
        // 3x3 preview that always fits the tile: 3 minis + 2 gaps + 2 pads <= iconSize
        let mini = iconSize * 0.24
        let gap = iconSize * 0.05
        let pad = (iconSize - 3 * mini - 2 * gap) / 2
        let gridItems = Array(repeating: GridItem(.fixed(mini), spacing: gap), count: 3)
        
        LazyVGrid(columns: gridItems, spacing: gap) {
            ForEach(apps.prefix(9)) { app in
                Image(nsImage: app.icon)
                    .resizable()
                    .frame(width: mini, height: mini)
                    .cornerRadius(mini * 0.18)
            }
            // Retain spacing consistency
            if apps.count < 9 {
                ForEach(0..<(9 - apps.count), id: \.self) { _ in
                    Color.clear
                        .frame(width: mini, height: mini)
                }
            }
        }
        .padding(pad)
        .frame(width: iconSize, height: iconSize)
        .background(
            // Light frosted tile like native Launchpad folders, instead of a dark hole
            RoundedRectangle(cornerRadius: iconSize * 0.225, style: .continuous)
                .fill(Color.white.opacity(0.16))
                .background(
                    VisualEffectView()
                        .clipShape(RoundedRectangle(cornerRadius: iconSize * 0.225, style: .continuous))
                )
                .overlay(
                    RoundedRectangle(cornerRadius: iconSize * 0.225, style: .continuous)
                        .stroke(Color.white.opacity(0.22), lineWidth: 1)
                )
        )
    }
}

// MARK: - Paged Dot Control

struct PageControlView: View {
    let numberOfPages: Int
    @Binding var currentPage: Int
    let dragOffset: CGFloat
    let screenWidth: CGFloat
    
    var body: some View {
        let fractionalPage = CGFloat(currentPage) - (dragOffset / max(1.0, screenWidth))
        
        HStack(spacing: 12) {
            ForEach(0..<numberOfPages, id: \.self) { index in
                let distance = abs(fractionalPage - CGFloat(index))
                let activeProgress = max(0.0, min(1.0, 1.0 - distance))
                let opacity = 0.3 + (activeProgress * 0.7)
                let scale = 1.0 + (activeProgress * 0.25)
                
                Circle()
                    .fill(Color.white.opacity(opacity))
                    .frame(width: 8, height: 8)
                    .scaleEffect(scale)
                    .onTapGesture {
                        withAnimation(.spring(response: 0.35, dampingFraction: 0.8)) {
                            currentPage = index
                        }
                    }
            }
        }
    }
}

// MARK: - Folder Expansion Overlay View

struct FolderTitleTextField: NSViewRepresentable {
    @Binding var text: String
    @Binding var isFocused: Bool
    var onCommit: () -> Void
    
    class Coordinator: NSObject, NSTextFieldDelegate {
        var parent: FolderTitleTextField
        
        init(_ parent: FolderTitleTextField) {
            self.parent = parent
        }
        
        func controlTextDidChange(_ obj: Notification) {
            if let textField = obj.object as? NSTextField {
                parent.text = textField.stringValue
            }
        }
        
        func control(_ control: NSControl, textView: NSTextView, doCommandBy commandSelector: Selector) -> Bool {
            if commandSelector == #selector(NSResponder.insertNewline(_:)) {
                parent.isFocused = false
                parent.onCommit()
                return true
            }
            return false
        }
        
        func controlTextDidBeginEditing(_ obj: Notification) {
            parent.isFocused = true
        }
        
        func controlTextDidEndEditing(_ obj: Notification) {
            parent.isFocused = false
            parent.onCommit()
        }
    }
    
    func makeCoordinator() -> Coordinator {
        Coordinator(self)
    }
    
    func makeNSView(context: Context) -> NSTextField {
        let textField = NSTextField()
        textField.delegate = context.coordinator
        textField.isBordered = false
        textField.drawsBackground = false
        textField.focusRingType = .none
        textField.textColor = .white
        textField.font = .systemFont(ofSize: 30, weight: .medium)
        textField.alignment = .center
        textField.isEditable = true
        textField.isSelectable = true
        return textField
    }
    
    func updateNSView(_ nsView: NSTextField, context: Context) {
        if nsView.stringValue != text {
            nsView.stringValue = text
        }
        
        DispatchQueue.main.async {
            if isFocused {
                if let window = nsView.window, window.firstResponder != nsView.currentEditor() {
                    window.makeFirstResponder(nsView)
                }
            } else {
                if let window = nsView.window, window.firstResponder == nsView.currentEditor() {
                    window.makeFirstResponder(nil)
                }
            }
        }
    }
}

struct FolderOverlayView: View {
    let folder: FolderItem
    let iconSize: CGFloat
    let focusedIndex: Int?
    let onClose: () -> Void
    let onRename: (String) -> Void
    let onLaunchApp: (AppInfo) -> Void
    let onRemoveApp: (AppInfo) -> Void
    
    @State private var folderName: String = ""
    @State private var isRenameFocused: Bool = false
    @State private var isTitleHovered: Bool = false
    
    /// Columns adapt to the app count so small folders get a snug panel
    /// instead of swimming inside a fixed-size box.
    private var columnCount: Int { min(max(folder.apps.count, 1), 5) }
    private var rowCount: Int { (folder.apps.count + columnCount - 1) / columnCount }
    
    // Layout metrics (kept as simple stored expressions so the
    // type-checker never has to chew through one giant body)
    private var cellWidth: CGFloat { iconSize + 20 }
    private let columnSpacing: CGFloat = 22
    private let rowSpacing: CGFloat = 26
    
    var body: some View {
        GeometryReader { geom in
            ZStack {
                backdrop
                
                VStack(spacing: 18) {
                    titleView
                    panel(screenWidth: geom.size.width)
                }
            }
            .onAppear {
                folderName = folder.name
            }
            .onChange(of: folder) { _, newFolder in
                folderName = newFolder.name
            }
        }
    }
    
    /// Dim backdrop: click closes; dropping an icon here pulls it out of the folder
    private var backdrop: some View {
        Color.black.opacity(0.35)
            .edgesIgnoringSafeArea(.all)
            .onTapGesture {
                onClose()
            }
            .onDrop(of: [UTType.text.identifier], isTargeted: nil) { providers in
                if let provider = providers.first {
                    provider.loadItem(forTypeIdentifier: UTType.text.identifier, options: nil) { (data, error) in
                        // Handle both Data (UTF8 bytes) and direct String from NSItemProvider
                        var path: String? = nil
                        if let data = data as? Data {
                            path = String(data: data, encoding: .utf8)
                        } else if let str = data as? String {
                            path = str
                        }
                        
                        if let appPath = path {
                            DispatchQueue.main.async {
                                if let appToRemove = folder.apps.first(where: { $0.path == appPath }) {
                                    withAnimation(.spring(response: 0.35, dampingFraction: 0.8)) {
                                        onRemoveApp(appToRemove)
                                    }
                                }
                            }
                        }
                    }
                }
                return true
            }
    }
    
    /// Floating title above the panel, like native Launchpad.
    /// Click it to rename — no pencil chrome needed.
    private var titleView: some View {
        ZStack {
            // Hidden text mirrors the field's content to size it dynamically
            Text((folderName.isEmpty ? " " : folderName) + " ")
                .font(.system(size: 30, weight: .medium))
                .opacity(0)
                .padding(.horizontal, 18)
                .frame(height: 44)
                .frame(minWidth: 140)
            
            FolderTitleTextField(text: $folderName, isFocused: $isRenameFocused, onCommit: {
                let trimmed = folderName.trimmingCharacters(in: .whitespacesAndNewlines)
                if !trimmed.isEmpty {
                    onRename(trimmed)
                } else {
                    folderName = folder.name
                }
            })
            .frame(height: 44)
        }
        .fixedSize(horizontal: true, vertical: false)
        .background(
            RoundedRectangle(cornerRadius: 10, style: .continuous)
                .fill(Color.white.opacity(isRenameFocused ? 0.14 : (isTitleHovered ? 0.08 : 0)))
        )
        .onHover { hovering in
            withAnimation(.easeOut(duration: 0.15)) {
                isTitleHovered = hovering
            }
        }
        .shadow(color: .black.opacity(0.5), radius: 6, x: 0, y: 2)
    }
    
    /// Content-sized glass panel; tall folders scroll beyond three rows
    private func panel(screenWidth: CGFloat) -> some View {
        let rawWidth: CGFloat = CGFloat(columnCount) * cellWidth + CGFloat(max(columnCount - 1, 0)) * columnSpacing + 72
        let panelWidth: CGFloat = min(rawWidth, screenWidth * 0.8)
        let needsScroll: Bool = rowCount > 3
        let visibleRows: Int = min(rowCount, 3)
        let rowHeight: CGFloat = iconSize + 26
        let gridHeight: CGFloat = CGFloat(visibleRows) * rowHeight + CGFloat(max(visibleRows - 1, 0)) * rowSpacing
        
        return Group {
            if needsScroll {
                ScrollView(showsIndicators: false) {
                    appGrid
                }
                .frame(height: gridHeight)
            } else {
                appGrid
            }
        }
        .padding(.horizontal, 36)
        .padding(.vertical, 30)
        .frame(width: panelWidth)
        .background(panelBackground)
        .shadow(color: Color.black.opacity(0.4), radius: 30, x: 0, y: 15)
        .onDrop(of: [UTType.text.identifier], isTargeted: nil) { _ in
            // Intercept drops inside the panel so they don't reach the
            // backdrop and accidentally remove the app from the folder.
            return true
        }
    }
    
    private var appGrid: some View {
        let folderColumns = Array(repeating: GridItem(.fixed(cellWidth), spacing: columnSpacing), count: columnCount)
        
        return LazyVGrid(columns: folderColumns, spacing: rowSpacing) {
            ForEach(0..<folder.apps.count, id: \.self) { idx in
                let app = folder.apps[idx]
                let isFocused = (focusedIndex == idx)
                
                InnerFolderAppCell(
                    app: app,
                    iconSize: iconSize,
                    isFocused: isFocused,
                    onLaunch: { onLaunchApp(app) },
                    onRemove: { onRemoveApp(app) }
                )
            }
        }
    }
    
    private var panelBackground: some View {
        RoundedRectangle(cornerRadius: 28, style: .continuous)
            .fill(Color.black.opacity(0.25))
            .background(
                VisualEffectView()
                    .clipShape(RoundedRectangle(cornerRadius: 28, style: .continuous))
            )
            .overlay(
                RoundedRectangle(cornerRadius: 28, style: .continuous)
                    .stroke(Color.white.opacity(0.15), lineWidth: 1)
            )
    }
}

struct InnerFolderAppCell: View {
    let app: AppInfo
    let iconSize: CGFloat
    let isFocused: Bool
    let onLaunch: () -> Void
    let onRemove: () -> Void
    
    @State private var isHovered = false
    
    var body: some View {
        Button(action: onLaunch) {
            VStack(spacing: 8) {
                Image(nsImage: app.icon)
                    .resizable()
                    .frame(width: iconSize, height: iconSize)
                    .scaleEffect(isHovered ? 1.08 : 1.0)
                    .shadow(color: isFocused ? Color.blue.opacity(0.5) : (isHovered ? .white.opacity(0.15) : .black.opacity(0.15)),
                            radius: isFocused ? 12 : (isHovered ? 10 : 4),
                            x: 0, y: isHovered ? 4 : 2)
                    .overlay(
                        RoundedRectangle(cornerRadius: 18)
                            .stroke(Color.blue.opacity(0.8), lineWidth: 3)
                            .scaleEffect(1.1)
                            .blur(radius: 0.5)
                            .opacity(isFocused ? 1.0 : 0.0)
                    )
                    .animation(.spring(response: 0.22, dampingFraction: 0.65), value: isHovered)
                
                Text(app.name)
                    .foregroundColor(.white)
                    .font(.system(size: 12, weight: .medium))
                    .multilineTextAlignment(.center)
                    .lineLimit(1)
                    .frame(height: 18)
            }
            .frame(width: iconSize + 20)
        }
        .buttonStyle(.plain)
        .onHover { hover in
            isHovered = hover
        }
        .onDrag {
            return NSItemProvider(object: app.path as NSString)
        }
        .contextMenu {
            Button("Launch App") { onLaunch() }
            Divider()
            Button("Remove from Folder") { onRemove() }
        }
    }
}

// MARK: - Skeleton Shimmer Grid Component

struct SkeletonGridView: View {
    let columns: Int
    @State private var shimmerOffset: CGFloat = -1.5
    
    var body: some View {
        let gridItems = Array(repeating: GridItem(.flexible(), spacing: 20), count: columns)
        
        LazyVGrid(columns: gridItems, spacing: 30) {
            ForEach(0..<21, id: \.self) { _ in
                VStack(spacing: 8) {
                    ZStack {
                        RoundedRectangle(cornerRadius: 18)
                            .fill(Color.white.opacity(0.08))
                            .frame(width: 80, height: 80)
                        
                        // Shimmer highlight effect
                        GeometryReader { proxy in
                            let w = proxy.size.width
                            
                            RoundedRectangle(cornerRadius: 18)
                                .fill(
                                    LinearGradient(
                                        gradient: Gradient(colors: [.clear, Color.white.opacity(0.12), .clear]),
                                        startPoint: .topLeading,
                                        endPoint: .bottomTrailing
                                    )
                                )
                                .offset(x: shimmerOffset * w)
                                .blendMode(.screen)
                        }
                        .frame(width: 80, height: 80)
                        .clipShape(RoundedRectangle(cornerRadius: 18))
                    }
                    
                    RoundedRectangle(cornerRadius: 4)
                        .fill(Color.white.opacity(0.08))
                        .frame(width: 60, height: 12)
                }
            }
        }
        .padding(.horizontal, 80)
        .padding(.vertical, 20)
        .onAppear {
            withAnimation(Animation.linear(duration: 1.5).repeatForever(autoreverses: false)) {
                shimmerOffset = 1.5
            }
        }
    }
}

// MARK: - Premium Dock View Component





// MARK: - AppKit Bridge Search TextField
struct SearchTextField: NSViewRepresentable {
    @Binding var text: String
    @Binding var isFocused: Bool
    var placeholder: String
    var onCommit: (() -> Void)? = nil
    
    class Coordinator: NSObject, NSTextFieldDelegate {
        var parent: SearchTextField
        
        init(_ parent: SearchTextField) {
            self.parent = parent
        }
        
        func controlTextDidChange(_ obj: Notification) {
            if let textField = obj.object as? NSTextField {
                parent.text = textField.stringValue
            }
        }
        
        func control(_ control: NSControl, textView: NSTextView, doCommandBy commandSelector: Selector) -> Bool {
            if commandSelector == #selector(NSResponder.insertNewline(_:)) {
                parent.onCommit?()
                return true
            }
            return false
        }
    }
    
    func makeCoordinator() -> Coordinator {
        Coordinator(self)
    }
    
    func makeNSView(context: Context) -> NSTextField {
        let textField = NSTextField()
        textField.delegate = context.coordinator
        textField.isBordered = false
        textField.drawsBackground = false
        textField.focusRingType = .none
        textField.textColor = .white
        textField.font = .systemFont(ofSize: 13, weight: .medium)
        textField.isEditable = true
        textField.isSelectable = true
        
        let placeholderAttr = NSAttributedString(
            string: placeholder,
            attributes: [
                .foregroundColor: NSColor.white.withAlphaComponent(0.5),
                .font: NSFont.systemFont(ofSize: 13, weight: .medium)
            ]
        )
        textField.placeholderAttributedString = placeholderAttr
        
        return textField
    }
    
    func updateNSView(_ nsView: NSTextField, context: Context) {
        if nsView.stringValue != text {
            nsView.stringValue = text
        }
        
        DispatchQueue.main.async {
            if isFocused {
                if let window = nsView.window, window.firstResponder != nsView.currentEditor() {
                    window.makeFirstResponder(nsView)
                    if let editor = nsView.currentEditor() {
                        editor.selectedRange = NSRange(location: nsView.stringValue.count, length: 0)
                    }
                }
            } else {
                if let window = nsView.window, window.firstResponder == nsView.currentEditor() {
                    window.makeFirstResponder(nil)
                }
            }
        }
    }
}

struct SettingsCardOverlayView: View {
    @Binding var columnsCount: Int
    @Binding var rowsCount: Int
    @Binding var iconSizeSelection: Double
    @Binding var bgTintOpacity: Double
    @Binding var showLabels: Bool
    @Binding var showSuggestions: Bool
    @Binding var persistentMode: Bool
    @Binding var showDockIcon: Bool
    @Binding var showMenuBarIcon: Bool
    @Binding var launchAtLogin: Bool
    @Binding var pinchGestures: Bool
    @Binding var selectedTab: Int
    
    let onAutoCategorize: () -> Void
    let onResetAll: () -> Void
    let onClose: () -> Void
    
    @State private var isCheckingForUpdates = false
    @State private var updateStatus: String? = nil
    @State private var availableUpdateURL: URL? = nil
    
    var body: some View {
        VStack(spacing: 0) {
            // Tab pills
            HStack(spacing: 6) {
                tabPill(tag: 0, title: "Layout", symbol: "square.grid.3x3")
                tabPill(tag: 1, title: "Behavior", symbol: "gearshape")
            }
            .padding(.top, 14)
            .padding(.bottom, 12)
            .frame(maxWidth: .infinity)
            
            Divider()
                .background(Color.white.opacity(0.1))
            
            // Content sizes to each tab naturally; the card animates between heights
            Group {
                if selectedTab == 0 {
                    layoutTab
                } else {
                    behaviorTab
                }
            }
            .padding(18)
            .frame(maxWidth: .infinity, alignment: .leading)
            
            Divider()
                .background(Color.white.opacity(0.1))
            
            // Footer Actions
            HStack(spacing: 12) {
                if selectedTab == 0 {
                    Button(action: onAutoCategorize) {
                        HStack(spacing: 4) {
                            Image(systemName: "sparkles")
                                .font(.system(size: 10))
                            Text("Auto-Categorize")
                                .font(.system(size: 11, weight: .medium))
                        }
                    }
                    .buttonStyle(SettingsOverlayButtonStyle(isDefault: false))
                }
                
                Spacer()
                
                Button("Reset Defaults", action: onResetAll)
                    .buttonStyle(SettingsOverlayButtonStyle(isDefault: false))
                
                Button("Done", action: onClose)
                    .buttonStyle(SettingsOverlayButtonStyle(isDefault: true))
            }
            .padding(.horizontal, 16)
            .padding(.vertical, 12)
        }
        .frame(width: 380)
        .background(
            // Same glass recipe as the folder panel so overlays feel related
            RoundedRectangle(cornerRadius: 24, style: .continuous)
                .fill(Color.black.opacity(0.3))
                .background(
                    VisualEffectView()
                        .clipShape(RoundedRectangle(cornerRadius: 24, style: .continuous))
                )
                .overlay(
                    RoundedRectangle(cornerRadius: 24, style: .continuous)
                        .stroke(Color.white.opacity(0.15), lineWidth: 1)
                )
        )
        .shadow(color: Color.black.opacity(0.4), radius: 30, x: 0, y: 15)
        .animation(.spring(response: 0.35, dampingFraction: 0.85), value: selectedTab)
        .animation(.spring(response: 0.35, dampingFraction: 0.85), value: persistentMode)
    }
    
    // MARK: - Tabs
    
    private var layoutTab: some View {
        VStack(alignment: .leading, spacing: 12) {
            sectionHeader("Grid")
            
            HStack {
                Text("Columns")
                    .font(.system(size: 13))
                    .foregroundColor(.white.opacity(0.85))
                Spacer()
                Stepper(value: $columnsCount, in: 4...10) {
                    Text("\(columnsCount)")
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundColor(.white)
                        .frame(width: 24, alignment: .center)
                }
                .controlSize(.small)
            }
            
            HStack {
                Text("Rows")
                    .font(.system(size: 13))
                    .foregroundColor(.white.opacity(0.85))
                Spacer()
                Stepper(value: $rowsCount, in: 3...7) {
                    Text("\(rowsCount)")
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundColor(.white)
                        .frame(width: 24, alignment: .center)
                }
                .controlSize(.small)
            }
            
            VStack(alignment: .leading, spacing: 4) {
                HStack {
                    Text("Icon Size")
                        .font(.system(size: 13))
                        .foregroundColor(.white.opacity(0.85))
                    Spacer()
                    Text("\(Int(iconSizeSelection)) pt")
                        .font(.system(size: 12, weight: .semibold))
                        .foregroundColor(.white.opacity(0.7))
                }
                
                HStack(spacing: 8) {
                    Image(systemName: "app")
                        .font(.system(size: 10))
                        .foregroundColor(.white.opacity(0.5))
                    Slider(value: $iconSizeSelection, in: 64...112, step: 8)
                        .controlSize(.small)
                        .tint(.blue)
                    Image(systemName: "app")
                        .font(.system(size: 16))
                        .foregroundColor(.white.opacity(0.5))
                }
            }
            
            sectionHeader("Appearance")
                .padding(.top, 8)
            
            VStack(alignment: .leading, spacing: 4) {
                HStack {
                    Text("Background Dim")
                        .font(.system(size: 13))
                        .foregroundColor(.white.opacity(0.85))
                    Spacer()
                    Text("\(Int(bgTintOpacity * 100))%")
                        .font(.system(size: 12, weight: .semibold))
                        .foregroundColor(.white.opacity(0.7))
                }
                
                HStack(spacing: 8) {
                    Image(systemName: "sun.max")
                        .font(.system(size: 11))
                        .foregroundColor(.white.opacity(0.5))
                    Slider(value: $bgTintOpacity, in: 0...0.6, step: 0.05)
                        .controlSize(.small)
                        .tint(.blue)
                    Image(systemName: "moon")
                        .font(.system(size: 11))
                        .foregroundColor(.white.opacity(0.5))
                }
            }
            
            settingToggle("Show Icon Labels", isOn: $showLabels)
            
            settingToggle("Show Suggestions Row", isOn: $showSuggestions)
                .help("Shows your most-used apps above the grid once you've launched a few.")
        }
        .transition(.opacity)
    }
    
    private var behaviorTab: some View {
        VStack(alignment: .leading, spacing: 12) {
            sectionHeader("General")
            
            settingToggle("Keep Running in Background", isOn: $persistentMode)
                .help("Keeps Launchpad resident in the background, allowing it to be toggled instantly with the global hotkey.")
            
            if persistentMode {
                VStack(alignment: .leading, spacing: 10) {
                    settingToggle("Show Dock Icon", isOn: $showDockIcon, subdued: true)
                    settingToggle("Show Menu Bar Icon", isOn: $showMenuBarIcon, subdued: true)
                }
                .padding(.leading, 18)
                .transition(.move(edge: .top).combined(with: .opacity))
            }
            
            settingToggle("Launch at Login", isOn: $launchAtLogin)
                .help("Registers Relaunched as a login item so the hotkey works right after startup.")
            
            sectionHeader("Shortcuts")
                .padding(.top, 8)
            
            HotkeyRecorderRow()
            
            settingToggle("Trackpad Pinch to Open & Close", isOn: $pinchGestures)
                .help("Pinch in with thumb and three fingers to open Launchpad; spread them apart to close — just like classic macOS.")
                .disabled(!TrackpadGestureManager.isSupported)
            
            if !TrackpadGestureManager.isSupported {
                Text("No multitouch trackpad detected")
                    .font(.system(size: 11))
                    .foregroundColor(.white.opacity(0.45))
                    .padding(.leading, 2)
            }
            
            sectionHeader("Updates")
                .padding(.top, 8)
            
            HStack(spacing: 8) {
                Button(action: checkForUpdates) {
                    Text(isCheckingForUpdates ? "Checking..." : "Check for Updates")
                        .font(.system(size: 11, weight: .semibold))
                }
                .buttonStyle(SettingsOverlayButtonStyle(isDefault: false))
                .disabled(isCheckingForUpdates)
                
                Spacer()
                
                if let status = updateStatus {
                    Text(status)
                        .font(.system(size: 11))
                        .foregroundColor(.white.opacity(0.7))
                        .lineLimit(1)
                }
                
                if let url = availableUpdateURL {
                    Button("View") {
                        NSWorkspace.shared.open(url)
                    }
                    .buttonStyle(SettingsOverlayButtonStyle(isDefault: true))
                }
            }
        }
        .transition(.opacity)
    }
    
    // MARK: - Building blocks
    
    private func tabPill(tag: Int, title: String, symbol: String) -> some View {
        Button {
            selectedTab = tag
        } label: {
            HStack(spacing: 5) {
                Image(systemName: symbol)
                    .font(.system(size: 11, weight: .medium))
                Text(title)
                    .font(.system(size: 12, weight: .semibold))
            }
            .foregroundColor(selectedTab == tag ? .white : .white.opacity(0.55))
            .padding(.vertical, 6)
            .padding(.horizontal, 14)
            .background(
                Capsule()
                    .fill(Color.white.opacity(selectedTab == tag ? 0.16 : 0))
            )
            .contentShape(Capsule())
        }
        .buttonStyle(.plain)
    }
    
    private func sectionHeader(_ title: String) -> some View {
        Text(title.uppercased())
            .font(.system(size: 10.5, weight: .semibold))
            .tracking(0.8)
            .foregroundColor(.white.opacity(0.4))
    }
    
    private func settingToggle(_ title: String, isOn: Binding<Bool>, subdued: Bool = false) -> some View {
        Toggle(title, isOn: isOn)
            .toggleStyle(.switch)
            .controlSize(.small)
            .tint(.blue)
            .foregroundColor(.white.opacity(subdued ? 0.75 : 0.85))
            .font(.system(size: subdued ? 12 : 13))
    }
    
    // MARK: - Update Check
    
    private func checkForUpdates() {
        isCheckingForUpdates = true
        updateStatus = nil
        availableUpdateURL = nil
        
        guard let url = URL(string: "https://api.github.com/repos/rhysx19/Relaunched/releases/latest") else {
            isCheckingForUpdates = false
            updateStatus = "Could not check"
            return
        }
        
        URLSession.shared.dataTask(with: url) { data, _, _ in
            var status = "Could not check for updates"
            var updateURL: URL? = nil
            
            if let data = data,
               let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
               let tag = json["tag_name"] as? String {
                let latest = tag.hasPrefix("v") ? String(tag.dropFirst()) : tag
                let current = Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String ?? "1.0"
                
                if latest.compare(current, options: .numeric) == .orderedDescending {
                    status = "v\(latest) is available"
                    updateURL = URL(string: (json["html_url"] as? String) ?? "https://github.com/rhysx19/Relaunched/releases")
                } else {
                    status = "Up to date (v\(current))"
                }
            }
            
            DispatchQueue.main.async {
                isCheckingForUpdates = false
                updateStatus = status
                availableUpdateURL = updateURL
            }
        }.resume()
    }
}

// MARK: - Global Hotkey Recorder Row

struct HotkeyRecorderRow: View {
    @State private var isRecording = false
    @State private var display: String = HotkeyPreference.load().display
    @State private var keyMonitor: Any? = nil
    
    var body: some View {
        HStack(spacing: 8) {
            Text("Global Hotkey")
                .font(.system(size: 13))
                .foregroundColor(.white.opacity(0.85))
            
            Spacer()
            
            Button(action: toggleRecording) {
                Text(isRecording ? "Press shortcut..." : display)
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundColor(isRecording ? .blue : .white)
                    .frame(minWidth: 96)
                    .padding(.vertical, 5)
                    .padding(.horizontal, 10)
                    .background(
                        RoundedRectangle(cornerRadius: 7, style: .continuous)
                            .fill(Color.white.opacity(isRecording ? 0.22 : 0.12))
                            .overlay(
                                RoundedRectangle(cornerRadius: 7, style: .continuous)
                                    .stroke(isRecording ? Color.blue.opacity(0.8) : Color.white.opacity(0.1), lineWidth: 1)
                            )
                    )
            }
            .buttonStyle(.plain)
            .help("Click, then press the new shortcut. It must include ⌘, ⌥ or ⌃. Esc cancels.")
            
            Button(action: resetToDefault) {
                Image(systemName: "arrow.counterclockwise")
                    .font(.system(size: 11, weight: .semibold))
                    .foregroundColor(.white.opacity(0.6))
            }
            .buttonStyle(.plain)
            .help("Reset to ⌥ Space")
        }
        .onDisappear {
            stopRecording()
        }
    }
    
    private func toggleRecording() {
        if isRecording {
            stopRecording()
            return
        }
        
        isRecording = true
        NotificationCenter.default.post(name: NSNotification.Name("LaunchpadHotkeyRecordingBegan"), object: nil)
        
        keyMonitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { event in
            if event.keyCode == 53 { // Esc cancels recording
                stopRecording()
                return nil
            }
            
            // Require a real modifier so the global hotkey can't hijack plain typing
            let flags = event.modifierFlags.intersection([.command, .option, .control])
            guard !flags.isEmpty else {
                NSSound.beep()
                return nil
            }
            
            let preference = HotkeyPreference.from(event: event)
            preference.save()
            display = preference.display
            stopRecording()
            
            // Re-register the Carbon hotkey with the new combination
            NotificationCenter.default.post(name: NSNotification.Name("LaunchpadSettingsChanged"), object: nil)
            return nil
        }
    }
    
    private func stopRecording() {
        if let monitor = keyMonitor {
            NSEvent.removeMonitor(monitor)
            keyMonitor = nil
        }
        if isRecording {
            isRecording = false
            NotificationCenter.default.post(name: NSNotification.Name("LaunchpadHotkeyRecordingEnded"), object: nil)
        }
    }
    
    private func resetToDefault() {
        stopRecording()
        HotkeyPreference.reset()
        display = HotkeyPreference.default.display
        NotificationCenter.default.post(name: NSNotification.Name("LaunchpadSettingsChanged"), object: nil)
    }
}

// MARK: - Suggestions Row Cell

struct SuggestionCell: View {
    let app: AppInfo
    let onLaunch: () -> Void
    
    @State private var isHovered = false
    
    var body: some View {
        VStack(spacing: 5) {
            Image(nsImage: app.icon)
                .resizable()
                .frame(width: 46, height: 46)
                .scaleEffect(isHovered ? 1.12 : 1.0)
                .shadow(color: isHovered ? .white.opacity(0.18) : .black.opacity(0.2),
                        radius: isHovered ? 9 : 4, x: 0, y: 3)
                .animation(.spring(response: 0.22, dampingFraction: 0.65), value: isHovered)
            
            Text(app.name)
                .foregroundColor(.white.opacity(0.75))
                .font(.system(size: 10, weight: .medium))
                .lineLimit(1)
                .frame(maxWidth: 70)
                .shadow(color: .black.opacity(0.6), radius: 2, x: 0, y: 1)
        }
        .contentShape(Rectangle())
        .onHover { hover in
            isHovered = hover
        }
        .onTapGesture {
            onLaunch()
        }
    }
}

// MARK: - App Get Info Card

struct AppInfoCardView: View {
    let app: AppInfo
    let onClose: () -> Void
    
    var body: some View {
        let bundle = Bundle(path: app.path)
        let version = bundle?.infoDictionary?["CFBundleShortVersionString"] as? String
        let build = bundle?.infoDictionary?["CFBundleVersion"] as? String
        let bundleID = bundle?.bundleIdentifier
        let category = AppScanner.getAppCategory(at: app.path)
        let launchCount = AppScanner.getLaunchCount(path: app.path)
        
        VStack(spacing: 18) {
            Image(nsImage: app.icon)
                .resizable()
                .frame(width: 96, height: 96)
                .shadow(color: .black.opacity(0.35), radius: 12, x: 0, y: 6)
            
            VStack(spacing: 4) {
                Text(app.name)
                    .font(.system(size: 22, weight: .bold))
                    .foregroundColor(.white)
                
                if let version = version {
                    Text("Version \(version)" + (build.map { " (\($0))" } ?? ""))
                        .font(.system(size: 12))
                        .foregroundColor(.white.opacity(0.6))
                }
            }
            
            VStack(alignment: .leading, spacing: 8) {
                InfoRow(label: "Category", value: category)
                if let bundleID = bundleID {
                    InfoRow(label: "Bundle ID", value: bundleID)
                }
                InfoRow(label: "Location", value: app.path)
                InfoRow(label: "Opened from here", value: launchCount == 1 ? "1 time" : "\(launchCount) times")
            }
            .padding(14)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(
                RoundedRectangle(cornerRadius: 12, style: .continuous)
                    .fill(Color.white.opacity(0.06))
            )
            
            HStack(spacing: 12) {
                Button("Show in Finder") {
                    NSWorkspace.shared.selectFile(app.path, inFileViewerRootedAtPath: "")
                }
                .buttonStyle(SettingsOverlayButtonStyle(isDefault: false))
                
                Button("Close", action: onClose)
                    .buttonStyle(SettingsOverlayButtonStyle(isDefault: true))
            }
        }
        .padding(28)
        .frame(width: 420)
        .background(
            RoundedRectangle(cornerRadius: 24, style: .continuous)
                .fill(Color.black.opacity(0.45))
                .background(
                    VisualEffectView()
                        .clipShape(RoundedRectangle(cornerRadius: 24, style: .continuous))
                )
                .overlay(
                    RoundedRectangle(cornerRadius: 24, style: .continuous)
                        .stroke(Color.white.opacity(0.14), lineWidth: 1)
                )
        )
        .shadow(color: Color.black.opacity(0.4), radius: 26, x: 0, y: 14)
        .onTapGesture {
            // Absorb taps so the dim backdrop behind doesn't dismiss the card
        }
    }
}

struct InfoRow: View {
    let label: String
    let value: String
    
    var body: some View {
        HStack(alignment: .top, spacing: 10) {
            Text(label)
                .font(.system(size: 11, weight: .semibold))
                .foregroundColor(.white.opacity(0.5))
                .frame(width: 110, alignment: .leading)
            
            Text(value)
                .font(.system(size: 11))
                .foregroundColor(.white.opacity(0.85))
                .lineLimit(2)
                .truncationMode(.middle)
                .textSelection(.enabled)
        }
    }
}

struct SettingsOverlayButtonStyle: ButtonStyle {
    let isDefault: Bool
    
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 11, weight: .semibold))
            .foregroundColor(.white)
            .padding(.horizontal, 12)
            .padding(.vertical, 6)
            .background(
                RoundedRectangle(cornerRadius: 8, style: .continuous)
                    .fill(isDefault ? Color.blue.opacity(0.85) : Color.white.opacity(0.12))
                    .opacity(configuration.isPressed ? 0.7 : 1.0)
            )
    }
}





