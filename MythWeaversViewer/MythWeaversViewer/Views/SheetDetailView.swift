import SwiftUI

struct SheetDetailView: View {
    @EnvironmentObject private var library: SheetLibrary
    @State private var sheet: CharacterSheet
    @State private var isLoading = false
    @State private var canGoBack = false
    @State private var pageTitle = ""
    @State private var showingEdit = false
    @State private var webViewID = UUID()

    init(sheet: CharacterSheet) {
        _sheet = State(initialValue: sheet)
    }

    var body: some View {
        VStack(spacing: 0) {
            if isLoading {
                ProgressView()
                    .controlSize(.small)
                    .padding(.vertical, 6)
                    .frame(maxWidth: .infinity)
            }

            SheetWebView(
                url: sheet.url,
                isLoading: $isLoading,
                canGoBack: $canGoBack,
                pageTitle: $pageTitle
            )
            .id(webViewID)
        }
        .navigationTitle(sheet.title)
        .navigationBarTitleDisplayMode(.inline)
        .toolbar {
            ToolbarItemGroup(placement: .topBarTrailing) {
                Button {
                    webViewID = UUID()
                } label: {
                    Image(systemName: "arrow.clockwise")
                }
                .accessibilityLabel("Reload")

                ShareLink(item: sheet.url) {
                    Image(systemName: "square.and.arrow.up")
                }

                Button {
                    showingEdit = true
                } label: {
                    Image(systemName: "pencil")
                }
                .accessibilityLabel("Edit bookmark")
            }
        }
        .sheet(isPresented: $showingEdit) {
            EditSheetView(sheet: $sheet) { updated in
                library.update(updated)
                sheet = updated
            }
        }
        .onAppear {
            library.markOpened(sheet)
        }
        .onChange(of: pageTitle) { _, newTitle in
            guard sheet.title.hasPrefix("Sheet #"),
                  !newTitle.isEmpty,
                  newTitle.lowercased() != "myth-weavers"
            else { return }
            sheet.title = newTitle
            library.update(sheet)
        }
    }
}

struct EditSheetView: View {
    @Binding var sheet: CharacterSheet
    var onSave: (CharacterSheet) -> Void
    @Environment(\.dismiss) private var dismiss

    @State private var title: String
    @State private var notes: String

    init(sheet: Binding<CharacterSheet>, onSave: @escaping (CharacterSheet) -> Void) {
        self._sheet = sheet
        self.onSave = onSave
        self._title = State(initialValue: sheet.wrappedValue.title)
        self._notes = State(initialValue: sheet.wrappedValue.notes)
    }

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    TextField("Title", text: $title)
                    LabeledContent("Sheet ID", value: sheet.mythWeaversID)
                    Text(sheet.url.absoluteString)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .textSelection(.enabled)
                }
                Section("Notes") {
                    TextField("Notes", text: $notes, axis: .vertical)
                        .lineLimit(3...8)
                }
            }
            .navigationTitle("Edit Bookmark")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Save") {
                        var updated = sheet
                        updated.title = title.trimmingCharacters(in: .whitespacesAndNewlines)
                        updated.notes = notes
                        onSave(updated)
                        dismiss()
                    }
                }
            }
        }
    }
}
