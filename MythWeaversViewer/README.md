# Myth-Weavers Viewer (iPhone + iPad)

## Phone app — Myth Case

A Home Screen web app for public Myth-Weavers sheets.

### Install on your iPhone

1. Open this link in **Safari**:
   - Live preview (while the cloud agent is running): https://wide-badgers-mate.loca.lt/
   - After GitHub Pages is enabled: `https://vinney491-dotcom.github.io/AlphaChannel/`
2. Tap **Share** → **Add to Home Screen** → **Add**
3. Open **Myth Case**, tap **Add sheet**, paste a public Myth-Weavers URL

Source: `MythWeaversViewer/web/`

## Native SwiftUI shell (Mac + Xcode)

For an in-app WebView (sheets stay inside the app UI):

1. Open `MythWeaversViewer/MythWeaversViewer.xcodeproj` on a Mac
2. Set your Team under **Signing & Capabilities**
3. Run on your iPhone or iPad (`⌘R`)

Requires **Xcode 15+** and **iOS 17+**.

## What it does

- Save public Myth-Weavers URLs on your device
- Open sheets for reading at the table
- Supports common link shapes (`sheet.html#id=…`, `/sheets/?id=…`)

Private / login-only sheets are out of scope for v1.
