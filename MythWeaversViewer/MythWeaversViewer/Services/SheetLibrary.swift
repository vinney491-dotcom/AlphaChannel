import Foundation
import Combine

@MainActor
final class SheetLibrary: ObservableObject {
    @Published private(set) var sheets: [CharacterSheet] = []
    @Published var pendingOpenID: UUID?

    private let storageKey = "mythweavers.savedSheets"
    private let defaults: UserDefaults

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        load()
    }

    var sortedSheets: [CharacterSheet] {
        sheets.sorted { lhs, rhs in
            let left = lhs.lastOpened ?? lhs.dateAdded
            let right = rhs.lastOpened ?? rhs.dateAdded
            return left > right
        }
    }

    @discardableResult
    func add(title: String, rawURL: String, notes: String = "") -> CharacterSheet? {
        guard let parsed = MythWeaversURLParser.parse(rawURL) else { return nil }

        if let existingID = parsed.id,
           let index = sheets.firstIndex(where: { $0.mythWeaversID == existingID }) {
            sheets[index].url = parsed.url
            if !title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                sheets[index].title = title.trimmingCharacters(in: .whitespacesAndNewlines)
            }
            if !notes.isEmpty {
                sheets[index].notes = notes
            }
            save()
            pendingOpenID = sheets[index].id
            return sheets[index]
        }

        let cleanedTitle = title.trimmingCharacters(in: .whitespacesAndNewlines)
        let sheet = CharacterSheet(
            title: cleanedTitle.isEmpty
                ? MythWeaversURLParser.defaultTitle(for: parsed.id)
                : cleanedTitle,
            mythWeaversID: parsed.id ?? UUID().uuidString,
            url: parsed.url,
            notes: notes
        )
        sheets.insert(sheet, at: 0)
        save()
        pendingOpenID = sheet.id
        return sheet
    }

    func addFromSharedURL(_ url: URL) {
        _ = add(title: "", rawURL: url.absoluteString)
    }

    func update(_ sheet: CharacterSheet) {
        guard let index = sheets.firstIndex(where: { $0.id == sheet.id }) else { return }
        sheets[index] = sheet
        save()
    }

    func markOpened(_ sheet: CharacterSheet) {
        guard let index = sheets.firstIndex(where: { $0.id == sheet.id }) else { return }
        sheets[index].lastOpened = Date()
        save()
    }

    func delete(at offsets: IndexSet) {
        let sorted = sortedSheets
        let ids = offsets.map { sorted[$0].id }
        sheets.removeAll { ids.contains($0.id) }
        save()
    }

    func delete(_ sheet: CharacterSheet) {
        sheets.removeAll { $0.id == sheet.id }
        save()
    }

    private func load() {
        guard let data = defaults.data(forKey: storageKey) else { return }
        do {
            sheets = try JSONDecoder().decode([CharacterSheet].self, from: data)
        } catch {
            sheets = []
        }
    }

    private func save() {
        do {
            let data = try JSONEncoder().encode(sheets)
            defaults.set(data, forKey: storageKey)
        } catch {
            // Local bookmark save failed; keep in-memory list for the session.
        }
    }
}
