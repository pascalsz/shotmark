---
name: ShotMark project site
description: The page is marked up the way ShotMark marks up a capture, on the app's own dark chrome.
colors:
  annotation-red: "#fc4242"
  annotation-red-label: "#ff6b6b"
  check-green: "#3dbe5c"
  download-blue: "#1f6fe0"
  download-blue-hover: "#1a5fc4"
  link-blue: "#5aa7ff"
  chrome-ground: "#1b1b1d"
  chrome-ground-deep: "#141416"
  chrome-panel: "#232326"
  chrome-raise: "#2a2a2e"
  chrome-hairline: "#3a3a40"
  text-bright: "#ececee"
  text-dim: "#b4b4bb"
  text-faint: "#9a9aa2"
typography:
  display:
    fontFamily: "Segoe UI Variable Display, Segoe UI, system-ui, -apple-system, Helvetica Neue, sans-serif"
    fontSize: "clamp(3rem, 8.4vw, 6rem)"
    fontWeight: 750
    lineHeight: 1.02
    letterSpacing: "-0.035em"
  headline:
    fontFamily: "Segoe UI Variable Display, Segoe UI, system-ui, -apple-system, Helvetica Neue, sans-serif"
    fontSize: "clamp(2rem, 4.2vw, 3.25rem)"
    fontWeight: 700
    lineHeight: 1.08
    letterSpacing: "-0.025em"
  title:
    fontFamily: "Segoe UI Variable Display, Segoe UI, system-ui, -apple-system, Helvetica Neue, sans-serif"
    fontSize: "1.25rem"
    fontWeight: 650
    lineHeight: 1.08
    letterSpacing: "-0.01em"
  lede:
    fontFamily: "Segoe UI Variable Text, Segoe UI, system-ui, -apple-system, Helvetica Neue, sans-serif"
    fontSize: "clamp(1.15rem, 1.9vw, 1.4rem)"
    fontWeight: 400
    lineHeight: 1.5
  body:
    fontFamily: "Segoe UI Variable Text, Segoe UI, system-ui, -apple-system, Helvetica Neue, sans-serif"
    fontSize: "1.0625rem"
    fontWeight: 400
    lineHeight: 1.6
  label-annotation:
    fontFamily: "Segoe UI Variable Display, Segoe UI, system-ui, -apple-system, Helvetica Neue, sans-serif"
    fontSize: "clamp(0.95rem, 1.6vw, 1.35rem)"
    fontWeight: 700
    lineHeight: 1.1
  label-key:
    fontFamily: "Segoe UI Variable Text, Segoe UI, system-ui, -apple-system, Helvetica Neue, sans-serif"
    fontSize: "0.82em"
    fontWeight: 600
    lineHeight: 1.4
rounded:
  mark: "0px"
  blur: "2px"
  focus: "4px"
  key: "6px"
  control: "10px"
  control-large: "12px"
  pill: "999px"
spacing:
  gutter: "clamp(20px, 5vw, 56px)"
  section: "clamp(72px, 11vw, 140px)"
  split-gap: "clamp(32px, 5vw, 72px)"
  block-gap: "clamp(36px, 5vw, 56px)"
  list-gap: "28px"
  measure: "62ch"
  container: "1240px"
components:
  button-primary:
    backgroundColor: "{colors.download-blue}"
    textColor: "#ffffff"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    padding: "12px 22px"
    height: "48px"
  button-primary-hover:
    backgroundColor: "{colors.download-blue-hover}"
    textColor: "#ffffff"
  button-primary-large:
    backgroundColor: "{colors.download-blue}"
    textColor: "#ffffff"
    rounded: "{rounded.control-large}"
    padding: "15px 28px"
    height: "58px"
  text-link:
    textColor: "{colors.text-bright}"
    height: "44px"
  keycap:
    backgroundColor: "{colors.chrome-raise}"
    textColor: "{colors.text-bright}"
    typography: "{typography.label-key}"
    rounded: "{rounded.key}"
    padding: "0.12em 0.5em 0.18em"
  language-pill:
    textColor: "{colors.text-bright}"
    rounded: "{rounded.pill}"
    padding: "5px 13px"
  app-window:
    backgroundColor: "{colors.chrome-panel}"
    rounded: "{rounded.control}"
  app-window-bar:
    backgroundColor: "{colors.chrome-raise}"
    textColor: "{colors.text-dim}"
    height: "36px"
  info-panel:
    backgroundColor: "{colors.chrome-panel}"
    textColor: "{colors.text-dim}"
    rounded: "{rounded.control-large}"
    padding: "26px 28px"
  annotation-frame:
    textColor: "{colors.annotation-red}"
    rounded: "{rounded.mark}"
  step-number:
    textColor: "{colors.text-bright}"
    rounded: "{rounded.mark}"
    size: "44px"
---

# Design System: ShotMark project site

## Overview

**Creative North Star: "The Marked-Up Capture"**

The site is a screenshot that ShotMark has already been used on. Its decoration is the product's own mechanism: the key word of a headline sits inside a square red frame, the app's straight red arrow points at it, facts carry the app's green check, a word that should stay private is pixelated, and real app renders carry live red frames with red labels. Nothing decorative exists that the app could not have drawn.

The ground is the desktop app's dark chrome (origin: `src/ShotMark.App/Theme/Theme.xaml`), so the page and the product read as one object. Density is calm and document-like: a single left-aligned column of headlines and short paragraphs, alternating with 5:7 splits beside real renders, generous section spacing, hairline-ruled lists instead of cards. Blue is functional only: links, the download button, the focus ring.

Fonts are the Windows system faces the app itself runs on (Segoe UI Variable), pinned by the brief: no webfonts, no external resources. Character comes from weight and tight tracking at display sizes, not from a second family.

**Key Characteristics:**
- Dark app chrome as the ground; tonal steps, not cards, separate regions.
- Annotation red, check green and the app's three-layer soft shadow are reserved for marks.
- Square-cornered, 4 px marks over text and images; rounded corners only on chrome and controls.
- Marks draw themselves once as their section enters view; complete and static without JS or with reduced motion.
- Real app renders in a framed app window, never mockups or illustrations.

## Colors

A near-neutral, very slightly cool dark chrome with three saturated signal colors, each with exactly one job.

### Primary
- **Annotation Red** (`annotation-red`): the stroke and fill of every mark: frames, arrow shaft and filled head, cross, step-number squares. Also the app icon's tile in the brand mark, and (at 35% alpha) text selection. Never a surface, button or body-text color.
- **Annotation Label Red** (`annotation-red-label`): the lighter red used only for text labels attached to a frame on a render ("Every PC, one history"), lifted from Annotation Red so small bold labels hold AA contrast on dark ground and imagery.

### Secondary
- **Check Green** (`check-green`): the check mark, used as the bullet for every fact and feature claim. Stroke only, round caps.

### Tertiary
- **Download Blue** (`download-blue`, hover `download-blue-hover`): fill of the primary download button and the skip link, always behind white text.
- **Link Blue** (`link-blue`): inline links and the 2 px focus outline; hover lightens to #8cc2ff. The site's link blue is lighter than the app's accent (#2D8CFF) for contrast on the dark ground.

### Neutral
- **Chrome Ground** (`chrome-ground`): page background and `theme-color`; the app's canvas color.
- **Deep Ground** (`chrome-ground-deep`): the download band, one tonal step below the page; scrollbar track.
- **Panel** (`chrome-panel`): app-window body and the SmartScreen info panel.
- **Raised Bar** (`chrome-raise`): app-window title bar and keycaps.
- **Hairline** (`chrome-hairline`): every 1 px border and rule: list rules, image borders, window frame, band edges, keycap edges, scrollbar thumb.
- **Bright Text** (`text-bright`): headlines, emphasized phrases, list titles.
- **Dim Text** (`text-dim`): body paragraphs, ledes, nav links, window-bar caption. Lifted from the app's #A4A4AC for AA body contrast.
- **Faint Text** (`text-faint`): footer text and window-control glyphs only.

### Named Rules
**The Red Means Marked Rule.** Annotation Red appears only as something ShotMark could have drawn (frame, arrow, cross, numbered square) or as the app icon. If it is a surface, a button or a paragraph, it is wrong.

**The Blue Means Go Rule.** Blue is reserved for things you click to leave or act: links, the download button, the skip link, the focus ring. It is never decoration.

**The App Chrome Rule.** Neutrals come from the app's theme. Site values may be lifted for contrast (dim, faint, link) but not re-hued.

## Typography

**Display Font:** Segoe UI Variable Display (with Segoe UI, system-ui, -apple-system, Helvetica Neue)
**Body Font:** Segoe UI Variable Text (same fallbacks)

**Character:** The app's own Windows face, set heavy (650 to 750) and tightly tracked in headings so the headline reads as a confident caption the marks can sit on; body stays at 400 with generous leading.

### Hierarchy
- **Display** (750, clamp(3rem, 8.4vw, 6rem), 1.02, -0.035em): the hero headline only; carries the framed key word and arrow. The 404 page uses a smaller step (clamp(2.4rem, 6vw, 4rem)).
- **Headline** (700, clamp(2rem, 4.2vw, 3.25rem), 1.08, -0.025em): section headings; short declarative sentences ending in a full stop.
- **Title** (650, 1.25rem, -0.01em): step, feature and panel titles; 1.1rem inside tool rows and the info panel.
- **Lede** (400, clamp(1.15rem, 1.9vw, 1.4rem), 1.5, max 34ch): the one hero sentence. Section intros use 1.15rem at 62ch.
- **Body** (400, 1.0625rem, 1.6; 1rem under 560 px): paragraphs, in Dim Text, max 62ch.
- **Annotation label** (700, clamp(0.95rem, 1.6vw, 1.35rem), 1.1; 0.8rem under 560 px): red text attached to a frame on a render, with a soft text shadow.
- **Key label** (600, 0.82em, 1.4): keycap text, scaled to its surrounding text.

Headings use `text-wrap: balance`, paragraphs `text-wrap: pretty`.

### Named Rules
**The One Family Rule.** One system family in two optical sizes; hierarchy comes from size, weight and tracking. No webfonts, no second family (pinned constraint).

## Layout

A single centered container (max 1240px) with a fluid gutter (clamp(20px, 5vw, 56px)). Content is left-aligned; headline blocks cap at 760px and running text at 62ch. Sections are separated by large vertical space (clamp(72px, 11vw, 140px)) rather than rules or background changes; the only band is the download section on Deep Ground with hairlines top and bottom.

Media sits in two-column splits at 5fr:7fr (text:image), mirrored to 7:5 with a flip, gap clamp(32px, 5vw, 72px). Fact and tool lists use auto-fill grids (min 280px / 320px). The hero render sits below the text and deliberately crosses the fold.

Responsive: at 880px all splits, the download band and the footer collapse to one column and the nav keeps only the language pill. At 560px buttons in action rows go full-width, annotation labels and the hero arrow shrink, and body drops to 1rem. Works from 360px.

## Elevation & Depth

Mostly tonal: ground, deep ground, panel and raised bar are the depth layers, separated by 1 px hairlines. Shadows exist for two jobs only: lifting the app renders off the page, and giving marks the app's own soft drop shadow so they read as annotations sitting on top.

### Shadow Vocabulary
- **Mark shadow** (`filter: drop-shadow(1px 2px 1px rgb(0 0 0 / 0.16)) drop-shadow(2px 3px 2px rgb(0 0 0 / 0.12)) drop-shadow(3px 4px 3px rgb(0 0 0 / 0.08))`): every frame, arrow, check and cross; the app's three-layer annotation shadow.
- **Window shadow** (`box-shadow: 0 40px 80px -40px rgb(0 0 0 / 0.85), 0 2px 0 rgb(255 255 255 / 0.04) inset`): the hero app window.
- **Render shadow** (`box-shadow: 0 30px 60px -36px rgb(0 0 0 / 0.9)`): app renders in sections.
- **Button lift** (`box-shadow: 0 1px 0 rgb(255 255 255 / 0.18) inset, 0 6px 14px -6px rgb(0 0 0 / 0.6)`): the download button.

### Named Rules
**The Marks Float Rule.** Marks always carry the mark shadow; surfaces never do. Panels and lists stay flat and use hairlines.

## Shapes

Two shape languages, kept apart. Marks are square: frames are unrounded rectangles with mitered corners and a 4 px stroke that stays 4 px at any size (non-scaling stroke, pixel viewBox fitted by script); the arrow is a butt-ended 7-unit shaft with a filled triangular head; step numbers are 44px squares with a 2px red border. Check and cross strokes use round caps and joins, as in the app. Chrome and controls are softly rounded: renders, window and buttons at 10px, the large button and info panel at 12px, keycaps at 6px with a 3px bottom edge, the language switch as a pill. Frames overhang their word slightly (about 0.12em each side).

### Named Rules
**The Square Mark Rule.** A mark never gets a border radius. A rounded frame is a UI box, not an annotation.

## Components

### Buttons
Solid, functional, the only blue mass on the page.
- **Shape:** gently rounded (10px; 12px for the large variant).
- **Primary:** Download Blue fill, white 600 text, 48px min height, 12px 22px padding, leading 20px line icon; large variant 58px, 15px 28px, 1.15rem.
- **Hover / Focus:** fill darkens to Download Blue Hover over 160ms ease-out; pressed nudges down 1px; focus is the global 2px Link Blue outline at 3px offset.
- **Text link (secondary):** Bright Text, 600, no underline until hover, trailing arrow icon, 44px target.

### Keycaps
Shortcut keys rendered as physical keys: Raised Bar fill, hairline border with a 3px bottom edge, 6px radius, 600 weight at 0.82em of the surrounding text. Used inline in copy and right-aligned in tool rows.

### Navigation
Brand mark (the app icon tile plus wordmark, 700) left; nav links in Dim Text at 0.95rem turning Bright on hover; the language switch is a hairline pill in Bright Text whose border lightens on hover. Under 880px only the pill remains. The language link carries the current section anchor to the other language.

### App Window
Real renders shown in a recreated app frame: Panel body, 36px Raised Bar title bar with the app icon, title in Dim Text and faint line-drawn window controls, hairline border, 10px radius, window shadow.

### Annotation Marks (signature)
The system's identity. A **frame** wraps a word or overlays a render (positioned in percentages of the image) with an optional red label above, right-aligned or to the side. An **arrow** points at a frame, tip on the frame edge. **Checks** bullet facts and features; a **cross** marks the 404. **Blur** hides a word with a grey pixel checker. With JS, marks in a section draw in once on entering the view (stroke reveal, 700ms cubic-bezier(0.22, 1, 0.36, 1), staggered by per-mark delay; arrow heads fade in after their shaft). Without JS or with reduced motion they are simply complete.

### Lists
Tool lists are hairline-ruled rows (icon, title, keycap, description), never tiles or cards. Feature lists and fact lists use a green check as bullet. Steps use red-bordered numbered squares.

### Info Panel
The SmartScreen help box: Panel fill, hairline border, 12px radius, 26px 28px padding, title plus Dim body and a numbered list. The only boxed content block on the page.

## Do's and Don'ts

### Do:
- **Do** mark the page with the app's own devices (frame, arrow, check, cross, blur) and give every mark the three-layer mark shadow.
- **Do** keep frame strokes at 4px with square, mitered corners at every size.
- **Do** show real app renders in the app-window frame or with a hairline border and render shadow.
- **Do** make every mark complete and visible without JavaScript and under reduced motion; the draw-in runs once.
- **Do** separate regions with space, tonal ground steps and 1px hairlines.
- **Do** use Check Green for claims that are true today, and only for those.

### Don't:
- **Don't** use Annotation Red for surfaces, buttons or running text.
- **Don't** use blue for anything that is not a link, the download button, the skip link or the focus ring.
- **Don't** round a frame or step square, and don't swap the arrow for a curved or decorative one.
- **Don't** present features as icon tiles or cards; use ruled rows and check-bulleted lists.
- **Don't** add decorative gradients; the only gradient in the system is the blur mark's pixel checker.
- **Don't** load webfonts or any external resource.
- **Don't** substitute illustrations or mockups for real app renders.
