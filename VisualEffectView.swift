import SwiftUI
import Cocoa

struct VisualEffectView: NSViewRepresentable {
    func makeNSView(context: Context) -> NSVisualEffectView {
        let view = NSVisualEffectView()
        view.blendingMode = .behindWindow
        view.state = .active
        // hudWindow or fullScreenUI are excellent for dark, high-quality macOS overlay blurs
        view.material = .hudWindow
        return view
    }
    
    func updateNSView(_ nsView: NSVisualEffectView, context: Context) {}
}
