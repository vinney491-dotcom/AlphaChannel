import Foundation

struct CharacterSheet: Identifiable, Codable, Hashable {
    let id: UUID
    var title: String
    var mythWeaversID: String
    var url: URL
    var notes: String
    var dateAdded: Date
    var lastOpened: Date?

    init(
        id: UUID = UUID(),
        title: String,
        mythWeaversID: String,
        url: URL,
        notes: String = "",
        dateAdded: Date = Date(),
        lastOpened: Date? = nil
    ) {
        self.id = id
        self.title = title
        self.mythWeaversID = mythWeaversID
        self.url = url
        self.notes = notes
        self.dateAdded = dateAdded
        self.lastOpened = lastOpened
    }
}

enum MythWeaversURLParser {
    /// Accepts public Myth-Weavers links and extracts a stable sheet id when possible.
    static func parse(_ raw: String) -> (id: String?, url: URL)? {
        let trimmed = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return nil }

        var candidate = trimmed
        if !candidate.contains("://") {
            candidate = "https://\(candidate)"
        }

        guard var components = URLComponents(string: candidate),
              let host = components.host?.lowercased(),
              host.contains("myth-weavers.com")
        else {
            return nil
        }

        // Normalize host to www for consistent bookmarks.
        if host == "myth-weavers.com" || host == "og.myth-weavers.com" {
            components.host = "www.myth-weavers.com"
        }

        guard let url = components.url else { return nil }

        if let id = extractID(from: components) {
            return (id, preferredURL(for: id, fallback: url))
        }

        return (nil, url)
    }

    static func preferredURL(for id: String, fallback: URL) -> URL {
        // Prefer the current viewer path; fall back to legacy hash form if needed.
        if let modern = URL(string: "https://www.myth-weavers.com/sheets/?id=\(id)") {
            return modern
        }
        return fallback
    }

    static func defaultTitle(for id: String?) -> String {
        if let id, !id.isEmpty {
            return "Sheet #\(id)"
        }
        return "Myth-Weavers Sheet"
    }

    private static func extractID(from components: URLComponents) -> String? {
        if let queryID = components.queryItems?.first(where: {
            ["id", "sheet", "sheetid"].contains($0.name.lowercased())
        })?.value, !queryID.isEmpty {
            return queryID
        }

        if let fragment = components.fragment {
            // Legacy: sheet.html#id=12345
            let parts = fragment.split(separator: "&").map(String.init)
            for part in parts {
                let kv = part.split(separator: "=", maxSplits: 1).map(String.init)
                if kv.count == 2, kv[0].lowercased() == "id", !kv[1].isEmpty {
                    return kv[1]
                }
            }
        }

        // Path forms: /sheets/12345 or /idunn/sheets/12345
        let segments = components.path.split(separator: "/").map(String.init)
        if let last = segments.last, last.allSatisfy(\.isNumber), !last.isEmpty {
            return last
        }

        return nil
    }
}
