# CLAUDE.md

Read these first, in this order:

1. **`AGENTS.md`** — agent rules. Mobile-first 375px, no ALL CAPS, no bold/heavy weight, no grey-on-grey, no visible scrollbars (use magnetic scroll snapping).
2. **`DESIGN.md`** — the design system this UI strictly follows. Re-read before adding a component.
3. **`AUDIT_BOOTSHARP.md`** — the consumer-side audit of the Bootsharp boundary with `motely-wasm` / `optimuspi/motelyjaml`. **Do not fix Bootsharp call sites in isolation** — the API the hooks call doesn't exist on the host yet. See companion `MotelyJAML/AUDIT_BOOTSHARP.md`.

## Companion repo

The browser WASM host lives in **`optimuspi/motelyjaml`** under `Motely.Wasm/`. It publishes the `motely-wasm` npm package this UI consumes. The contract between the two is currently broken; the authoritative audit is on the host side.

## Quick rules

- **`motely-wasm` is imported and booted once, at the module level** (`src/motelyBoot.ts`). Do NOT wrap it in additional JS abstractions.
- **No facade hooks around Bootsharp.** Subscribe to events with `.subscribe()` / `.unsubscribe()` per `MotelyJAML/BOOTSHARP.md § Events`. Do not overwrite handler properties globally — that's the bug `useSearch.ts` currently has.
- **Externalized `motely-wasm`** in `vite.config.ts` is intentional (consumers control resolution; iframe path uses unpkg importmap). Don't bundle it.
- **No emoji, no ALL CAPS** in UI copy. See `DESIGN.md`.

## When in doubt

If a Bootsharp call you want to make isn't exported, check `MotelyJAML/Motely.Wasm/Program.cs` on master — that's the authoritative export list. Don't synthesize the call hoping it'll exist later.
