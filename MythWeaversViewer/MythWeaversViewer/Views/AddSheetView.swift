import SwiftUI

struct AddSheetView: View {
    @EnvironmentObject private var library: SheetLibrary
    @Environment(\.dismiss) private var dismiss

    @State private var title = ""
    @State private var urlText = ""
    @State private var notes = ""
    @State private var errorMessage: String?

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    TextField("Title (optional)", text: $title)
                    TextField("Myth-Weavers URL", text: $urlText)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled()
                        .keyboardType(.URL)
                } footer: {
                    Text("Paste a public sheet link, for example https://www.myth-weavers.com/sheet.html#id=12345")
                }

                Section("Notes") {
                    TextField("Optional notes", text: $notes, axis: .vertical)
                        .lineLimit(3...6)
                }

                if let errorMessage {
                    Section {
                        Text(errorMessage)
                            .foregroundStyle(.red)
                    }
                }
            }
            .navigationTitle("Add Sheet")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Add") { add() }
                        .disabled(urlText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                }
            }
        }
    }

    private func add() {
        if library.add(title: title, rawURL: urlText, notes: notes) != nil {
            dismiss()
        } else {
            errorMessage = "That doesn’t look like a Myth-Weavers URL."
        }
    }
}
