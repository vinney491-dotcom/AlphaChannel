# Myth-Weavers Viewer (iPhone + iPad)

Two ways to use public Myth-Weavers sheets on your phone:

## 1) Phone app now — Myth Case (Home Screen)

Open in **Safari** on your iPhone, then **Share → Add to Home Screen**:

https://cdn.jsdelivr.net/gh/vinney491-dotcom/AlphaChannel@cursor/myth-weavers-viewer-3c20/MythWeaversViewer/web/index.html

Source: `MythWeaversViewer/web/`

## 2) Native SwiftUI shell (Mac + Xcode)

Universal app with an in-app `WKWebView` library:

1. On a Mac, open `MythWeaversViewer/MythWeaversViewer.xcodeproj`
2. Set your Team under **Signing & Capabilities**
3. Run on your iPhone or iPad (`⌘R`)

Requires **Xcode 15+** and **iOS 17+**.

## What both versions do

- Save public Myth-Weavers URLs
- Open sheets for reading at the table
- Support common link shapes (`sheet.html#id=…`, `/sheets/?id=…`)

Private account sheets that require login are out of scope for v1.

## Project layout

```
MythWeaversViewer/
  web/                         # Myth Case phone web app
  MythWeaversViewer.xcodeproj/ # Native iOS project
  MythWeaversViewer/           # SwiftUI sources
```
