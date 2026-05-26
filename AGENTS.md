# AGENTS.md

Hard rules for agents working in this repo. CLAUDE.md links here for the design constraints — don't violate them.

## Design rules

- Never use ALL CAPS.
- Never use bold or heavy font-weight.
- Never put grey text on a grey background. This includes `tone="grey"` JimboText sitting on `--j-darkest`, `--j-dark-grey`, `--j-teal-grey`, or `--j-surface-inset` surfaces — those are all "grey" to the eye. Pick a tone that has contrast against the actual background, or change the background.
- **Every component is a `Jimbo*` component.** No raw `<button>`, no anonymous helper components inside consumer screens, no off-brand primitives. If a `Jimbo*` doesn't exist for the thing you need, add it to `src/ui/` with a Storybook story and export it from `src/ui.ts`. If you feel the urge to add three or four at once, you're overshooting — pick one, ship it, see if you actually need the others.
- **Never use emoji as button icons or button content.** It's not 2012. Use `react-icons` (already a peer dep). Pick a consistent icon family (e.g. `react-icons/fi`) and use it everywhere a glyph is needed.
- **Item names go in `JimboTooltip`, not inline labels.** Showing item names ("Blueprint", "Perkeo", "Observatory") under or beside the sprite creates a permanent spacing fight on a 320px surface, and players already recognize the art. Render the sprite; attach a `JimboTooltip` for the name. Reserve inline text labels for counters, prices, or state — not identity.
- Canonical surface is **iPhone SE 5 portrait, 320×568, HARD LOCKED.** Every component must read at 320px width and fit inside 568px height — no scroll, no stretch, no reflow. The `.j-app` container is `width: 320px; min-width: 320px; max-width: 320px; height: 568px;` etc. by design. **This applies to every consumer including MCP App embeds and desktop.** We get this right at 320×568 first; we don't widen the design just because the host can render a larger viewport. If your component doesn't fit, redesign the component — do not relax the lock.
- "Juice" — squash, bounce, hover lift, dance — comes from CSS (`.j-font-dance-char`, `transform: scale(1.05) translateY(-2px)`, keyframe animations). Not from JS animation wrappers, not from Framer Motion, not from a useEffect loop.
- No visible scrollbars. Use magnetic scroll snapping (`scroll-snap-type`, `scroll-snap-align`) and hide overflow chrome.

## motely-wasm wrapper rule

Don't add JS wrappers around `motely-wasm`. Import `bootsharp` and `Motely` directly and call them. The canonical boot pattern is top-level await in the consumer's entry point:

```ts
import bootsharp, { Motely } from "motely-wasm";
await bootsharp.boot("/motely-wasm/bin");
```

Hooks that need the runtime should inline a Standby-guard (`bootsharp.getStatus() === bootsharp.BootStatus.Standby` → boot) — they should not depend on a context provider, a runtime hook, or any other indirection layer.

## Component placement

Components live in `jaml-ui` with a Storybook story. Consuming apps compose them — they do not define their own inline React components for game-card / Jimbo-styled UI. If you find yourself sketching a component inline in a consumer repo, the correct move is to add it here (with a story) and import it from the appropriate barrel.
