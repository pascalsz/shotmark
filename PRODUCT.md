# Product

<!-- impeccable:product-schema 1 -->

## Platform

web (project site) · the product itself is a Windows desktop app (WPF, .NET 10)

## Stack

Project site: hand-written static HTML/CSS with minimal JavaScript under `site/`, deployed by GitHub Actions to GitHub Pages at `https://pascalsz.github.io/shotmark/`. No site generator, no npm, no external fonts/scripts/CDNs (decided by Pascal: low maintenance, no attack surface, no own server).

## Users

People on Windows who take screenshots many times a day to explain something (support, documentation, bug reports, instructions) and want to mark them up quickly. Many know Snagit and look for a free, lighter alternative. One core group works on several PCs that share a folder (Dropbox, OneDrive, network drive).

## Product Purpose

ShotMark: press PrtScn, drag an area, mark it up — the capture is saved and on the clipboard immediately. Success: install in a minute, first marked-up screenshot within seconds, no account, no subscription.

## Positioning

Free and open source (MIT), no account, no telemetry, updates itself from GitHub Releases. Several PCs work together through a shared folder: the history shows which capture was made when on which PC, and every capture stays editable later. A sober "free alternative to Snagit for everyday use" — never a clone claim, never an unprovable comparison.

## Capabilities and Constraints

- Region capture on all monitors, magnifier and pixel size; tooltips stay in the capture (screen frozen on the first hotkey key).
- Tools: rectangle, arrow, check, cross, text, blur (pixelate), crop; undo/redo; copy to clipboard.
- Scrolling capture for long pages (auto or manual scrolling), up to 16,000 px.
- History strip with day headers, PC filter, conflict handling when two PCs edit the same capture.
- UI in English and German, follows the Windows language.
- Windows 10/11, 64-bit, per-user install via `ShotMark.App-win-Setup.exe`, no admin rights, no .NET needed. Setup is not code-signed yet (SmartScreen warning on first download).
- Not available: macOS (planned later, no date), video recording, cloud service of its own.

## Brand Commitments

- Name: ShotMark. App icon `src/ShotMark.App/Assets/ShotMark.ico` (red rounded square with capture mark).
- The app's dark palette and its red annotation marks are the recognizable identity; the site uses them (pinned by Pascal: direction "C dark").
- German copy addresses the reader with "du", like the app.
- Trademark notice: "Snagit is a trademark of TechSmith Corporation. ShotMark is not affiliated with TechSmith." No Snagit logo, screenshots or look.

## Evidence on Hand

- Real app renders in both languages from `tests/ShotMark.App.Tests/UiSnapshots.cs` (demo content is synthetic and neutral), shipped as `site/assets/{en,de}/*.png` via `scripts/site-images.ps1`.
- No users, testimonials, download counts or benchmarks exist — never invent them.

## Product Principles

1. Speed over features: the shortest path from keypress to a shareable, marked-up image.
2. Nothing in the way: no account, no subscription, no telemetry, no nagging.
3. Honest and sober: claims only what the app demonstrably does.
4. Shared folders are first-class: several PCs, one history.

## Accessibility & Inclusion

Site meets WCAG 2.1 AA (contrast, keyboard, focus visible, reduced motion respected, readable without JavaScript), works from 360 px width.
