import Cocoa

// MARK: - Raw multitouch bindings (MultitouchSupport.framework)
//
// The classic Launchpad gesture — pinch in with thumb + three fingers to open,
// spread to close — can't be built on AppKit's `magnify` events: they carry no
// finger count, so any implementation based on them would hijack everyday
// two-finger zooming in Safari, Preview, etc.
//
// Instead we read raw trackpad touch frames from the same private framework
// the system itself uses (as do BetterTouchTool-style utilities). It needs no
// special permissions. The framework is loaded with dlopen/dlsym at runtime so
// the app keeps working (gestures just stay off) if it's ever unavailable.

fileprivate struct MTPoint {
    var x: Float
    var y: Float
}

/// Byte layout of the framework's 96-byte MTTouch struct (de-facto community
/// documented for 15+ years). Fields are read at explicit offsets rather than
/// through a mirrored Swift struct so the ABI assumptions are spelled out:
///   0  int32  frame          16 int32 identifier      32 float normalized.x
///   8  double timestamp      20 int32 state           36 float normalized.y
///                            24 int32 fingerID        ...
fileprivate enum MTTouchLayout {
    static let stride = 96
    static let stateOffset = 20
    static let normalizedXOffset = 32
    static let normalizedYOffset = 36
    /// Touch state value meaning "finger fully in contact with the pad".
    static let stateTouching: Int32 = 4
}

fileprivate typealias MTContactCallbackFunction = @convention(c) (
    UnsafeMutableRawPointer?,   // device
    UnsafeMutableRawPointer?,   // MTTouch array
    Int32,                      // touch count
    Double,                     // timestamp
    Int32                       // frame id
) -> Int32

fileprivate typealias MTDeviceCreateListFn = @convention(c) () -> Unmanaged<CFMutableArray>?
fileprivate typealias MTCallbackRegistrationFn = @convention(c) (UnsafeMutableRawPointer?, MTContactCallbackFunction?) -> Void
fileprivate typealias MTDeviceStartFn = @convention(c) (UnsafeMutableRawPointer?, Int32) -> Void
fileprivate typealias MTDeviceStopFn = @convention(c) (UnsafeMutableRawPointer?) -> Void

/// Lazily resolved entry points into MultitouchSupport. `nil` when the
/// framework or any symbol is missing.
fileprivate final class MTApi {
    static let shared: MTApi? = MTApi()

    let createList: MTDeviceCreateListFn
    let registerCallback: MTCallbackRegistrationFn
    let unregisterCallback: MTCallbackRegistrationFn
    let deviceStart: MTDeviceStartFn
    let deviceStop: MTDeviceStopFn

    private init?() {
        guard let handle = dlopen(
            "/System/Library/PrivateFrameworks/MultitouchSupport.framework/MultitouchSupport",
            RTLD_NOW
        ) else { return nil }

        func sym<T>(_ name: String, _ type: T.Type) -> T? {
            guard let ptr = dlsym(handle, name) else { return nil }
            return unsafeBitCast(ptr, to: T.self)
        }

        guard
            let create = sym("MTDeviceCreateList", MTDeviceCreateListFn.self),
            let register = sym("MTRegisterContactFrameCallback", MTCallbackRegistrationFn.self),
            let unregister = sym("MTUnregisterContactFrameCallback", MTCallbackRegistrationFn.self),
            let start = sym("MTDeviceStart", MTDeviceStartFn.self),
            let stop = sym("MTDeviceStop", MTDeviceStopFn.self)
        else { return nil }

        createList = create
        registerCallback = register
        unregisterCallback = unregister
        deviceStart = start
        deviceStop = stop
    }

    func copyDeviceList() -> CFMutableArray? {
        return createList()?.takeRetainedValue()
    }
}

/// One shared C-convention callback for every device (C callbacks can't
/// capture context, so frames are routed through the singleton).
fileprivate let gestureFrameCallback: MTContactCallbackFunction = { _, touchesPtr, touchCount, _, _ in
    var points: [MTPoint] = []
    if let touchesPtr = touchesPtr, touchCount > 0 {
        points.reserveCapacity(Int(touchCount))
        for i in 0..<Int(touchCount) {
            let touch = touchesPtr.advanced(by: i * MTTouchLayout.stride)
            let state = touch.load(fromByteOffset: MTTouchLayout.stateOffset, as: Int32.self)
            if state == MTTouchLayout.stateTouching {
                points.append(MTPoint(
                    x: touch.load(fromByteOffset: MTTouchLayout.normalizedXOffset, as: Float.self),
                    y: touch.load(fromByteOffset: MTTouchLayout.normalizedYOffset, as: Float.self)
                ))
            }
        }
    }
    TrackpadGestureManager.shared.processFrame(points)
    return 0
}

// MARK: - Gesture recognizer

/// Watches raw trackpad frames and fires when the user performs the classic
/// Launchpad gestures: a 4+ finger pinch-in (`onPinchIn`) or spread-out
/// (`onSpreadOut`). Four-finger swipes don't trigger it — the fingers move
/// together there, so their spread barely changes.
final class TrackpadGestureManager {
    static let shared = TrackpadGestureManager()

    /// Invoked on the main thread when a thumb + three finger pinch-in lands.
    var onPinchIn: (() -> Void)?
    /// Invoked on the main thread when a thumb + three finger spread-out lands.
    var onSpreadOut: (() -> Void)?

    /// Whether the user wants gesture monitoring on (drives wake-restart).
    private(set) var isEnabled = false

    /// True when there's at least one trackpad we can read raw touches from.
    static let isSupported: Bool = {
        guard let api = MTApi.shared, let list = api.copyDeviceList() else { return false }
        return CFArrayGetCount(list) > 0
    }()

    private var activeDevices: [UnsafeMutableRawPointer] = []
    private var deviceList: CFMutableArray?  // keeps device refs alive while running

    // Recognizer state (touched from per-device callback threads)
    private let stateLock = NSLock()
    private var gestureActive = false
    private var gestureConsumed = false
    private var initialSpread: Float = 0
    private var lastTriggerTime: TimeInterval = 0

    // Tuning
    private let pinchInRatio: Float = 0.62    // spread must shrink to 62%
    private let spreadOutRatio: Float = 1.45  // spread must grow to 145%
    private let triggerCooldown: TimeInterval = 0.75

    private init() {
        // Trackpad callbacks can go quiet after sleep; re-attach on wake.
        NSWorkspace.shared.notificationCenter.addObserver(
            forName: NSWorkspace.didWakeNotification, object: nil, queue: .main
        ) { [weak self] _ in
            guard let self = self, self.isEnabled else { return }
            self.detachDevices()
            DispatchQueue.main.asyncAfter(deadline: .now() + 1.0) {
                if self.isEnabled && self.activeDevices.isEmpty {
                    self.attachDevices()
                }
            }
        }
    }

    func start() {
        isEnabled = true
        if activeDevices.isEmpty {
            attachDevices()
        }
    }

    func stop() {
        isEnabled = false
        detachDevices()
    }

    private func attachDevices() {
        guard let api = MTApi.shared, let list = api.copyDeviceList() else { return }
        let count = CFArrayGetCount(list)
        guard count > 0 else { return }

        for i in 0..<count {
            guard let value = CFArrayGetValueAtIndex(list, i) else { continue }
            let device = UnsafeMutableRawPointer(mutating: value)
            api.registerCallback(device, gestureFrameCallback)
            api.deviceStart(device, 0)
            activeDevices.append(device)
        }
        deviceList = list
        resetRecognizer()
    }

    private func detachDevices() {
        guard let api = MTApi.shared else {
            activeDevices.removeAll()
            deviceList = nil
            return
        }
        for device in activeDevices {
            api.deviceStop(device)
            api.unregisterCallback(device, gestureFrameCallback)
        }
        activeDevices.removeAll()
        deviceList = nil
        resetRecognizer()
    }

    private func resetRecognizer() {
        stateLock.lock()
        gestureActive = false
        gestureConsumed = false
        initialSpread = 0
        stateLock.unlock()
    }

    // MARK: Frame processing

    fileprivate func processFrame(_ touches: [MTPoint]) {
        stateLock.lock()
        defer { stateLock.unlock() }

        if touches.count >= 4 {
            let spread = averageSpread(touches)

            if !gestureActive {
                // New 4+ finger contact: capture the baseline spread.
                gestureActive = true
                gestureConsumed = false
                initialSpread = max(spread, 0.02)
                return
            }

            guard !gestureConsumed else { return }

            let now = Date().timeIntervalSince1970
            guard now - lastTriggerTime > triggerCooldown else { return }

            let ratio = spread / initialSpread
            if ratio < pinchInRatio {
                gestureConsumed = true
                lastTriggerTime = now
                DispatchQueue.main.async { self.onPinchIn?() }
            } else if ratio > spreadOutRatio {
                gestureConsumed = true
                lastTriggerTime = now
                DispatchQueue.main.async { self.onSpreadOut?() }
            }
        } else if touches.count <= 2 {
            // Fingers lifted; 3-finger frames during lift-off are ignored so a
            // wobbly release doesn't restart the baseline.
            gestureActive = false
            gestureConsumed = false
        }
    }

    /// Mean distance of the touches from their centroid, in normalized
    /// trackpad coordinates. Shrinks during a pinch, grows during a spread,
    /// stays roughly constant during multi-finger swipes.
    private func averageSpread(_ points: [MTPoint]) -> Float {
        let n = Float(points.count)
        var cx: Float = 0, cy: Float = 0
        for p in points { cx += p.x; cy += p.y }
        cx /= n
        cy /= n

        var total: Float = 0
        for p in points {
            let dx = p.x - cx
            let dy = p.y - cy
            total += (dx * dx + dy * dy).squareRoot()
        }
        return total / n
    }
}
