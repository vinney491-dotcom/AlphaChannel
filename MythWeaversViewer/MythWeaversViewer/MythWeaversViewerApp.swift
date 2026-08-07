import SwiftUI

@main
struct MythWeaversViewerApp: App {
    @StateObject private var library = SheetLibrary()

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environmentObject(library)
                .onOpenURL { url in
                    library.addFromSharedURL(url)
                }
        }
    }
}
