import SwiftUI

struct ContentView: View {
    @EnvironmentObject private var library: SheetLibrary
    @State private var showingAddSheet = false
    @State private var selectedSheet: CharacterSheet?
    @State private var searchText = ""

    private var filteredSheets: [CharacterSheet] {
        let base = library.sortedSheets
        let query = searchText.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !query.isEmpty else { return base }
        return base.filter {
            $0.title.localizedCaseInsensitiveContains(query)
                || $0.mythWeaversID.localizedCaseInsensitiveContains(query)
                || $0.notes.localizedCaseInsensitiveContains(query)
        }
    }

    var body: some View {
        NavigationSplitView {
            List(selection: $selectedSheet) {
                if filteredSheets.isEmpty {
                    ContentUnavailableView(
                        library.sheets.isEmpty ? "No sheets yet" : "No matches",
                        systemImage: "doc.text.magnifyingglass",
                        description: Text(
                            library.sheets.isEmpty
                                ? "Add a public Myth-Weavers link to start your library."
                                : "Try a different name or sheet id."
                        )
                    )
                } else {
                    ForEach(filteredSheets) { sheet in
                        NavigationLink(value: sheet) {
                            SheetRowView(sheet: sheet)
                        }
                    }
                    .onDelete(perform: library.delete)
                }
            }
            .navigationTitle("Myth-Weavers")
            .searchable(text: $searchText, prompt: "Search sheets")
            .toolbar {
                ToolbarItem(placement: .primaryAction) {
                    Button {
                        showingAddSheet = true
                    } label: {
                        Image(systemName: "plus")
                    }
                    .accessibilityLabel("Add sheet")
                }
            }
            .sheet(isPresented: $showingAddSheet) {
                AddSheetView()
            }
            .onChange(of: library.pendingOpenID) { _, newValue in
                guard let newValue,
                      let sheet = library.sheets.first(where: { $0.id == newValue })
                else { return }
                selectedSheet = sheet
                library.pendingOpenID = nil
            }
        } detail: {
            if let selectedSheet {
                SheetDetailView(sheet: selectedSheet)
            } else {
                ContentUnavailableView(
                    "Select a sheet",
                    systemImage: "square.split.2x1",
                    description: Text("Pick a saved public link, or add a new one.")
                )
            }
        }
    }
}

struct SheetRowView: View {
    let sheet: CharacterSheet

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(sheet.title)
                .font(.headline)
                .lineLimit(1)
            Text("ID \(sheet.mythWeaversID)")
                .font(.caption)
                .foregroundStyle(.secondary)
            if !sheet.notes.isEmpty {
                Text(sheet.notes)
                    .font(.caption2)
                    .foregroundStyle(.tertiary)
                    .lineLimit(1)
            }
        }
        .padding(.vertical, 2)
    }
}

#Preview {
    ContentView()
        .environmentObject(SheetLibrary())
}
