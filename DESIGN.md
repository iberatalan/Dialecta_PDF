---
name: Dialecta PDF
colors:
  surface: '#131313'
  surface-dim: '#131313'
  surface-bright: '#393939'
  surface-container-lowest: '#0e0e0e'
  surface-container-low: '#1b1b1c'
  surface-container: '#202020'
  surface-container-high: '#2a2a2a'
  surface-container-highest: '#353535'
  on-surface: '#e5e2e1'
  on-surface-variant: '#c0c7d4'
  inverse-surface: '#e5e2e1'
  inverse-on-surface: '#303030'
  outline: '#8a919e'
  outline-variant: '#404752'
  surface-tint: '#a3c9ff'
  primary: '#a3c9ff'
  on-primary: '#00315c'
  primary-container: '#0078d4'
  on-primary-container: '#ffffff'
  inverse-primary: '#0060ab'
  secondary: '#b6c8df'
  on-secondary: '#213243'
  secondary-container: '#37485b'
  on-secondary-container: '#a5b7cd'
  tertiary: '#ffb4a8'
  on-tertiary: '#680100'
  tertiary-container: '#da3b2a'
  on-tertiary-container: '#ffffff'
  error: '#ffb4ab'
  on-error: '#690005'
  error-container: '#93000a'
  on-error-container: '#ffdad6'
  primary-fixed: '#d3e3ff'
  primary-fixed-dim: '#a3c9ff'
  on-primary-fixed: '#001c39'
  on-primary-fixed-variant: '#004883'
  secondary-fixed: '#d2e4fb'
  secondary-fixed-dim: '#b6c8df'
  on-secondary-fixed: '#0a1d2d'
  on-secondary-fixed-variant: '#37485b'
  tertiary-fixed: '#ffdad4'
  tertiary-fixed-dim: '#ffb4a8'
  on-tertiary-fixed: '#410000'
  on-tertiary-fixed-variant: '#930200'
  background: '#131313'
  on-background: '#e5e2e1'
  surface-variant: '#353535'
typography:
  display-lg:
    fontFamily: Inter
    fontSize: 28px
    fontWeight: '600'
    lineHeight: 36px
    letterSpacing: -0.02em
  headline-md:
    fontFamily: Inter
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 28px
  title-sm:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '500'
    lineHeight: 24px
  body-md:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
  body-sm:
    fontFamily: Inter
    fontSize: 13px
    fontWeight: '400'
    lineHeight: 18px
  label-uppercase:
    fontFamily: Inter
    fontSize: 11px
    fontWeight: '700'
    lineHeight: 16px
    letterSpacing: 0.05em
  mono-data:
    fontFamily: jetbrainsMono
    fontSize: 12px
    fontWeight: '400'
    lineHeight: 16px
rounded:
  sm: 0.25rem
  DEFAULT: 0.5rem
  md: 0.75rem
  lg: 1rem
  xl: 1.5rem
  full: 9999px
spacing:
  sidebar-width: 260px
  container-padding: 32px
  card-gap: 24px
  gutter-md: 16px
  stack-sm: 8px
  stack-xs: 4px
---

## Brand & Style

The design system is engineered for **Dialecta PDF**, a premium Windows desktop application that prioritizes precision, security, and professional utility. The brand personality is authoritative yet understated, distancing itself from "AI-heavy" trends in favor of a reliable, tool-first philosophy.

The visual style is **Corporate Modern**, drawing heavily from the WinUI 3 (Fluent) aesthetic. It utilizes semi-transparent materials, refined geometry, and a disciplined approach to whitespace to ensure complex PDF operations feel manageable and high-end. The goal is to provide a "Pro" environment where the interface recedes, allowing the user's documents and tasks to take center stage.

- **Minimalism:** Redundant borders are replaced by tonal changes.
- **Precision:** Alignment follows a strict 4px baseline grid.
- **Utility:** Functional elements use clear, solid colors to denote state changes without visual noise.

## Colors

The palette is anchored in a **sophisticated dark mode** that reduces eye strain during long working sessions. 

- **Primary Action:** A professional Windows Blue (#0078D4) is used exclusively for primary calls to action (e.g., "Convert", "Merge").
- **Secondary/Accent:** A soft navy (#2D3E50) serves as the foundation for selected states in the sidebar and subtle UI accents.
- **Semantic Red:** A reserved, solid red (#C42B1C) is used for destructive actions like "Remove" or "Delete" to ensure they are distinct but not neon.
- **Neutrals:** The background hierarchy uses deep charcoals. The main canvas is the darkest layer (#121212), with cards and sidebar providing subtle elevation through slightly lighter values.

## Typography

This design system utilizes **Inter** for its exceptional legibility on digital displays and its neutral, professional character. 

- **Hierarchy:** Use `display-lg` for primary page headers. 
- **Section Headers:** Card titles and sidebar category labels use `label-uppercase` with increased letter spacing to create clear visual boundaries.
- **Data Grids:** For technical information like SHA-256 hashes or file sizes, use a monospaced font (JetBrains Mono) to ensure character alignment and readability.
- **Contrast:** Secondary information (descriptions, helper text) should use `body-sm` with a reduced opacity (70%) rather than a different color, maintaining palette harmony.

## Layout & Spacing

The layout follows a **Fixed Sidebar + Fluid Content** model common in professional desktop software.

- **Sidebar:** A fixed width of 260px provides a stable navigation anchor.
- **Content Area:** Uses a fluid grid with a maximum content width of 1440px. When the window exceeds this, the content centers with auto-margins.
- **Grid:** Elements should align to a 12-column internal grid within the content area.
- **Rhythm:** Use 32px for outer container margins to provide "breathable" luxury. Internal card components use 16px (gutter-md) for internal padding.
- **Reflow:** On smaller window sizes, the right-hand "Settings/Action" panel can collapse into a bottom sheet or a toggleable drawer to prioritize the central workspace.

## Elevation & Depth

Depth is conveyed through **Tonal Layering** and **Subtle Outlines** rather than heavy shadows.

- **Level 0 (Canvas):** #121212. The base application background.
- **Level 1 (Sidebar/Bottom Bar):** #191919. Surfaces that sit flush with the frame.
- **Level 2 (Cards/Work Area):** #1E1E1E. These elements use a 1px solid border (#FFFFFF at 5% opacity) to define edges against the canvas.
- **Interactive States:** When a card or list item is hovered, its background shifts to #252525. No "pop" shadows are used, keeping the interface flat and efficient.
- **Modals/Popovers:** Use a backdrop blur (20px) with a semi-transparent fill to create a distinct separation from the workspace during focused tasks.

## Shapes

The shape language is defined by **Medium Roundedness (8px)**, aligning with modern Windows standards.

- **Primary Elements:** Buttons, input fields, and small cards use 8px (`rounded-md`).
- **Containers:** Large workspace areas or the main application window frame (when not maximized) use 12px (`rounded-lg`).
- **Selection Indicators:** The active state in the sidebar uses a vertical "pill" indicator (4px width) on the far left of the item, paired with an 8px rounded background for the item itself.

## Components

### Buttons
- **Primary:** Solid #0078D4 fill, white text, 8px corner radius. Subtle hover brightness increase (+10%).
- **Secondary/Ghost:** No fill, 1px border (#FFFFFF at 15%), white text.
- **Destructive:** Solid #C42B1C fill for high-risk actions like "Clear All".

### Navigation Sidebar
- Items should have a height of 40px with 12px horizontal padding.
- Use thin-line icons (1.5pt stroke) to the left of the text.
- Selected state: Background #2D3E50 (Soft Navy) with #0078D4 left-border accent.

### Input Fields
- Background #252525, 1px bottom border (accented blue on focus).
- Height: 36px for standard inputs.
- Labels sit above the field in `label-uppercase` style.

### Data Tables (Audit Logs)
- Header: Background #191919, uppercase labels, fixed position.
- Rows: Alternate row striping is avoided; use 1px horizontal dividers (#FFFFFF at 5%).
- Scrollbars: Thin, "ghost" style that only appears on hover.

### Feature Cards
- Right-hand settings panels are grouped in cards with a background of #1E1E1E.
- Each section within a card is separated by a 1px divider.
- Use 24px spacing between distinct cards.

### Drop Zones
- Central workspace for "Select File" should have a dashed border (#FFFFFF at 10%) when empty, transforming to a solid tonal surface when a file is loaded.