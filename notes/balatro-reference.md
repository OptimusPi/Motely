# Balatro Reference Notes

Captured from in-game screenshots the user shared. These are
**inspiration / vocabulary notes**, not a clone spec. The product is
*goofy whimsical cozy* — that's the mood, this doc is the toolkit.

---

## The big idea: square in the middle, chips float outside

Balatro's core layout works on every device because it commits to one
trick:

- **The focused gameplay area is roughly square.** It locks aspect, so
  it never stretches or squishes regardless of screen.
- **Auxiliary UI floats outside the square.** Score column, deck stack,
  profile chip, language toggle, settings icon — these *escape* the
  central panel and live in whatever margin the screen happens to give
  them.
- **The background is a full-bleed living swirl** (Milkdrop-style
  visualizer). It absorbs whatever space the square + chips don't use.

Why it's smart:

- Phone portrait: tight margin, chips dock to corners.
- Console / 16:9 desktop: lots of swirl + comfy chip spread.
- Ultrawide: same square, chips fan out.
- No layout thrash. The square stays square.

Examples spotted:

- **Shop screen:** SHOP marquee + Round score + Hands/Discards + $ + Run
  Info + Options live to the LEFT of the square. Deck stack (54/54 with
  topographic-back) lives to the RIGHT, outside the box.
- **New Run / Challenges screens:** Profile chip bottom-left, Language
  toggle bottom-right, link icon bottom-right, rocket icon top-left.
  None of those are inside the panel.

---

## Inside the square: panel anatomy

- **Panel fill:** dark slate-teal (~`#1d2a30` ish), big rounded corners
  (~12–16px).
- **Frame:** two-tone borderbox — outer thicker dark stroke + an inner
  lighter highlight line. Reads like a picture frame, not a single
  slab.
- **Internal sections** are sub-panels with their own dark fill +
  rounded corners + subtle inner border (e.g. the Custom Rules /
  Game Modifiers cards on Challenges screen).

---

## Buttons

The single most important visual unit. Every button looks like a
**physical chip you can press.**

- **Hard drop shadow** — `3–4px` straight down, **fully opaque black**,
  **no blur**. Not a bevel, not a soft shadow. Hard.
- **Outer dark stroke** — 1–2px outline in a darker tone of the same
  hue. Locks the color in.
- **Bold flat fill** — saturated, slightly chunky. No gradients.
- **Pixel-y display font**, white text, dark `text-shadow` for
  readability.
- **Big tap target.** Sized for thumbs (mobile) and D-pad focus
  (console). Avoid hairline buttons.
- **Color = function**, not decoration:
  - Red — primary action, list items, "do the thing"
  - Blue — Play / confirm / final action
  - Orange / gold — Back, Voucher, secondary
  - Green — positive / Reroll / checkmark / success
  - Purple — Tarot
  - Teal / cyan — Planet / Celestial
  - Indigo / dark blue — Spectral

Special button shapes:

- **Wide full-width "Back"** glued to the bottom of every panel.
  Always orange. Always in the same place. Thumb- and D-pad-reachable.
- **Big "PLAY"** centered, blue, the most prominent button on the screen
  it appears in.
- **Carousel arrows** — tall thin red rectangles with a `<` or `>`
  glyph, flanking a card or strip of content. Same chip treatment as
  regular buttons.

---

## Tabs

- **Horizontal tabs** as a row of red chip buttons. The active one gets
  a small **red triangle indicator** above it, pointing down.
- **The triangle bounces** — but gently. Linear straight up/down
  motion, with a slight hold at the downbeat each cycle. No ease-in-out
  velocity profile (no "growing/shrinking" feel).
- **Vertical tabs** — same pattern, rotated. Used along the side
  (e.g. JOKERS / CONSUMABLES / VOUCHERS in Challenges). Writing-mode
  rotation, not transformed text.

---

## Carousels (the "no modal" trick)

When picking from a list of options (decks, stakes, etc.), Balatro
doesn't open a modal — it shows **one item at a time with `<` / `>`
arrows** and **pagination dots** underneath.

- Big arrow buttons flank the content card.
- Dots below show position.
- Always visible. No backdrop. No popup.

This is the answer to "how do I pick a thing without losing my view of
the rest of the screen?" — *don't open something, advance through
something.*

---

## Two-pane layouts (the OTHER "no modal" trick)

For larger sets, Balatro uses a **list on the left, detail on the
right** layout instead of a fullscreen picker.

- Challenges screen: 1–10 numbered list on left, currently-selected
  challenge's rules + modifiers + Play button on right.
- Shop screen: stats column on left of the play area, slots on the
  right.

The point: **the source list and the active selection are on screen at
the same time.** You never lose context.

---

## Typography

- **Pixel-y display font** for titles, labels, all UI chrome.
- White text with **dark `text-shadow`** for legibility on saturated
  fills.
- **Color tokens inside body text** for emphasis — e.g. "start with
  Tarot Merchant" with "Tarot Merchant" rendered in tarot-purple,
  "Planet Merchant" in planet-teal. The text itself is multicolor when
  it references game concepts.

---

## Motion

- **Linear easing for indicator bounce** — no ease-in-out. The motion
  is constant-velocity up, constant-velocity down, with a *hold* at the
  downbeat.
- **Pop-in / scale-bounce on cards** as they appear in shops/packs —
  but quick, never floaty.
- **No motion blur**, no soft fades. Things land or pop.
- **No transitions on hover** that imply lightness. Brightness shift,
  yes. Translate-up on hover, no.

---

## Background

- Full-bleed, animated, swirling, color-rich — feels like a music
  visualizer (the user explicitly named Milkdrop, the Winamp
  visualizer).
- Sits **behind everything**. The square never has its own background
  image; it just sits on top of the swirl.
- Different decks/stakes/states can change the swirl palette.

---

## Anti-patterns (things that drift the look toward "generic modern UI")

The CSS defaults that pull *against* this aesthetic — fight these
explicitly:

- ❌ Hairline 1px borders
- ❌ Soft blurred `box-shadow`s (`blur > 0`)
- ❌ `ease-in-out` for repeating bounces (gives a "growing/shrinking"
  velocity feel — wrong for on-beat motion)
- ❌ Gradients on buttons
- ❌ Fullscreen modals with opaque backdrops covering the play area
- ❌ Thin "modern" sans-serifs
- ❌ Subtle low-saturation colors (the palette is BOLD)
- ❌ Translate-on-hover lift effects (too "web-y", not "console-y")
- ❌ `transition: all 200ms ease` defaults — be explicit per property

---

## Console-thumb checklist

Whenever building a new screen, ask:

- Can a thumb reach every primary action? (Bottom of the screen
  matters.)
- Could a D-pad navigate this without a mouse? (Implies focus order
  and big focusable targets.)
- Is there a clearly visible "Back" at the bottom?
- Are list items full-width clickable, not just a tiny icon?
- Does the layout still work if I rotate to portrait? (Square +
  floating chips trick.)

---

## Mood notes

The vibe is **goofy whimsical cozy**, not slick or premium.

- Imperfect-looking fonts beat smooth ones.
- Hard chunky shadows beat soft realistic ones.
- Bold saturated fills beat tasteful muted palettes.
- A weird tilt or off-center icon is good — it adds personality.
- The UI should feel like it could be holding a small woodland creature.
