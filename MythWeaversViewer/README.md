# Myth-Weavers Viewer (iPhone + iPad)

Universal SwiftUI app for opening **public** Myth-Weavers character sheet links on iPhone and iPad.

## What it does

- Paste a public Myth-Weavers URL into a local library
- Open the sheet in an in-app browser (`WKWebView`)
- Search, rename, note, share, and reload bookmarks
- Works as a universal app (iPhone and iPad split view)

## Supported URL shapes

- Legacy: `https://www.myth-weavers.com/sheet.html#id=12345`
- Query / path forms under `myth-weavers.com` (sheet id is extracted when present)

Private account sheets that require login are out of scope for this first version.

## Open in Xcode

1. On a Mac, open `MythWeaversViewer/MythWeaversViewer.xcodeproj`
2. Select your Team under **Signing & Capabilities** (set a real bundle id if you like)
3. Pick an iPhone or iPad simulator (or your device)
4. Run (`⌘R`)

Requires **Xcode 15+** and **iOS 17+**.

## Use it

1. Tap **+**
2. Paste a public sheet URL
3. Optionally set a title / notes
4. Tap the sheet to view it

## Project layout

```
MythWeaversViewer/
  MythWeaversViewer.xcodeproj/
  MythWeaversViewer/
    MythWeaversViewerApp.swift
    Models/CharacterSheet.swift
    Services/SheetLibrary.swift
    Views/
```

## Next ideas

- Safari / share-sheet “Open in Myth-Weavers Viewer”
- Optional Myth-Weavers sign-in for private sheets
- Native summary strip (HP / AC) for systems we can parse reliably
