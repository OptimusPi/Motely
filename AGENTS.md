# AGENTS.md

Hard rules for agents working in this repo. CLAUDE.md links here for the design constraints — don't violate them.

## Design rules

- Never use ALL CAPS.
- Never use bold or heavy font-weight.
- Never put grey text on a grey background.
- Mobile-first at 375px width. Every component must read at that width before any wider breakpoint is considered.
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
