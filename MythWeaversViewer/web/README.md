# Myth Case — phone app (Add to Home Screen)

This is the **phone app** for public Myth-Weavers sheets. It runs in Safari as a home-screen web app.

## Install on iPhone / iPad

1. On your phone, open Safari and go to the app URL (after this branch is pushed, jsDelivr serves it from GitHub):

   **https://cdn.jsdelivr.net/gh/vinney491-dotcom/AlphaChannel@cursor/myth-weavers-viewer-3c20/MythWeaversViewer/web/index.html**

2. Tap **Share** → **Add to Home Screen** → **Add**
3. Open **Myth Case** from your Home Screen
4. Tap **Add sheet**, paste a public Myth-Weavers link, save
5. Tap a sheet to open it on Myth-Weavers

## What it does

- Saves public sheet links on your device
- One-tap open to the live Myth-Weavers viewer
- Works offline for your library list (sheet pages still need network)

## Native iOS project

For an App Store–style WebView shell (sheets stay inside the app), use the Xcode project in `MythWeaversViewer/MythWeaversViewer.xcodeproj` on a Mac.
