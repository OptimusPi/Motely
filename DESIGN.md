---
name: Jimbo
description: Balatro-inspired design system for JAML seed finder tools. Eyedropped from actual game shader output — not Lua hex values.
colors:
  red: "#ff4c40"
  blue: "#0093ff"
  green: "#429f79"
  orange: "#ff9800"
  gold: "#e4b643"
  purple: "#9e74ce"
  dark-red: "#a02721"
  dark-blue: "#0057a1"
  dark-orange: "#a05b00"
  dark-green: "#215f46"
  dark-purple: "#5e437e"
  dark-grey: "#3a5055"
  darkest: "#1e2b2d"
  grey: "#708386"
  teal-grey: "#404c4e"
  panel-edge: "#1e2e32"
  inner-border: "#334461"
  border-silver: "#b9c2d2"
  border-south: "#777e89"
  gold-text: "#e4b643"
  green-text: "#35bd86"
  orange-text: "#ff8f00"
  white: "#ffffff"
  black: "#000000"
  tarot-button: "#9e74ce"
  planet-button: "#00a7ca"
  spectral-button: "#2e76fd"
typography:
  display:
    fontFamily: m6x11plus, monospace
    fontSize: 26px
    fontWeight: 400
    lineHeight: 1
    letterSpacing: 0.04em
  heading:
    fontFamily: m6x11plus, monospace
    fontSize: 14px
    fontWeight: 400
    lineHeight: 1.2
    letterSpacing: 0.08em
  body:
    fontFamily: m6x11plus, monospace
    fontSize: 11px
    fontWeight: 400
    lineHeight: 1.3
    letterSpacing: 0.05em
  label:
    fontFamily: m6x11plus, monospace
    fontSize: 9px
    fontWeight: 400
    lineHeight: 1
    letterSpacing: 0.1em
  micro:
    fontFamily: m6x11plus, monospace
    fontSize: 8px
    fontWeight: 400
    lineHeight: 1
    letterSpacing: 0.08em
rounded:
  sm: 4px
  md: 6px
  lg: 8px
  pill: 10px
spacing:
  xs: 2px
  sm: 4px
  md: 8px
  lg: 12px
  xl: 16px
components:
  panel:
    backgroundColor: "{colors.dark-grey}"
    rounded: "{rounded.md}"
  panel-edge:
    backgroundColor: "{colors.panel-edge}"
  panel-darkest:
    backgroundColor: "{colors.darkest}"
    rounded: "{rounded.md}"
  button-primary:
    backgroundColor: "{colors.red}"
    textColor: "{colors.white}"
    rounded: "{rounded.md}"
    padding: 8px
  button-primary-hover:
    backgroundColor: "{colors.dark-red}"
  button-secondary:
    backgroundColor: "{colors.blue}"
    textColor: "{colors.white}"
    rounded: "{rounded.md}"
    padding: 8px
  button-secondary-hover:
    backgroundColor: "{colors.dark-blue}"
  button-back:
    backgroundColor: "{colors.orange}"
    textColor: "{colors.white}"
    rounded: "{rounded.md}"
    padding: 8px
  button-back-hover:
    backgroundColor: "{colors.dark-orange}"
  tab-active:
    backgroundColor: "{colors.gold}"
    textColor: "{colors.black}"
    rounded: "{rounded.sm}"
  tab-inactive:
    backgroundColor: "transparent"
    textColor: "{colors.grey}"
  score-must:
    backgroundColor: "{colors.blue}"
    textColor: "{colors.white}"
  score-should:
    backgroundColor: "{colors.red}"
    textColor: "{colors.white}"
  glow-must:
    backgroundColor: "{colors.gold}"
  glow-should:
    backgroundColor: "{colors.green-text}"
---

## Overview

Jimbo is the design system for Balatro seed finder tools (JAML-UI, WeeJoker, Seed Finder). It recreates the cozy, tactile, chunky feel of LocalThunk's Balatro — dark panels with silver borders, 3D-press buttons, pixel typography, juice animations. Everything feels like a physical object you can poke.

The system is built **Mobile First** for the **iPhone SE viewport: 375×667px**. This is a HARD constraint — not a minimum, it is THE target. All app screens must fit within 375×667 with **NO SCROLLING**. Content must be designed to fit, not overflow. No fat padding, no bloated margins — every pixel earns its place. If content doesn't fit, redesign it to be more compact or split it into a separate view.

## Colors

All colors are eyedropped from Balatro's actual rendered shader output. Do NOT substitute with Lua source hex values — the game's shader pipeline transforms them.

- **Red (#ff4c40):** Primary action, mult scoring, should-clause hits. The "play" color.
- **Blue (#0093ff):** Secondary action, chips scoring, must-clause gates. The "requirement" color.
- **Green (#429f79):** Success, positive state, money.
- **Orange (#ff9800):** Back/return actions, warning, configuration, misc.
- **Gold (#e4b643):** Seed text, premium highlights, active tab. The "treasure" color.
- **Purple (#9e74ce):** Joker rarity, tarot cards.
- **Dark Grey (#3a5055):** Panel backgrounds — the primary surface.
- **Darkest (#1e2b2d):** Deepest background, inset areas.
- **Grey (#708386):** Disabled text, labels.
- **Border Silver (#b9c2d2):** Panel top/side borders — the "silver frame."
- **Border South (#777e89):** Panel bottom border — creates the 3D depth illusion.
- **Panel Edge (#1e2e32):** Thin outer edge on panels.

Must-clause items glow blue. Should-clause items glow gold/green. Non-matching items render at 40-60% opacity with slight grayscale.

## Typography

m6x11plus (m6x11plusplus.otf) is the ONLY font. It is a single-weight pixel font. NEVER apply font-weight bold, semibold, or any weight other than 400. Bold makes it look muddy. NEVER USE HEAVY!

Text is NEVER ALL CAPS. Use proper Title Case or Sentence case. Seed codes use the display size (26px) in gold (#e4b643).

Contrast is critical. NEVER make grey text on top of a grey background. If using a dark grey background, use white or light contrasting text.

## Layout

Hard target: **375×667px (iPhone SE portrait)**. No scrolling on app-level screens. Components must fit within the viewport. Snap-scrolling is ONLY allowed for internal content areas that are explicitly paginated (e.g., ante pages within a fullscreen analyzer). Top-level app views must never scroll.

**Thumb Zone Rule:** Back buttons, navigation buttons, and primary actions ALWAYS go at the **bottom** of the screen. Users hold their phone with one hand and tap with their thumb — bottom-positioned buttons are always in reach. NEVER put back/nav buttons at the top of a mobile screen. Back buttons are ALWAYS orange and labeled "Back".

**Button Consistency:** All footer buttons use `lg` size across every screen. No mixing sizes in footers.

Horizontal swipe for seed navigation. NO visible scrollbars globally — use `::-webkit-scrollbar { display: none; }` and `-ms-overflow-style: none`.

Panels use 2px solid borders with border-silver on top/sides and border-south on bottom, creating a subtle 3D card effect. Inner shadow: `inset 0 0 0 1px rgba(255,255,255,0.04)`. Outer shadow: `0 2px 0 #000`.

### Responsive Breakpoints (Container Queries)

The j-app shell uses CSS **container queries** (not media queries) because it may live inside a mobile browser, a Claude MCP artifact, or a centered desktop preview. The container determines the layout, not the viewport.

| Name | Container Width | Height | Scroll | Usage |
|---|---|---|---|---|
| `compact` | ≤ 400px | Fixed 667px | Never | iPhone SE. Default. |
| `cozy` | 401–750px | Flexible | Vertical OK | MCP inline artifacts, wider phones. |
| `wide` | 751px+ | Flexible | Vertical OK | Tablet/desktop (future). |

Default is `compact` (375×667 locked). Add `<JimboApp fluid>` or `.j-app--fluid` to unlock for MCP/desktop contexts. This lets the container stretch to fill its parent (up to 750px) and activates `@container jimbo` queries in CSS.

**What changes compact → cozy:**
- Height constraint lifts — content determines height
- Vertical scroll becomes OK (host manages scroll)
- Padding bumps from `--j-space-lg` to `--j-space-xl`
- Stat grid values bump to 20px
- Info card titles bump to 14px
- Section header tags bump to 12px
- Buttons STAY `lg` — thumb zone rule still applies
- Footer STAYS bottom-anchored

**What does NOT change:** Colors, fonts, components, spacing tokens, button behavior, animation. The design language is identical — only density shifts.

## Elevation & Depth

Buttons have a colored "underside" via box-shadow (not blur). On press, translateY increases by 2-3px and the shadow collapses — the button physically sinks. On hover, apply a tiny brightness bump (no lift).

Panels sit on a dark south-shadow (`0 3px 0 rgba(0,0,0,0.55)`). Translucent panels (for swirl-background contexts) use `rgba(15, 24, 26, 0.78)` with `backdrop-filter: blur(2px)`.

JAML-hit items get a GlowRing: `box-shadow: 0 0 0 2px [color], 0 0 10px [color]` with a 1.6s pulse animation. Must = blue glow, should = gold/green glow.

**Button:** Chunky 3D press. DO NOT ADD A COLORED EDGE/BORDER. Buttons rely entirely on a solid `box-shadow` to create the colored "underside" depth. Hover brightness bump. Press sinks +2-3px and the box-shadow collapses to 0. Variants: primary (red), secondary (blue), back (orange), default (grey). NO GOLD BUTTONS. Sizes via padding, not font-size. Easing: `cubic-bezier(0.34, 1.56, 0.64, 1)`.

**Badge (JimboBadge):** Badges indicate status and DO NOT CLICK. They are strictly flat. They have a colored background and flat borders, but NO 3D BOX-SHADOW and NO PRESS ANIMATIONS. Variants: dark, blue, red, green, orange, purple, grey. NO GOLD BADGES.

**Panel:** Dark grey (#3a5055) background, 2px solid border (silver top/sides, south bottom), border-radius 6px. Inner highlight: `inset 0 0 0 1px rgba(255,255,255,0.04)`. Drop: `0 2px 0 #000`.

**Tabs (JimboTabs):** Active tab = gold background + black text. Inactive = transparent + grey text. No bold. Small radius (4px). Tabs are the ONLY navigation — no hamburger menus, no sidebars on mobile.

**ScoreCol:** Must-clauses show as blue-framed boxes with checkmark/cross badge. Should-clauses show as bare sprites with a red corner count badge (x2, x3...). Unlit = 40% opacity + grayscale.

**ShopTape:** Horizontal grab-scrollable strip of item sprites. Edge fades (linear-gradient masks) on left/right. Hidden scrollbar. Cursor: grab/grabbing.

**PackCell:** Tap to fan — cards explode out with springy stagger (40ms delay, juice easing). Tap again to collapse. Closed state = single pack sprite with type label.

**GlowRing:** Pulsing outline around JAML-hit items. `0 0 0 2px [color], 0 0 10px [color]`. Animation: opacity 0.55 → 1.0 over 1.6s ease-in-out infinite.

**Card Hover Juice:** Cards receive a 3D magnetic tilt plus a "juice-up" scale animation (`scale(1.05) translateY(-2px)`) on hover to mimic a physical interaction.

**Font Dance:** Text can be made to wiggle with a staggered `translateY` offset animation across characters (`.j-font-dance-char`), creating a cozy pixel font animation.

**SeedPagerHeader:** Three columns: [left stride arrow] [identity panel with seed + copy + deck/stake] [right stride arrow]. Stride arrows are tall red bars with dark-red inset shadow. Identity panel is translucent with counter pip.

## Do's and Don'ts

- DO use m6x11plus for everything except code/monospace.
- DO design for 375×667px (iPhone SE). Everything must fit — no scrolling.
- DO use translateY + box-shadow for button depth. Not CSS 3D transforms.
- DO put back buttons and nav actions at the **bottom** of the screen (thumb zone).
- DON'T use font-weight bold or heavy. m6x11plus is single-weight.
- DON'T use ALL CAPS. It is considered an embellishment and ruins the aesthetic.
- DON'T put grey text on top of a grey background.
- DON'T use fat padding or margins. Balatro UI is dense and cozy.
- DON'T add visible scrollbars. Vertical magnetic snap-scroll + horizontal swipe only.
- DON'T use rounded corners larger than 10px. Balatro is chunky, not bubbly.
- DON'T use blur-based shadows for depth. Use solid colored box-shadows 80% opaque.
- DON'T put back/nav buttons at the top of a screen. They go at the BOTTOM.
- DON'T use redundant JS wrappers for `motely-wasm`. Import globally and `motely.boot()` once. Use `?worker&inline` for search workers rather than blob strings, and do not prop-drill `motelyWasmUrl`.
